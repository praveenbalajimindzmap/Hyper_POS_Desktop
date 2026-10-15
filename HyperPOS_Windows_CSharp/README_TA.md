# Hyper POS Windows App (C# / WebView2)

இந்த project, உங்கள் தற்போதைய POS website `https://jomjom.sg/admin/pos`-ஐ Windows desktop application window-ல் திறக்கும் C# WinForms starter app.

## முக்கிய வேறுபாடு
இது website-ஐ Windows app window-ல் காட்டும் wrapper. Screenshot-ல் உள்ள UI உங்கள் தற்போதைய website-லிருந்தே வரும். இது இன்னும் native C# POS அல்ல; மேலும் internet இல்லாமல் வேலை செய்யும் offline conversion-யும் இதனால் மட்டும் கிடைக்காது.

## தேவைகள்
- Windows 10/11
- .NET 8 SDK
- Microsoft Edge WebView2 Runtime
- POS website-க்கு login access

## இயக்குவது
1. ZIP-ஐ extract செய்யவும்.
2. `HyperPOS.Windows.csproj` உள்ள folder-ல் Terminal திறக்கவும்.
3. இயக்கவும்:

```powershell
dotnet restore
dotnet run
```

4. Publish செய்ய Self-contained Windows x64 build:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true
```

Publish output பொதுவாக `bin\\Release\\net8.0-windows\\win-x64\\publish\\` folder-ல் இருக்கும்.

## Keyboard shortcuts
- `F11`: fullscreen toggle
- `Esc`: fullscreen-இலிருந்து வெளியேறு
- `Ctrl+R`: POS page reload

## URL மாற்றுவது
`PosForm.cs`-ல் உள்ள `PosUrl` constant-ஐ மாற்றவும்.

## தனி Native Windows POS application
இந்த wrapper-இற்கு தனியாக C# WPF + SQLite offline POS application `HyperPOS.Desktop` folder-ல் உள்ளது. இயக்குவதற்கு அந்த folder-ல் Terminal திறந்து `dotnet restore` மற்றும் `dotnet run` இயக்கவும். அதன் local database `%LOCALAPPDATA%\\HyperPOS.Desktop\\hyperpos.db` இடத்தில் சேமிக்கப்படும்.

வலைத்தள POS பொருட்களை பெற, கொடுக்கப்பட்ட PHP திட்டத்தின் read-only `GET /api/v1/desktop-pos/products` endpoint-ஐ deploy செய்து, server-ல் `DESKTOP_POS_API_TOKEN` அமைக்கவும். Desktop app-இன் Product sync settings-ல் HTTPS site root மற்றும் அந்த key-ஐ உள்ளிட்டு catalog-ஐ local SQLite-க்கு sync செய்யலாம். Product sync மட்டும் இணையத்துடன் நடக்கும்; sales இன்னும் desktop-இல் மட்டுமே local ஆக சேமிக்கப்படும்.
