# HyperPOS Desktop

A native Windows point-of-sale app built with C# WPF and SQLite. It caches the web POS catalog for offline use and stores desktop sales locally. Its layout follows the supplied screenshot and the local PHP POS views at `resources/views/admin-views/pos/index.blade.php` and `resources/views/admin-views/pos/partials/_cart.blade.php`.

## Requirements

- Windows 10 or 11
- .NET 8 SDK

## Run

From this directory:

```powershell
dotnet restore
dotnet run
```

The SQLite database is created at `%LOCALAPPDATA%\HyperPOS.Desktop\hyperpos.db`. New databases start with an empty product catalog until the first successful sync. Sample products from earlier builds are hidden from checkout; if one already appears on a saved receipt, its archived row is kept for that receipt.

## Connect the website catalog

The provided SQL dump (`Downloads/latest_final_empty_db_3.sql`) has a products table definition but no product rows. The desktop app therefore fetches POS-eligible products from the PHP application's read-only endpoint and stores them in SQLite:

1. Deploy the endpoint additions in the provided PHP project to the website.
2. On the server, set `DESKTOP_POS_API_TOKEN` to a unique random secret of at least 32 characters. For example, generate one with `php -r "echo bin2hex(random_bytes(32)), PHP_EOL;"`. Do not commit or share the secret.
3. Clear cached Laravel configuration after changing the server environment: `php artisan config:clear`.
4. Open **Product sync settings** in the desktop app, enter the HTTPS site root (for example `https://saraaboutique.com`) and the server key, then select **Save & sync**.
5. Use **Sync products** to refresh later. The app only replaces the local online-product cache after every catalog page downloads and validates successfully.

The API route is `GET /api/v1/desktop-pos/products`; it requires the server key as an HTTP Bearer token and is rate-limited. It only returns active, in-house, physical POS products without variants. Digital products and variant products are intentionally excluded because this desktop starter does not yet have matching variant selection and per-variant inventory handling.

## Current functionality

- Browse and search cached products by name, SKU, and barcode; filter by category.
- Add local-only products with category, SKU, optional barcode, price, and opening stock.
- Import products from CSV or Excel (`.xlsx`) in **Products → Import CSV / Excel**. The importer finds a product header row after report titles and filter notes. It recognizes common headings such as `Product Name`, `Product Code`, `Product Unit Price`, `Selling Price`, `Quantity`, and `Current Stock Amount`. Category and barcode are optional; reports without a SKU generate a stable SKU from the product name and report serial number (`SL`). Quoted values and comma-containing names are supported in CSV files. A repeat import updates locally imported products by SKU; products missing from the report stay unchanged. The whole file is validated before it is saved. Website-synced product SKUs cannot be overwritten by an import.
- Build a cart, adjust quantities, and prevent quantities exceeding available stock.
- Record cash, card, digital/UPI, or gift-card payment types locally; cash checkout calculates change.
- Open a local cash shift by recording opening cash before checkout. The opening amount is not editable after the shift is started.
- Save a cart as a held order with a customer/order name, search held orders, resume them, or delete them. Holding a cart does not deduct or reserve stock.
- Review receipts by business date, see daily order and sales totals, open receipt details, and print through the Windows print dialog.
- Close a shift with a denomination-based cash count, compare counted cash with opening cash plus recorded cash sales, select a variance reason, and save notes.
- Scan a product barcode or SKU in the scanner box and press Enter to add it to the cart.
- Use the split POS layout with the receipt/cart at left, product grid at right, compact store header, green stock/payment accents, and category/search toolbar.
- Save the sale, sale lines, and stock deduction in a single SQLite transaction.
- Review completed sales and receipt details by date, including earlier weeks.

All amounts currently use Indian rupees. Tax is not configured or calculated yet. Payments are recorded only; the app does not connect to card terminals or online payment providers. Product prices and server stock refresh from the website on sync; desktop shifts, held orders, checkout, settlement, sales, and receipts remain local and are **not uploaded to the website**. Local sales reduce the cached stock. Refreshes apply the website's stock changes while preserving local sales since the previous refresh, but other registers cannot see these sales until a sales-upload workflow is built.

The supplied SQL dump contains no actual products, so synchronizing requires the desktop endpoint to be deployed on the live Laravel site. The public admin POS URL may require an authenticated admin session; the API key is configured separately for the desktop connection.
