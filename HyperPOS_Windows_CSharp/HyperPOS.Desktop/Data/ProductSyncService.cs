using System.Net.Http;
using System.Net.Http.Headers;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using HyperPOS.Desktop.Models;

namespace HyperPOS.Desktop.Data;

public sealed class ProductSyncService
{
    private const int PageSize = 100;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<int> SyncAsync(
        SyncConfiguration configuration,
        PosDatabase database,
        CancellationToken cancellationToken = default)
    {
        var baseUri = ValidateBaseUrl(configuration.BaseUrl);
        if (string.IsNullOrWhiteSpace(configuration.ApiToken))
            throw new InvalidOperationException("A product-sync API key is required.");

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", configuration.ApiToken);
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));

        var products = new List<RemoteProduct>();
        var page = 1;
        int? expectedTotal = null;
        while (true)
        {
            var requestUri = new Uri(
                baseUri,
                $"api/v1/desktop-pos/products?page={page}&per_page={PageSize}");
            using var response = await client.GetAsync(requestUri, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Product sync failed with HTTP {(int)response.StatusCode} ({response.ReasonPhrase}). " +
                    "Confirm the API endpoint is deployed and the product-sync key is correct.");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var result = await JsonSerializer.DeserializeAsync<CatalogPage>(
                stream, JsonOptions, cancellationToken)
                ?? throw new InvalidDataException("The product-sync API returned an empty response.");

            if (result.Page != page || result.PerPage != PageSize || result.Total < 0)
                throw new InvalidDataException("The product-sync API returned invalid paging metadata.");
            if (expectedTotal.HasValue && expectedTotal.Value != result.Total)
                throw new InvalidDataException("The online catalog changed during sync. Retry the sync.");
            expectedTotal = result.Total;
            products.AddRange(result.Data);

            if (!result.HasMore)
            {
                if (products.Count != result.Total)
                    throw new InvalidDataException("The product-sync API returned an incomplete catalog.");
                break;
            }

            if (result.Data.Count == 0 || products.Count >= result.Total)
                throw new InvalidDataException("The product-sync API returned inconsistent paging data.");
            page++;
        }

        return database.SyncRemoteProducts(products);
    }

    public static Uri ValidateBaseUrl(string baseUrl)
    {
        if (!Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && !(uri.IsLoopback && uri.Scheme == Uri.UriSchemeHttp)) ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new InvalidOperationException(
                "Enter a site root using HTTPS (HTTP is allowed only for localhost), for example https://saraaboutique.com.");
        }

        var normalizedPath = uri.AbsolutePath.Trim('/');
        if (normalizedPath.Length > 0)
        {
            throw new InvalidOperationException("Enter the site root URL, not an admin page or API path.");
        }

        return new Uri(uri.GetLeftPart(UriPartial.Authority) + "/", UriKind.Absolute);
    }

    private sealed class CatalogPage
    {
        [JsonPropertyName("data")]
        public List<RemoteProduct> Data { get; init; } = [];

        [JsonPropertyName("page")]
        public int Page { get; init; }

        [JsonPropertyName("per_page")]
        public int PerPage { get; init; }

        [JsonPropertyName("total")]
        public int Total { get; init; }

        [JsonPropertyName("has_more")]
        public bool HasMore { get; init; }
    }
}
