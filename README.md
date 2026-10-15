# Saraa Boutique - Desktop POS (HyperPOS)

A pure native Windows desktop Point of Sale (POS) application built with **WPF (.NET 8)** and an **offline SQLite database**.

## Features
- **Pure Native Desktop Application**: Built with C# and WPF for fast rendering and high DPI support.
- **Offline First**: All transactions, inventory, and shifts are stored locally in an embedded SQLite database (`hyperpos.db`).
- **Saraa Boutique Catalog**: Preloaded with 58 store categories, products, stock counts, and prices.
- **Super Admin Authentication**: Secure login supporting BCrypt password hashing.
- **POS Operations**:
  - Barcode scanning & quick search
  - Category navigation with smooth mouse-wheel horizontal scrolling
  - Real-time shopping cart with quantity steppers and line totals
  - Hold/Suspend cart & Recall held orders
  - Cash, Card, and UPI tender settlement
  - Shift management (Opening drawer cash & Day-end settlement with denomination counter)

## Tech Stack
- **Framework**: .NET 8 (Windows Desktop WPF)
- **Database**: SQLite (`Microsoft.Data.Sqlite`)
- **Security**: `BCrypt.Net-Next`
- **Architecture**: MVVM / Code-Behind with modular service layer

## How to Run from Source
```powershell
# Restore & build
dotnet build HyperPOS_Windows_CSharp/HyperPOS.Desktop/HyperPOS.Desktop.csproj

# Run application
dotnet run --project HyperPOS_Windows_CSharp/HyperPOS.Desktop/HyperPOS.Desktop.csproj
```
