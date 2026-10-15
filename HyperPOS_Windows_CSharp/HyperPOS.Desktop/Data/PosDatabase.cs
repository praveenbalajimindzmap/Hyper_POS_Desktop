using HyperPOS.Desktop.Models;
using Microsoft.Data.Sqlite;
using System.IO;
using System.Collections.Generic;

namespace HyperPOS.Desktop.Data;

public sealed class PosDatabase
{
    private readonly string connectionString;

    public string DatabasePath { get; }

    public PosDatabase()
        : this(GetDefaultDatabasePath())
    {
    }

    public static string GetDefaultDatabasePath()
    {
        try
        {
            // 1. Portable mode: always use hyperpos.db in the application directory
            var appDirDb = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "hyperpos.db");
            var dir = AppDomain.CurrentDomain.BaseDirectory;
            // Check write access by testing directory
            if (Directory.Exists(dir))
            {
                return appDirDb;
            }
        }
        catch
        {
            // Fallback
        }

        // 2. Fallback to AppData Local (if app directory is read-only, e.g. Program Files)
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HyperPOS.Desktop",
            "hyperpos.db");
    }

    public PosDatabase(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        var fullPath = Path.GetFullPath(databasePath);
        var dataDirectory = Path.GetDirectoryName(fullPath)
            ?? throw new ArgumentException("The database path must include a parent directory.", nameof(databasePath));
        Directory.CreateDirectory(dataDirectory);
        DatabasePath = fullPath;
        connectionString = new SqliteConnectionStringBuilder { DataSource = DatabasePath }.ToString();
        Initialize();
    }

    public List<Product> GetProducts(string? search = null, string? category = null)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, name, category, sku, barcode, price, stock, image_url, original_price, discount, discount_type
            FROM products
            WHERE is_sample = 0
              AND is_active = 1
              AND (@search IS NULL OR name LIKE @pattern OR sku LIKE @pattern OR barcode LIKE @pattern)
              AND (@category IS NULL OR category = @category)
            ORDER BY name COLLATE NOCASE;
            """;
        command.Parameters.AddWithValue("@search", string.IsNullOrWhiteSpace(search) ? DBNull.Value : search);
        command.Parameters.AddWithValue("@pattern", $"%{search?.Trim()}%");
        command.Parameters.AddWithValue("@category", string.IsNullOrWhiteSpace(category) || category == "All"
            ? DBNull.Value
            : category);

        using var reader = command.ExecuteReader();
        var products = new List<Product>();
        while (reader.Read())
        {
            var price = reader.GetDecimal(5);
            var origPrice = reader.IsDBNull(8) ? price : reader.GetDecimal(8);
            var discount = reader.IsDBNull(9) ? 100m : reader.GetDecimal(9);
            var discountType = reader.IsDBNull(10) ? "flat" : reader.GetString(10);

            products.Add(new Product
            {
                Id = reader.GetInt64(0),
                Name = reader.GetString(1),
                Category = reader.GetString(2),
                Sku = reader.GetString(3),
                Barcode = reader.GetString(4),
                Price = price,
                Stock = reader.GetInt32(6),
                ImageUrl = reader.IsDBNull(7) ? null : reader.GetString(7),
                OriginalPrice = origPrice,
                Discount = discount,
                DiscountType = discountType
            });
        }

        return products;
    }

    public List<string> GetCategories()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT name FROM (
                SELECT c.name, 
                       (SELECT COUNT(*) FROM products p WHERE p.category = c.name AND p.is_active = 1) AS prod_count,
                       c.id AS sort_id
                FROM categories c
                UNION
                SELECT DISTINCT p.category AS name, 
                       COUNT(*) AS prod_count,
                       9999 AS sort_id
                FROM products p
                WHERE p.is_sample = 0 AND p.is_active = 1
                GROUP BY p.category
            )
            GROUP BY name
            ORDER BY 
                CASE WHEN prod_count > 0 THEN 0 ELSE 1 END,
                prod_count DESC,
                sort_id ASC;
            """;
        using var reader = command.ExecuteReader();
        var categories = new List<string> { "All" };
        while (reader.Read())
        {
            var catName = reader.GetString(0);
            if (!categories.Contains(catName, StringComparer.OrdinalIgnoreCase))
                categories.Add(catName);
        }
        return categories;
    }

    public void AddProduct(string name, string category, string sku, string barcode, decimal price, int stock, string? imageUrl = null, decimal? originalPrice = null, decimal discount = 100, string discountType = "flat")
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO products (name, category, sku, barcode, price, stock, is_sample, is_active, image_url, original_price, discount, discount_type)
            VALUES (@name, @category, @sku, @barcode, @price, @stock, 0, 1, @imageUrl, @originalPrice, @discount, @discountType);
            """;
        command.Parameters.AddWithValue("@name", name.Trim());
        command.Parameters.AddWithValue("@category", category.Trim());
        command.Parameters.AddWithValue("@sku", sku.Trim());
        command.Parameters.AddWithValue("@barcode", barcode.Trim());
        command.Parameters.AddWithValue("@price", price);
        command.Parameters.AddWithValue("@stock", stock);
        command.Parameters.AddWithValue("@imageUrl", string.IsNullOrWhiteSpace(imageUrl) ? DBNull.Value : imageUrl);
        command.Parameters.AddWithValue("@originalPrice", originalPrice.HasValue ? originalPrice.Value : (object)DBNull.Value);
        command.Parameters.AddWithValue("@discount", discount);
        command.Parameters.AddWithValue("@discountType", discountType);
        command.ExecuteNonQuery();
    }

    public void UpdateProductImageUrl(string sku, string imageUrl)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE products SET image_url = @imageUrl WHERE sku = @sku;";
        command.Parameters.AddWithValue("@imageUrl", imageUrl);
        command.Parameters.AddWithValue("@sku", sku);
        command.ExecuteNonQuery();
    }

    public void UpdateProductPricing(string sku, decimal originalPrice, decimal discount, string discountType, decimal sellingPrice, string? imageUrl = null, int? stock = null)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE products 
            SET original_price = @originalPrice, 
                discount = @discount, 
                discount_type = @discountType, 
                price = @sellingPrice,
                image_url = COALESCE(@imageUrl, image_url),
                stock = COALESCE(@stock, stock)
            WHERE sku = @sku;
            """;
        command.Parameters.AddWithValue("@sku", sku);
        command.Parameters.AddWithValue("@originalPrice", originalPrice);
        command.Parameters.AddWithValue("@discount", discount);
        command.Parameters.AddWithValue("@discountType", discountType);
        command.Parameters.AddWithValue("@sellingPrice", sellingPrice);
        command.Parameters.AddWithValue("@imageUrl", string.IsNullOrWhiteSpace(imageUrl) ? DBNull.Value : imageUrl);
        command.Parameters.AddWithValue("@stock", stock.HasValue ? stock.Value : DBNull.Value);
        command.ExecuteNonQuery();
    }

    public ProductImportResult ImportLocalProducts(IReadOnlyCollection<ImportedProduct> products)
    {
        if (products.Count == 0)
            throw new InvalidOperationException("The CSV contains no products to import.");

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        var added = 0;
        var updated = 0;
        foreach (var product in products)
        {
            using var existingCommand = connection.CreateCommand();
            existingCommand.Transaction = transaction;
            existingCommand.CommandText = """
                SELECT id, source_id
                FROM products
                WHERE lower(sku) = lower(@sku)
                LIMIT 1;
                """;
            existingCommand.Parameters.AddWithValue("@sku", product.Sku.Trim());
            using var existingReader = existingCommand.ExecuteReader();
            var existingId = 0L;
            var isRemote = false;
            if (existingReader.Read())
            {
                existingId = existingReader.GetInt64(0);
                isRemote = !existingReader.IsDBNull(1);
            }
            existingReader.Close();

            if (isRemote)
                throw new InvalidDataException(
                    $"SKU '{product.Sku}' belongs to a website-synced product. Change the CSV SKU or update it through product sync.");

            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            if (existingId == 0)
            {
                command.CommandText = """
                    INSERT INTO products
                        (name, category, sku, barcode, price, stock, is_sample, is_active)
                    VALUES
                        (@name, @category, @sku, @barcode, @price, @stock, 0, 1);
                    """;
                added++;
            }
            else
            {
                command.CommandText = """
                    UPDATE products
                    SET name = @name, category = @category, barcode = @barcode,
                        price = @price, stock = @stock, is_sample = 0, is_active = 1
                    WHERE id = @id;
                    """;
                command.Parameters.AddWithValue("@id", existingId);
                updated++;
            }
            command.Parameters.AddWithValue("@name", product.Name.Trim());
            command.Parameters.AddWithValue("@category",
                string.IsNullOrWhiteSpace(product.Category) ? "Uncategorized" : product.Category.Trim());
            command.Parameters.AddWithValue("@sku", product.Sku.Trim());
            command.Parameters.AddWithValue("@barcode", product.Barcode.Trim());
            command.Parameters.AddWithValue("@price", product.Price);
            command.Parameters.AddWithValue("@stock", product.Stock);
            command.ExecuteNonQuery();
        }

        transaction.Commit();
        return new ProductImportResult { Added = added, Updated = updated };
    }

    public int SyncRemoteProducts(IReadOnlyCollection<RemoteProduct> products)
    {
        if (products.Any(product =>
                product.Id <= 0 ||
                string.IsNullOrWhiteSpace(product.Name) ||
                string.IsNullOrWhiteSpace(product.Sku) ||
                product.Price < 0 ||
                product.Stock < 0))
        {
            throw new InvalidDataException("The catalog response contains a product with invalid required fields.");
        }

        if (products.Select(product => product.Id).Distinct().Count() != products.Count)
            throw new InvalidDataException("The catalog response contains duplicate product IDs.");

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        using (var deactivateCommand = connection.CreateCommand())
        {
            deactivateCommand.Transaction = transaction;
            deactivateCommand.CommandText =
                "UPDATE products SET is_active = 0 WHERE source_id IS NOT NULL;";
            deactivateCommand.ExecuteNonQuery();
        }

        foreach (var product in products)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO products
                    (source_id, name, category, sku, barcode, price, stock, source_stock, is_sample, is_active)
                VALUES
                    (@sourceId, @name, @category, @sku, @barcode, @price, @stock, @stock, 0, 1)
                ON CONFLICT(source_id) WHERE source_id IS NOT NULL DO UPDATE SET
                    name = excluded.name,
                    category = excluded.category,
                    sku = excluded.sku,
                    barcode = excluded.barcode,
                    price = excluded.price,
                    stock = MAX(0, excluded.stock + products.stock - COALESCE(products.source_stock, excluded.stock)),
                    source_stock = excluded.stock,
                    is_active = 1,
                    is_sample = 0;
                """;
            command.Parameters.AddWithValue("@sourceId", product.Id);
            command.Parameters.AddWithValue("@name", product.Name.Trim());
            command.Parameters.AddWithValue("@category",
                string.IsNullOrWhiteSpace(product.Category) ? "Uncategorized" : product.Category.Trim());
            command.Parameters.AddWithValue("@sku", product.Sku.Trim());
            command.Parameters.AddWithValue("@barcode", product.Barcode?.Trim() ?? "");
            command.Parameters.AddWithValue("@price", product.Price);
            command.Parameters.AddWithValue("@stock", product.Stock);
            command.ExecuteNonQuery();
        }

        transaction.Commit();
        return products.Count;
    }

    public List<SaleSummary> GetSales(DateTime? businessDate = null, string? cashierEmail = null)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT s.id, s.receipt_number, s.created_at, s.customer_name, s.payment_method, s.total,
                   COUNT(si.id),
                   COALESCE(s.order_status, 'Delivered'),
                   COALESCE(s.customer_phone, 'No phone number'),
                   COALESCE(s.refunded_amount, 0)
            FROM sales s
            LEFT JOIN sale_items si ON si.sale_id = s.id
            WHERE (@businessDate IS NULL OR substr(s.created_at, 1, 10) = @businessDate)
              AND (@cashierEmail IS NULL OR s.cashier_email = @cashierEmail)
            GROUP BY s.id
            ORDER BY s.id DESC;
            """;
        command.Parameters.AddWithValue("@businessDate",
            (object?)businessDate?.ToString("yyyy-MM-dd") ?? DBNull.Value);
        command.Parameters.AddWithValue("@cashierEmail",
            string.IsNullOrWhiteSpace(cashierEmail) ? DBNull.Value : cashierEmail);
        using var reader = command.ExecuteReader();
        var sales = new List<SaleSummary>();
        while (reader.Read())
        {
            sales.Add(new SaleSummary
            {
                Id = reader.GetInt64(0),
                ReceiptNumber = reader.GetString(1),
                CreatedAt = DateTime.Parse(reader.GetString(2)),
                CustomerName = reader.GetString(3),
                PaymentMethod = reader.GetString(4),
                Total = reader.GetDecimal(5),
                ItemCount = reader.GetInt32(6),
                Status = reader.GetString(7),
                CustomerPhone = reader.GetString(8),
                RefundedAmount = reader.GetDecimal(9)
            });
        }

        return sales;
    }

    public PosShift? GetActiveShift(string? cashierEmail = null)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, business_date, opened_at, opening_cash
            FROM pos_shifts
            WHERE closed_at IS NULL
              AND (@cashierEmail IS NULL OR cashier_email = @cashierEmail)
            ORDER BY id DESC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@cashierEmail",
            string.IsNullOrWhiteSpace(cashierEmail) ? DBNull.Value : cashierEmail);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
            return null;

        return new PosShift
        {
            Id = reader.GetInt64(0),
            BusinessDate = DateTime.Parse(reader.GetString(1)),
            OpenedAt = DateTime.Parse(reader.GetString(2)),
            OpeningCash = reader.GetDecimal(3)
        };
    }

    public void OpenShift(decimal openingCash, string cashierName = "Praveen Balaji", string cashierEmail = "praveenbalajimindzmap@gmail.com")
    {
        if (openingCash <= 0 || openingCash > 999999.99m)
            throw new ArgumentOutOfRangeException(nameof(openingCash), "Opening cash must be between ₹0.01 and ₹999,999.99.");

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO pos_shifts (business_date, opened_at, opening_cash, cashier_name, cashier_email)
            SELECT @businessDate, @openedAt, @openingCash, @cashierName, @cashierEmail
            WHERE NOT EXISTS (SELECT 1 FROM pos_shifts WHERE closed_at IS NULL AND (cashier_email = @cashierEmail OR @cashierEmail IS NULL));
            """;
        command.Parameters.AddWithValue("@businessDate", DateTime.Today.ToString("yyyy-MM-dd"));
        command.Parameters.AddWithValue("@openedAt", DateTime.Now.ToString("O"));
        command.Parameters.AddWithValue("@openingCash", openingCash);
        command.Parameters.AddWithValue("@cashierName", cashierName);
        command.Parameters.AddWithValue("@cashierEmail", cashierEmail);
        if (command.ExecuteNonQuery() != 1)
            throw new InvalidOperationException("A POS shift is already open for this cashier.");
    }

    public List<HeldOrderSummary> GetHeldOrders(string? cashierEmail = null)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT h.id, h.customer_name, h.held_at, COUNT(i.id), COALESCE(SUM(i.unit_price * i.quantity), 0)
            FROM held_orders h
            LEFT JOIN held_order_items i ON i.held_order_id = h.id
            WHERE (@cashierEmail IS NULL OR h.cashier_email = @cashierEmail)
            GROUP BY h.id
            ORDER BY h.id DESC;
            """;
        command.Parameters.AddWithValue("@cashierEmail",
            string.IsNullOrWhiteSpace(cashierEmail) ? DBNull.Value : cashierEmail);
        using var reader = command.ExecuteReader();
        var orders = new List<HeldOrderSummary>();
        while (reader.Read())
        {
            orders.Add(new HeldOrderSummary
            {
                Id = reader.GetInt64(0),
                CustomerName = reader.GetString(1),
                HeldAt = DateTime.Parse(reader.GetString(2)),
                ItemCount = reader.GetInt32(3),
                Total = reader.GetDecimal(4)
            });
        }

        return orders;
    }

    public long SaveHeldOrder(string customerName, IReadOnlyCollection<CartItem> items, string cashierName = "Praveen Balaji", string cashierEmail = "praveenbalajimindzmap@gmail.com")
    {
        if (items.Count == 0)
            throw new InvalidOperationException("The cart is empty.");

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        using var orderCommand = connection.CreateCommand();
        orderCommand.Transaction = transaction;
        orderCommand.CommandText = """
            INSERT INTO held_orders (customer_name, held_at, cashier_name, cashier_email) 
            VALUES (@customerName, @heldAt, @cashierName, @cashierEmail);
            SELECT last_insert_rowid();
            """;
        orderCommand.Parameters.AddWithValue("@customerName",
            string.IsNullOrWhiteSpace(customerName) ? "Walking Customer" : customerName.Trim());
        orderCommand.Parameters.AddWithValue("@heldAt", DateTime.Now.ToString("O"));
        orderCommand.Parameters.AddWithValue("@cashierName", cashierName);
        orderCommand.Parameters.AddWithValue("@cashierEmail", cashierEmail);
        var orderId = (long)orderCommand.ExecuteScalar()!;

        foreach (var item in items)
        {
            using var itemCommand = connection.CreateCommand();
            itemCommand.Transaction = transaction;
            itemCommand.CommandText = """
                INSERT INTO held_order_items
                    (held_order_id, product_id, product_name, category, sku, barcode, unit_price, stock, quantity)
                VALUES (@orderId, @productId, @name, @category, @sku, @barcode, @price, @stock, @quantity);
                """;
            itemCommand.Parameters.AddWithValue("@orderId", orderId);
            itemCommand.Parameters.AddWithValue("@productId", item.Product.Id);
            itemCommand.Parameters.AddWithValue("@name", item.Product.Name);
            itemCommand.Parameters.AddWithValue("@category", item.Product.Category);
            itemCommand.Parameters.AddWithValue("@sku", item.Product.Sku);
            itemCommand.Parameters.AddWithValue("@barcode", item.Product.Barcode);
            itemCommand.Parameters.AddWithValue("@price", item.Product.Price);
            itemCommand.Parameters.AddWithValue("@stock", item.Product.Stock);
            itemCommand.Parameters.AddWithValue("@quantity", item.Quantity);
            itemCommand.ExecuteNonQuery();
        }

        transaction.Commit();
        return orderId;
    }

    public HeldOrderDetails ResumeHeldOrder(long orderId)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT h.customer_name, i.product_id, i.product_name, i.category, i.sku,
                   i.barcode, i.unit_price, i.stock, i.quantity
            FROM held_orders h
            JOIN held_order_items i ON i.held_order_id = h.id
            WHERE h.id = @id
            ORDER BY i.id;
            """;
        command.Parameters.AddWithValue("@id", orderId);
        using var reader = command.ExecuteReader();
        var items = new List<CartItem>();
        var customerName = "Walking Customer";
        while (reader.Read())
        {
            customerName = reader.GetString(0);
            items.Add(new CartItem
            {
                Product = new Product
                {
                    Id = reader.GetInt64(1),
                    Name = reader.GetString(2),
                    Category = reader.GetString(3),
                    Sku = reader.GetString(4),
                    Barcode = reader.GetString(5),
                    Price = reader.GetDecimal(6),
                    Stock = reader.GetInt32(7)
                },
                Quantity = reader.GetInt32(8)
            });
        }
        reader.Close();
        if (items.Count == 0)
            throw new InvalidOperationException("The held order no longer exists or has no items.");

        using var deleteCommand = connection.CreateCommand();
        deleteCommand.Transaction = transaction;
        deleteCommand.CommandText = "DELETE FROM held_orders WHERE id = @id;";
        deleteCommand.Parameters.AddWithValue("@id", orderId);
        if (deleteCommand.ExecuteNonQuery() != 1)
            throw new InvalidOperationException("The held order could not be resumed.");

        transaction.Commit();
        return new HeldOrderDetails { CustomerName = customerName, Items = items };
    }

    public void DeleteHeldOrder(long orderId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM held_orders WHERE id = @id;";
        command.Parameters.AddWithValue("@id", orderId);
        if (command.ExecuteNonQuery() != 1)
            throw new InvalidOperationException("The held order no longer exists.");
    }

    public ReceiptDetails? GetReceipt(long saleId)
    {
        using var connection = OpenConnection();
        using var saleCommand = connection.CreateCommand();
        saleCommand.CommandText = """
            SELECT id, receipt_number, created_at, customer_name, payment_method, total, amount_paid
            FROM sales
            WHERE id = @id;
            """;
        saleCommand.Parameters.AddWithValue("@id", saleId);
        using var saleReader = saleCommand.ExecuteReader();
        if (!saleReader.Read())
            return null;

        var sale = new SaleSummary
        {
            Id = saleReader.GetInt64(0),
            ReceiptNumber = saleReader.GetString(1),
            CreatedAt = DateTime.Parse(saleReader.GetString(2)),
            CustomerName = saleReader.GetString(3),
            PaymentMethod = saleReader.GetString(4),
            Total = saleReader.GetDecimal(5),
            ItemCount = 0
        };
        var amountPaid = saleReader.GetDecimal(6);
        saleReader.Close();

        using var itemsCommand = connection.CreateCommand();
        itemsCommand.CommandText = """
            SELECT product_name, sku, unit_price, quantity
            FROM sale_items
            WHERE sale_id = @id
            ORDER BY id;
            """;
        itemsCommand.Parameters.AddWithValue("@id", saleId);
        using var itemsReader = itemsCommand.ExecuteReader();
        var items = new List<ReceiptLine>();
        var itemCount = 0;
        while (itemsReader.Read())
        {
            var quantity = itemsReader.GetInt32(3);
            itemCount++;
            items.Add(new ReceiptLine
            {
                ProductName = itemsReader.GetString(0),
                Sku = itemsReader.GetString(1),
                UnitPrice = itemsReader.GetDecimal(2),
                Quantity = quantity
            });
        }

        sale = new SaleSummary
        {
            Id = sale.Id,
            ReceiptNumber = sale.ReceiptNumber,
            CreatedAt = sale.CreatedAt,
            CustomerName = sale.CustomerName,
            PaymentMethod = sale.PaymentMethod,
            Total = sale.Total,
            ItemCount = itemCount
        };
        return new ReceiptDetails { Sale = sale, Items = items, AmountPaid = amountPaid };
    }

    public RefundOrderSummary? GetRefundOrderSummary(long saleId)
    {
        using var connection = OpenConnection();
        using var saleCommand = connection.CreateCommand();
        saleCommand.CommandText = """
            SELECT id, receipt_number, created_at, customer_name, payment_method, total,
                   COALESCE(refunded_amount, 0), COALESCE(customer_phone, 'No phone number')
            FROM sales
            WHERE id = @id;
            """;
        saleCommand.Parameters.AddWithValue("@id", saleId);
        using var saleReader = saleCommand.ExecuteReader();
        if (!saleReader.Read())
            return null;

        var orderId = saleReader.GetInt64(0);
        var receiptNumber = saleReader.GetString(1);
        var createdAt = DateTime.Parse(saleReader.GetString(2));
        var customerName = saleReader.GetString(3);
        var paymentMethod = saleReader.GetString(4);
        var totalAmount = saleReader.GetDecimal(5);
        var alreadyRefunded = saleReader.GetDecimal(6);
        var customerPhone = saleReader.GetString(7);
        saleReader.Close();

        if (customerPhone == "No phone number" || string.IsNullOrWhiteSpace(customerPhone))
        {
            using var custCommand = connection.CreateCommand();
            custCommand.CommandText = "SELECT phone FROM customers WHERE name = @name LIMIT 1;";
            custCommand.Parameters.AddWithValue("@name", customerName);
            var phoneObj = custCommand.ExecuteScalar();
            if (phoneObj is string p && !string.IsNullOrWhiteSpace(p))
                customerPhone = p;
        }

        using var itemsCommand = connection.CreateCommand();
        itemsCommand.CommandText = """
            SELECT si.id, si.product_id, si.product_name, si.sku, si.unit_price, si.quantity,
                   COALESCE(si.refunded_quantity, 0), p.image_url
            FROM sale_items si
            LEFT JOIN products p ON p.id = si.product_id
            WHERE si.sale_id = @id
            ORDER BY si.id;
            """;
        itemsCommand.Parameters.AddWithValue("@id", saleId);
        using var itemsReader = itemsCommand.ExecuteReader();
        var items = new List<RefundLineItem>();
        int totalItemCount = 0;
        while (itemsReader.Read())
        {
            var origQty = itemsReader.GetInt32(5);
            totalItemCount += origQty;
            items.Add(new RefundLineItem
            {
                OrderItemId = itemsReader.GetInt64(0),
                ProductId = itemsReader.GetInt64(1),
                ProductName = itemsReader.GetString(2),
                Sku = itemsReader.GetString(3),
                UnitPrice = itemsReader.GetDecimal(4),
                OriginalQty = origQty,
                RefundedQty = itemsReader.GetInt32(6),
                ImageUrl = itemsReader.IsDBNull(7) ? null : itemsReader.GetString(7)
            });
        }

        return new RefundOrderSummary
        {
            OrderId = orderId,
            ReceiptNumber = receiptNumber,
            CreatedAt = createdAt,
            CustomerName = customerName,
            CustomerPhone = customerPhone,
            PaymentMethod = paymentMethod,
            TotalAmount = totalAmount,
            AlreadyRefundedAmount = alreadyRefunded,
            TotalItemCount = totalItemCount,
            Items = items
        };
    }

    public (bool Success, string Message, decimal TotalRefunded) ProcessRefund(
        long orderId,
        List<RefundLineItem> refundLines,
        string reason,
        string notes,
        string cashierName)
    {
        var activeRefunds = refundLines.Where(l => l.RefundQty > 0).ToList();
        if (activeRefunds.Count == 0)
            return (false, "Please select at least one item and quantity to refund.", 0);

        if (string.IsNullOrWhiteSpace(reason) || reason == "Select a Reason")
            return (false, "Please select a refund reason.", 0);

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            using var checkCommand = connection.CreateCommand();
            checkCommand.Transaction = transaction;
            checkCommand.CommandText = "SELECT payment_method, total, COALESCE(refunded_amount, 0) FROM sales WHERE id = @id;";
            checkCommand.Parameters.AddWithValue("@id", orderId);
            using var checkReader = checkCommand.ExecuteReader();
            if (!checkReader.Read())
                return (false, "Order not found.", 0);

            var paymentMethod = checkReader.GetString(0);
            var orderTotal = checkReader.GetDecimal(1);
            var alreadyRefunded = checkReader.GetDecimal(2);
            checkReader.Close();

            decimal totalRefundAmount = 0m;
            var nowStr = DateTime.Now.ToString("O");

            foreach (var line in activeRefunds)
            {
                var lineAmount = line.UnitPrice * line.RefundQty;
                totalRefundAmount += lineAmount;

                using var itemUpdate = connection.CreateCommand();
                itemUpdate.Transaction = transaction;
                itemUpdate.CommandText = """
                    UPDATE sale_items
                    SET refunded_quantity = COALESCE(refunded_quantity, 0) + @qty
                    WHERE id = @id;
                    """;
                itemUpdate.Parameters.AddWithValue("@qty", line.RefundQty);
                itemUpdate.Parameters.AddWithValue("@id", line.OrderItemId);
                itemUpdate.ExecuteNonQuery();

                using var stockUpdate = connection.CreateCommand();
                stockUpdate.Transaction = transaction;
                stockUpdate.CommandText = """
                    UPDATE products
                    SET stock = stock + @qty
                    WHERE id = @productId;
                    """;
                stockUpdate.Parameters.AddWithValue("@qty", line.RefundQty);
                stockUpdate.Parameters.AddWithValue("@productId", line.ProductId);
                stockUpdate.ExecuteNonQuery();

                using var rfTxCmd = connection.CreateCommand();
                rfTxCmd.Transaction = transaction;
                rfTxCmd.CommandText = """
                    INSERT INTO refund_transactions
                        (order_id, order_item_id, product_id, product_name, sku, refunded_qty, refund_amount, refund_reason, refund_notes, payment_method, created_at, cashier_name)
                    VALUES
                        (@orderId, @orderItemId, @productId, @name, @sku, @qty, @amount, @reason, @notes, @method, @created, @cashier);
                    """;
                rfTxCmd.Parameters.AddWithValue("@orderId", orderId);
                rfTxCmd.Parameters.AddWithValue("@orderItemId", line.OrderItemId);
                rfTxCmd.Parameters.AddWithValue("@productId", line.ProductId);
                rfTxCmd.Parameters.AddWithValue("@name", line.ProductName);
                rfTxCmd.Parameters.AddWithValue("@sku", line.Sku);
                rfTxCmd.Parameters.AddWithValue("@qty", line.RefundQty);
                rfTxCmd.Parameters.AddWithValue("@amount", lineAmount);
                rfTxCmd.Parameters.AddWithValue("@reason", reason);
                rfTxCmd.Parameters.AddWithValue("@notes", notes ?? "");
                rfTxCmd.Parameters.AddWithValue("@method", paymentMethod);
                rfTxCmd.Parameters.AddWithValue("@created", nowStr);
                rfTxCmd.Parameters.AddWithValue("@cashier", string.IsNullOrWhiteSpace(cashierName) ? "Praveen Balaji" : cashierName);
                rfTxCmd.ExecuteNonQuery();
            }

            using var countRemainingCmd = connection.CreateCommand();
            countRemainingCmd.Transaction = transaction;
            countRemainingCmd.CommandText = """
                SELECT COALESCE(SUM(quantity - refunded_quantity), 0)
                FROM sale_items
                WHERE sale_id = @orderId;
                """;
            countRemainingCmd.Parameters.AddWithValue("@orderId", orderId);
            var remainingItems = Convert.ToInt32(countRemainingCmd.ExecuteScalar());

            var newStatus = remainingItems <= 0 ? "Refunded" : "Partially Refunded";

            using var saleUpdate = connection.CreateCommand();
            saleUpdate.Transaction = transaction;
            saleUpdate.CommandText = """
                UPDATE sales
                SET refunded_amount = COALESCE(refunded_amount, 0) + @refundAmount,
                    order_status = @status
                WHERE id = @orderId;
                """;
            saleUpdate.Parameters.AddWithValue("@refundAmount", totalRefundAmount);
            saleUpdate.Parameters.AddWithValue("@status", newStatus);
            saleUpdate.Parameters.AddWithValue("@orderId", orderId);
            saleUpdate.ExecuteNonQuery();

            transaction.Commit();
            return (true, "Refund processed successfully.", totalRefundAmount);
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            return (false, $"Failed to process refund: {ex.Message}", 0);
        }
    }

    public ShiftSettlement? GetShiftSettlement(long shiftId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT p.id, p.business_date, p.opened_at, p.opening_cash,
                   (SELECT COUNT(*) FROM sales s WHERE s.shift_id = p.id),
                   (SELECT COUNT(i.id)
                    FROM sales s JOIN sale_items i ON i.sale_id = s.id WHERE s.shift_id = p.id),
                   (SELECT COALESCE(SUM(s.total), 0) FROM sales s WHERE s.shift_id = p.id),
                   (SELECT COALESCE(SUM(s.total), 0) FROM sales s
                    WHERE s.shift_id = p.id AND lower(s.payment_method) = 'cash')
            FROM pos_shifts p
            WHERE p.id = @id AND p.closed_at IS NULL
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@id", shiftId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
            return null;

        var settlement = new ShiftSettlement
        {
            ShiftId = reader.GetInt64(0),
            BusinessDate = DateTime.Parse(reader.GetString(1)),
            OpenedAt = DateTime.Parse(reader.GetString(2)),
            OpeningCash = reader.GetDecimal(3),
            OrderCount = reader.GetInt32(4),
            ItemCount = reader.GetInt32(5),
            TotalSales = reader.GetDecimal(6),
            CashSales = reader.GetDecimal(7)
        };
        reader.Close();

        using var paymentCommand = connection.CreateCommand();
        paymentCommand.CommandText = """
            SELECT payment_method, COUNT(*), SUM(total)
            FROM sales WHERE shift_id = @id
            GROUP BY payment_method ORDER BY payment_method;
            """;
        paymentCommand.Parameters.AddWithValue("@id", shiftId);
        using var paymentReader = paymentCommand.ExecuteReader();
        var payments = new List<PaymentMethodSummary>();
        while (paymentReader.Read())
        {
            payments.Add(new PaymentMethodSummary
            {
                PaymentMethod = paymentReader.GetString(0),
                OrderCount = paymentReader.GetInt32(1),
                Amount = paymentReader.GetDecimal(2)
            });
        }

        return new ShiftSettlement
        {
            ShiftId = settlement.ShiftId,
            BusinessDate = settlement.BusinessDate,
            OpenedAt = settlement.OpenedAt,
            OpeningCash = settlement.OpeningCash,
            OrderCount = settlement.OrderCount,
            ItemCount = settlement.ItemCount,
            TotalSales = settlement.TotalSales,
            CashSales = settlement.CashSales,
            Payments = payments
        };
    }

    public void CloseShift(
        ShiftSettlement settlement,
        decimal countedCash,
        string differenceReason,
        string notes,
        IReadOnlyCollection<CashDenominationCount> denominations)
    {
        if (countedCash < 0)
            throw new ArgumentOutOfRangeException(nameof(countedCash), "Counted cash cannot be negative.");
        if (Math.Abs(countedCash - settlement.ExpectedCash) >= 0.01m &&
            string.IsNullOrWhiteSpace(differenceReason))
            throw new InvalidOperationException("Select a reason for the cash difference.");

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        decimal currentExpectedCash;
        using (var expectedCommand = connection.CreateCommand())
        {
            expectedCommand.Transaction = transaction;
            expectedCommand.CommandText = """
                SELECT opening_cash + COALESCE((
                    SELECT SUM(total) FROM sales
                    WHERE shift_id = pos_shifts.id AND lower(payment_method) = 'cash'
                ), 0)
                FROM pos_shifts
                WHERE id = @id AND closed_at IS NULL;
                """;
            expectedCommand.Parameters.AddWithValue("@id", settlement.ShiftId);
            if (expectedCommand.ExecuteScalar() is not object expectedValue)
                throw new InvalidOperationException("This shift has already been closed.");
            currentExpectedCash = Convert.ToDecimal(expectedValue);
        }
        if (currentExpectedCash != settlement.ExpectedCash)
            throw new InvalidOperationException("Sales changed while this settlement was open. Reopen settlement and count again.");
        if (Math.Abs(countedCash - currentExpectedCash) >= 0.01m &&
            string.IsNullOrWhiteSpace(differenceReason))
            throw new InvalidOperationException("Select a reason for the cash difference.");

        using var updateCommand = connection.CreateCommand();
        updateCommand.Transaction = transaction;
        updateCommand.CommandText = """
            UPDATE pos_shifts
            SET closed_at = @closedAt, counted_cash = @counted,
                cash_difference = @difference, difference_reason = @reason, notes = @notes
            WHERE id = @id AND closed_at IS NULL;
            """;
        updateCommand.Parameters.AddWithValue("@closedAt", DateTime.Now.ToString("O"));
        updateCommand.Parameters.AddWithValue("@counted", countedCash);
        updateCommand.Parameters.AddWithValue("@difference", countedCash - currentExpectedCash);
        updateCommand.Parameters.AddWithValue("@reason", differenceReason);
        updateCommand.Parameters.AddWithValue("@notes", notes);
        updateCommand.Parameters.AddWithValue("@id", settlement.ShiftId);
        if (updateCommand.ExecuteNonQuery() != 1)
            throw new InvalidOperationException("This shift has already been closed.");

        foreach (var denomination in denominations)
        {
            using var countCommand = connection.CreateCommand();
            countCommand.Transaction = transaction;
            countCommand.CommandText = """
                INSERT INTO shift_cash_counts (shift_id, denomination, quantity)
                VALUES (@shiftId, @denomination, @quantity);
                """;
            countCommand.Parameters.AddWithValue("@shiftId", settlement.ShiftId);
            countCommand.Parameters.AddWithValue("@denomination", denomination.Denomination);
            countCommand.Parameters.AddWithValue("@quantity", denomination.Quantity);
            countCommand.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public string SaveSale(
        IReadOnlyCollection<CartItem> items,
        string paymentMethod,
        decimal amountPaid,
        string customerName,
        long shiftId,
        string cashierName = "Praveen Balaji",
        string cashierEmail = "praveenbalajimindzmap@gmail.com")
    {
        if (items.Count == 0)
            throw new InvalidOperationException("The cart is empty.");

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        using (var shiftCommand = connection.CreateCommand())
        {
            shiftCommand.Transaction = transaction;
            shiftCommand.CommandText = "SELECT 1 FROM pos_shifts WHERE id = @id AND closed_at IS NULL;";
            shiftCommand.Parameters.AddWithValue("@id", shiftId);
            if (shiftCommand.ExecuteScalar() is null)
                throw new InvalidOperationException("Open a cash shift before completing a sale.");
        }
        var total = items.Sum(item => item.LineTotal);

        foreach (var item in items)
        {
            using var stockCommand = connection.CreateCommand();
            stockCommand.Transaction = transaction;
            stockCommand.CommandText = """
                UPDATE products SET stock = stock - @quantity
                WHERE id = @id AND is_active = 1 AND stock >= @quantity;
                """;
            stockCommand.Parameters.AddWithValue("@id", item.Product.Id);
            stockCommand.Parameters.AddWithValue("@quantity", item.Quantity);
            if (stockCommand.ExecuteNonQuery() != 1)
                throw new InvalidOperationException($"Not enough stock for {item.Product.Name}.");
        }

        var receiptNumber = $"POS-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}";
        using (var saleCommand = connection.CreateCommand())
        {
            saleCommand.Transaction = transaction;
            saleCommand.CommandText = """
                INSERT INTO sales
                    (receipt_number, created_at, customer_name, payment_method, total, amount_paid, shift_id, cashier_name, cashier_email)
                VALUES (@receipt, @created, @customerName, @method, @total, @paid, @shiftId, @cashierName, @cashierEmail);
                SELECT last_insert_rowid();
                """;
            saleCommand.Parameters.AddWithValue("@receipt", receiptNumber);
            saleCommand.Parameters.AddWithValue("@created", DateTime.Now.ToString("O"));
            saleCommand.Parameters.AddWithValue("@customerName",
                string.IsNullOrWhiteSpace(customerName) ? "Walking Customer" : customerName.Trim());
            saleCommand.Parameters.AddWithValue("@method", paymentMethod);
            saleCommand.Parameters.AddWithValue("@total", total);
            saleCommand.Parameters.AddWithValue("@paid", amountPaid);
            saleCommand.Parameters.AddWithValue("@shiftId", shiftId);
            saleCommand.Parameters.AddWithValue("@cashierName", cashierName);
            saleCommand.Parameters.AddWithValue("@cashierEmail", cashierEmail);
            var saleId = (long)saleCommand.ExecuteScalar()!;

            foreach (var item in items)
            {
                using var itemCommand = connection.CreateCommand();
                itemCommand.Transaction = transaction;
                itemCommand.CommandText = """
                    INSERT INTO sale_items (sale_id, product_id, product_name, sku, unit_price, quantity)
                    VALUES (@saleId, @productId, @name, @sku, @price, @quantity);
                    """;
                itemCommand.Parameters.AddWithValue("@saleId", saleId);
                itemCommand.Parameters.AddWithValue("@productId", item.Product.Id);
                itemCommand.Parameters.AddWithValue("@name", item.Product.Name);
                itemCommand.Parameters.AddWithValue("@sku", item.Product.Sku);
                itemCommand.Parameters.AddWithValue("@price", item.Product.Price);
                itemCommand.Parameters.AddWithValue("@quantity", item.Quantity);
                itemCommand.ExecuteNonQuery();
            }
        }

        transaction.Commit();
        return receiptNumber;
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys = ON;";
        command.ExecuteNonQuery();
        return connection;
    }

    private void Initialize()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA foreign_keys = ON;
            CREATE TABLE IF NOT EXISTS products (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL,
                category TEXT NOT NULL,
                sku TEXT NOT NULL UNIQUE,
                barcode TEXT NOT NULL DEFAULT '',
                price NUMERIC NOT NULL CHECK (price >= 0),
                stock INTEGER NOT NULL CHECK (stock >= 0),
                source_id INTEGER NULL,
                source_stock INTEGER NULL,
                is_sample INTEGER NOT NULL DEFAULT 0,
                is_active INTEGER NOT NULL DEFAULT 1
            );
            CREATE TABLE IF NOT EXISTS pos_shifts (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                business_date TEXT NOT NULL,
                opened_at TEXT NOT NULL,
                opening_cash NUMERIC NOT NULL CHECK (opening_cash > 0),
                closed_at TEXT NULL,
                counted_cash NUMERIC NULL,
                cash_difference NUMERIC NULL,
                difference_reason TEXT NOT NULL DEFAULT '',
                notes TEXT NOT NULL DEFAULT ''
            );
            CREATE TABLE IF NOT EXISTS held_orders (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                customer_name TEXT NOT NULL,
                held_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS held_order_items (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                held_order_id INTEGER NOT NULL REFERENCES held_orders(id) ON DELETE CASCADE,
                product_id INTEGER NOT NULL,
                product_name TEXT NOT NULL,
                category TEXT NOT NULL,
                sku TEXT NOT NULL,
                barcode TEXT NOT NULL DEFAULT '',
                unit_price NUMERIC NOT NULL,
                stock INTEGER NOT NULL,
                quantity INTEGER NOT NULL CHECK (quantity > 0)
            );
            CREATE TABLE IF NOT EXISTS sales (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                receipt_number TEXT NOT NULL UNIQUE,
                created_at TEXT NOT NULL,
                customer_name TEXT NOT NULL DEFAULT 'Walking Customer',
                payment_method TEXT NOT NULL,
                total NUMERIC NOT NULL CHECK (total >= 0),
                amount_paid NUMERIC NOT NULL CHECK (amount_paid >= total),
                shift_id INTEGER NULL REFERENCES pos_shifts(id)
            );
            CREATE TABLE IF NOT EXISTS sale_items (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                sale_id INTEGER NOT NULL REFERENCES sales(id),
                product_id INTEGER NOT NULL REFERENCES products(id),
                product_name TEXT NOT NULL,
                sku TEXT NOT NULL,
                unit_price NUMERIC NOT NULL,
                quantity INTEGER NOT NULL CHECK (quantity > 0)
            );
            CREATE TABLE IF NOT EXISTS shift_cash_counts (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                shift_id INTEGER NOT NULL REFERENCES pos_shifts(id) ON DELETE CASCADE,
                denomination NUMERIC NOT NULL,
                quantity INTEGER NOT NULL CHECK (quantity >= 0)
            );
            CREATE TABLE IF NOT EXISTS customers (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL,
                phone TEXT NOT NULL DEFAULT '',
                email TEXT NOT NULL DEFAULT '',
                address TEXT NOT NULL DEFAULT '',
                created_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS categories (
                id INTEGER PRIMARY KEY,
                name TEXT NOT NULL UNIQUE,
                slug TEXT NOT NULL DEFAULT ''
            );
            CREATE TABLE IF NOT EXISTS admins (
                id INTEGER PRIMARY KEY,
                name TEXT NOT NULL,
                email TEXT NOT NULL UNIQUE,
                phone TEXT NOT NULL DEFAULT '',
                role_id INTEGER NOT NULL DEFAULT 6,
                role_name TEXT NOT NULL DEFAULT 'Cashier',
                password_hash TEXT NOT NULL DEFAULT '',
                is_active INTEGER NOT NULL DEFAULT 1
            );
            CREATE TABLE IF NOT EXISTS refund_transactions (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                order_id INTEGER NOT NULL REFERENCES sales(id),
                order_item_id INTEGER NOT NULL REFERENCES sale_items(id),
                product_id INTEGER NOT NULL REFERENCES products(id),
                product_name TEXT NOT NULL,
                sku TEXT NOT NULL,
                refunded_qty INTEGER NOT NULL,
                refund_amount NUMERIC NOT NULL,
                refund_reason TEXT NOT NULL,
                refund_notes TEXT NOT NULL DEFAULT '',
                payment_method TEXT NOT NULL DEFAULT 'Cash',
                created_at TEXT NOT NULL,
                cashier_name TEXT NOT NULL DEFAULT 'Praveen Balaji'
            );
            """;
        command.ExecuteNonQuery();
        EnsureColumn(connection, "admins", "password_hash", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(connection, "sales", "customer_name", "TEXT NOT NULL DEFAULT 'Walking Customer'");
        EnsureColumn(connection, "sales", "customer_phone", "TEXT NOT NULL DEFAULT 'No phone number'");
        EnsureColumn(connection, "sales", "order_status", "TEXT NOT NULL DEFAULT 'Delivered'");
        EnsureColumn(connection, "sales", "refunded_amount", "NUMERIC NOT NULL DEFAULT 0");
        EnsureColumn(connection, "sale_items", "refunded_quantity", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(connection, "sales", "shift_id", "INTEGER NULL REFERENCES pos_shifts(id)");
        EnsureColumn(connection, "sales", "cashier_name", "TEXT NOT NULL DEFAULT 'Praveen Balaji'");
        EnsureColumn(connection, "sales", "cashier_email", "TEXT NOT NULL DEFAULT 'praveenbalajimindzmap@gmail.com'");
        EnsureColumn(connection, "pos_shifts", "cashier_name", "TEXT NOT NULL DEFAULT 'Praveen Balaji'");
        EnsureColumn(connection, "pos_shifts", "cashier_email", "TEXT NOT NULL DEFAULT 'praveenbalajimindzmap@gmail.com'");
        EnsureColumn(connection, "held_orders", "cashier_name", "TEXT NOT NULL DEFAULT 'Praveen Balaji'");
        EnsureColumn(connection, "held_orders", "cashier_email", "TEXT NOT NULL DEFAULT 'praveenbalajimindzmap@gmail.com'");
        EnsureColumn(connection, "products", "source_id", "INTEGER NULL");
        EnsureColumn(connection, "products", "source_stock", "INTEGER NULL");
        EnsureColumn(connection, "products", "is_sample", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(connection, "products", "image_url", "TEXT NULL");
        EnsureColumn(connection, "products", "original_price", "NUMERIC NULL");
        EnsureColumn(connection, "products", "discount", "NUMERIC NOT NULL DEFAULT 100");
        EnsureColumn(connection, "products", "discount_type", "TEXT NOT NULL DEFAULT 'flat'");
        using (var indexCommand = connection.CreateCommand())
        {
            indexCommand.CommandText = """
                CREATE UNIQUE INDEX IF NOT EXISTS ux_products_source_id
                ON products(source_id) WHERE source_id IS NOT NULL;
                """;
            indexCommand.ExecuteNonQuery();
        }
        RemoveBundledSampleProducts(connection);
    }

    public void SeedCategory(int id, string name, string slug)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT OR REPLACE INTO categories (id, name, slug)
            VALUES (@id, @name, @slug);
            """;
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@name", name.Trim());
        command.Parameters.AddWithValue("@slug", slug.Trim());
        command.ExecuteNonQuery();
    }

    public void SeedAdmin(int id, string name, string email, string phone, int roleId, string roleName, string passwordHash = "")
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT OR REPLACE INTO admins (id, name, email, phone, role_id, role_name, password_hash, is_active)
            VALUES (@id, @name, @email, @phone, @roleId, @roleName, @passwordHash, 1);
            """;
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@name", name.Trim());
        command.Parameters.AddWithValue("@email", email.Trim());
        command.Parameters.AddWithValue("@phone", phone.Trim());
        command.Parameters.AddWithValue("@roleId", roleId);
        command.Parameters.AddWithValue("@roleName", roleName.Trim());
        command.Parameters.AddWithValue("@passwordHash", passwordHash.Trim());
        command.ExecuteNonQuery();
    }

    public (bool Success, string Message, string Name, string Email, string RoleName) ValidateAdminLogin(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email))
            return (false, "Please enter an email address.", "", "", "");

        if (string.IsNullOrWhiteSpace(password))
            return (false, "Please enter your password.", "", "", "");

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name, email, role_name, password_hash FROM admins WHERE email = @email COLLATE NOCASE AND is_active = 1;";
        command.Parameters.AddWithValue("@email", email.Trim());
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return (false, $"No active admin account found for '{email}'.", "", "", "");
        }

        var name = reader.GetString(1);
        var dbEmail = reader.GetString(2);
        var roleName = reader.GetString(3);
        var hash = reader.IsDBNull(4) ? "" : reader.GetString(4);

        if (string.IsNullOrWhiteSpace(hash))
        {
            return (true, "", name, dbEmail, roleName);
        }

        bool passwordMatches = false;
        try
        {
            passwordMatches = BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch
        {
            passwordMatches = string.Equals(password, hash, StringComparison.Ordinal);
        }

        if (!passwordMatches)
        {
            return (false, "Incorrect password. Please enter the valid credentials for this admin account.", name, dbEmail, roleName);
        }

        return (true, "", name, dbEmail, roleName);
    }

    public Customer AddCustomer(string name, string phone = "", string email = "", string address = "")
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Customer name is required.", nameof(name));

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO customers (name, phone, email, address, created_at)
            VALUES (@name, @phone, @email, @address, @created);
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("@name", name.Trim());
        command.Parameters.AddWithValue("@phone", phone.Trim());
        command.Parameters.AddWithValue("@email", email.Trim());
        command.Parameters.AddWithValue("@address", address.Trim());
        var now = DateTime.Now;
        command.Parameters.AddWithValue("@created", now.ToString("O"));
        var id = (long)command.ExecuteScalar()!;
        return new Customer
        {
            Id = id,
            Name = name.Trim(),
            Phone = phone.Trim(),
            Email = email.Trim(),
            Address = address.Trim(),
            CreatedAt = now
        };
    }

    public List<Customer> GetCustomers(string? search = null)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, name, phone, email, address, created_at
            FROM customers
            WHERE (@search IS NULL OR name LIKE @pattern OR phone LIKE @pattern OR email LIKE @pattern)
            ORDER BY id DESC;
            """;
        command.Parameters.AddWithValue("@search", string.IsNullOrWhiteSpace(search) ? DBNull.Value : search);
        command.Parameters.AddWithValue("@pattern", $"%{search?.Trim()}%");
        using var reader = command.ExecuteReader();
        var list = new List<Customer>();
        while (reader.Read())
        {
            list.Add(new Customer
            {
                Id = reader.GetInt64(0),
                Name = reader.GetString(1),
                Phone = reader.GetString(2),
                Email = reader.GetString(3),
                Address = reader.GetString(4),
                CreatedAt = DateTime.Parse(reader.GetString(5))
            });
        }
        return list;
    }

    private static void EnsureColumn(SqliteConnection connection, string tableName, string columnName, string declaration)
    {
        using var columnsCommand = connection.CreateCommand();
        columnsCommand.CommandText = $"PRAGMA table_info({tableName});";
        using var reader = columnsCommand.ExecuteReader();
        while (reader.Read())
        {
            if (reader.GetString(1).Equals(columnName, StringComparison.OrdinalIgnoreCase))
                return;
        }

        reader.Close();
        using var alterCommand = connection.CreateCommand();
        alterCommand.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {declaration};";
        alterCommand.ExecuteNonQuery();
    }

    private static void RemoveBundledSampleProducts(SqliteConnection connection)
    {
        using var columnCommand = connection.CreateCommand();
        columnCommand.CommandText = "PRAGMA table_info(products);";
        using var reader = columnCommand.ExecuteReader();
        var hasSampleColumn = false;
        while (reader.Read())
        {
            if (reader.GetString(1) == "is_sample")
            {
                hasSampleColumn = true;
                break;
            }
        }
        reader.Close();

        if (!hasSampleColumn)
        {
            using var alterCommand = connection.CreateCommand();
            alterCommand.CommandText =
                "ALTER TABLE products ADD COLUMN is_sample INTEGER NOT NULL DEFAULT 0;";
            alterCommand.ExecuteNonQuery();
        }

        using var cleanupCommand = connection.CreateCommand();
        cleanupCommand.CommandText = """
            DELETE FROM products
            WHERE ((sku = 'CL-001' AND name = 'Classic Kurti')
                OR (sku = 'CL-002' AND name = 'Printed Cotton Dress')
                OR (sku = 'AC-001' AND name = 'Everyday Handbag')
                OR (sku = 'JW-001' AND name = 'Pearl Drop Earrings')
                OR (sku = 'AC-002' AND name = 'Floral Scarf')
                OR (sku = 'CL-003' AND name = 'Linen Co-ord Set')
                OR (sku = 'JW-002' AND name = 'Gold Tone Bracelet')
                OR (sku = 'AC-003' AND name = 'Embroidered Clutch'))
              AND NOT EXISTS (
                  SELECT 1 FROM sale_items WHERE sale_items.product_id = products.id
              );

            UPDATE products
            SET sku = 'DEMO-ARCHIVED-' || sku, is_sample = 1, is_active = 0
            WHERE ((sku = 'CL-001' AND name = 'Classic Kurti')
                OR (sku = 'CL-002' AND name = 'Printed Cotton Dress')
                OR (sku = 'AC-001' AND name = 'Everyday Handbag')
                OR (sku = 'JW-001' AND name = 'Pearl Drop Earrings')
                OR (sku = 'AC-002' AND name = 'Floral Scarf')
                OR (sku = 'CL-003' AND name = 'Linen Co-ord Set')
                OR (sku = 'JW-002' AND name = 'Gold Tone Bracelet')
                OR (sku = 'AC-003' AND name = 'Embroidered Clutch'));
            """;
        cleanupCommand.ExecuteNonQuery();
    }

    private static void EnsureProductColumn(SqliteConnection connection, string columnName, string declaration)
    {
        using var columnsCommand = connection.CreateCommand();
        columnsCommand.CommandText = "PRAGMA table_info(products);";
        using var reader = columnsCommand.ExecuteReader();
        while (reader.Read())
        {
            if (reader.GetString(1) == columnName)
                return;
        }

        reader.Close();
        using var alterCommand = connection.CreateCommand();
        alterCommand.CommandText = $"ALTER TABLE products ADD COLUMN {columnName} {declaration};";
        alterCommand.ExecuteNonQuery();
    }

    private static void EnsureSaleColumn(SqliteConnection connection, string columnName, string declaration)
    {
        using var columnsCommand = connection.CreateCommand();
        columnsCommand.CommandText = "PRAGMA table_info(sales);";
        using var reader = columnsCommand.ExecuteReader();
        while (reader.Read())
        {
            if (reader.GetString(1) == columnName)
                return;
        }

        reader.Close();
        using var alterCommand = connection.CreateCommand();
        alterCommand.CommandText = $"ALTER TABLE sales ADD COLUMN {columnName} {declaration};";
        alterCommand.ExecuteNonQuery();
    }
}
