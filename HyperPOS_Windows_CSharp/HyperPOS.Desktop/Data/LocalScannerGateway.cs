using System.IO;
using System.Net;
using System.Text;

namespace HyperPOS.Desktop.Data;

public sealed class LocalScannerGateway : IDisposable
{
    private HttpListener? listener;
    private bool isRunning;
    public event Action<string>? OnBarcodeReceived;

    public int Port { get; }

    public LocalScannerGateway(int port = 8085)
    {
        Port = port;
    }

    public void Start()
    {
        if (isRunning) return;

        try
        {
            listener = new HttpListener();
            listener.Prefixes.Add($"http://*:{Port}/scan/");
            listener.Start();
            isRunning = true;
            listener.BeginGetContext(OnContextReceived, null);
        }
        catch (HttpListenerException)
        {
            // Fallback to localhost if wildcard requires elevated admin permissions
            try
            {
                listener?.Close();
                listener = new HttpListener();
                listener.Prefixes.Add($"http://localhost:{Port}/scan/");
                listener.Start();
                isRunning = true;
                listener.BeginGetContext(OnContextReceived, null);
            }
            catch
            {
                // Port unavailable or permissions restricted, silently degrade
                isRunning = false;
            }
        }
    }

    private void OnContextReceived(IAsyncResult ar)
    {
        if (!isRunning || listener == null || !listener.IsListening)
            return;

        try
        {
            var context = listener.EndGetContext(ar);
            // Immediately listen for next incoming scan
            listener.BeginGetContext(OnContextReceived, null);

            var request = context.Request;
            var barcode = request.QueryString["code"] ?? request.QueryString["barcode"];

            if (string.IsNullOrWhiteSpace(barcode) && request.HttpMethod == "POST" && request.HasEntityBody)
            {
                using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                barcode = reader.ReadToEnd().Trim();
            }

            if (!string.IsNullOrWhiteSpace(barcode))
            {
                OnBarcodeReceived?.Invoke(barcode.Trim());
            }

            // CORS headers so web/flutter callers have zero restrictions
            context.Response.AddHeader("Access-Control-Allow-Origin", "*");
            context.Response.ContentType = "application/json";
            var responseBytes = Encoding.UTF8.GetBytes("{\"status\":\"success\",\"message\":\"Scanned\"}");
            context.Response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
            context.Response.Close();
        }
        catch
        {
            // Ignore socket disconnects
        }
    }

    public void Stop()
    {
        isRunning = false;
        try
        {
            listener?.Stop();
            listener?.Close();
        }
        catch { }
    }

    public void Dispose() => Stop();
}
