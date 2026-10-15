using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Microsoft.VisualBasic.FileIO;
using HyperPOS.Desktop.Models;

namespace HyperPOS.Desktop.Data;

public sealed class ProductCsvImporter
{
    private static readonly string[] SerialAliases = ["sl", "serial", "serialnumber", "sno"];
    private static readonly string[][] HeaderAliases =
    [
        ["name", "productname", "title"],
        ["sku", "productcode", "code", "itemcode"],
        ["category", "categoryname"],
        ["barcode", "barcodevalue", "ean", "upc"],
        ["price", "sellingprice", "saleprice", "unitprice", "regularprice", "productunitprice"],
        ["stock", "quantity", "qty", "currentstock", "stockquantity", "currentstockamount"]
    ];

    public ProductImportResult Import(string filePath, PosDatabase database)
    {
        var products = Parse(filePath);
        return database.ImportLocalProducts(products);
    }

    public IReadOnlyList<ImportedProduct> Parse(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!File.Exists(filePath))
            throw new FileNotFoundException("The selected CSV file does not exist.", filePath);

        var extension = Path.GetExtension(filePath);
        if (!extension.Equals(".csv", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Choose a CSV (.csv) or Excel workbook (.xlsx) product report.");

        var rows = extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase)
            ? ReadExcelRows(filePath)
            : ReadCsvRows(filePath);
        if (rows.Count == 0)
            throw new InvalidDataException("The product report is empty.");

        var headerRowIndex = FindHeaderRow(rows);
        var headers = rows[headerRowIndex];
        var indexes = ResolveHeaders(headers);
        var products = new List<ImportedProduct>();
        var seenSkus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var rowIndex = headerRowIndex + 1; rowIndex < rows.Count; rowIndex++)
        {
            var rowNumber = rowIndex + 1;
            var fields = rows[rowIndex];
            if (fields.All(string.IsNullOrWhiteSpace))
                continue;

            var name = GetValue(fields, indexes.Name, rowNumber, "name");
            var priceText = GetValue(fields, indexes.Price, rowNumber, "price");
            var stockText = GetValue(fields, indexes.Stock, rowNumber, "stock");
            var category = indexes.Category < 0
                ? "Uncategorized"
                : GetValue(fields, indexes.Category, rowNumber, "category");
            var barcode = indexes.Barcode < 0 ? "" : GetValue(fields, indexes.Barcode, rowNumber, "barcode");
            var sku = indexes.Sku < 0
                ? BuildSku(name, indexes.Serial < 0
                    ? rowNumber.ToString(CultureInfo.InvariantCulture)
                    : GetValue(fields, indexes.Serial, rowNumber, "serial number"))
                : GetValue(fields, indexes.Sku, rowNumber, "SKU");
            if (sku.Length == 0 && indexes.Barcode >= 0)
                sku = barcode;

            if (name.Length == 0)
                throw new InvalidDataException($"Row {rowNumber} has no product name.");
            if (sku.Length == 0)
                throw new InvalidDataException($"Row {rowNumber} has no SKU or barcode.");
            if (!TryParsePrice(priceText, out var price) || price < 0)
                throw new InvalidDataException($"Row {rowNumber} has an invalid non-negative price '{priceText}'.");
            if (!int.TryParse(stockText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var stock) ||
                stock < 0)
                throw new InvalidDataException($"Row {rowNumber} has an invalid non-negative whole-number stock '{stockText}'.");
            if (!seenSkus.Add(sku))
                throw new InvalidDataException($"Row {rowNumber} repeats SKU/barcode '{sku}'. Each product must have a unique SKU.");

            products.Add(new ImportedProduct
            {
                Name = name,
                Category = string.IsNullOrWhiteSpace(category) ? "Uncategorized" : category,
                Sku = sku,
                Barcode = barcode,
                Price = price,
                Stock = stock
            });
        }

        if (products.Count == 0)
            throw new InvalidDataException("The report contains a header but no product rows.");

        return products;
    }

    private static List<string[]> ReadCsvRows(string filePath)
    {
        using var parser = new TextFieldParser(filePath, Encoding.UTF8, detectEncoding: true)
        {
            TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = true
        };
        parser.SetDelimiters(",");
        var rows = new List<string[]>();
        try
        {
            while (!parser.EndOfData)
                rows.Add(parser.ReadFields() ?? []);
        }
        catch (MalformedLineException ex)
        {
            throw new InvalidDataException($"CSV row {ex.LineNumber} has invalid quoting.", ex);
        }

        return rows;
    }

    private static List<string[]> ReadExcelRows(string filePath)
    {
        using var archive = ZipFile.OpenRead(filePath);
        var workbook = ReadXml(archive, "xl/workbook.xml");
        var workbookNamespace = XNamespace.Get("http://schemas.openxmlformats.org/spreadsheetml/2006/main");
        var relationshipNamespace = XNamespace.Get("http://schemas.openxmlformats.org/officeDocument/2006/relationships");
        var packageRelationshipNamespace = XNamespace.Get("http://schemas.openxmlformats.org/package/2006/relationships");
        var sheet = workbook.Root?.Element(workbookNamespace + "sheets")?.Elements(workbookNamespace + "sheet").FirstOrDefault()
            ?? throw new InvalidDataException("The Excel workbook does not contain a worksheet.");
        var relationshipId = (string?)sheet.Attribute(relationshipNamespace + "id")
            ?? throw new InvalidDataException("The first Excel worksheet has no relationship.");
        var relationships = ReadXml(archive, "xl/_rels/workbook.xml.rels");
        var target = (string?)relationships.Root?.Elements(packageRelationshipNamespace + "Relationship")
            .FirstOrDefault(relationship => (string?)relationship.Attribute("Id") == relationshipId)?
            .Attribute("Target")
            ?? throw new InvalidDataException("The first Excel worksheet could not be located.");
        var sheetPath = target.StartsWith("/", StringComparison.Ordinal)
            ? target.TrimStart('/')
            : target.StartsWith("xl/", StringComparison.Ordinal)
                ? target
                : "xl/" + target.TrimStart('/');
        var worksheet = ReadXml(archive, sheetPath);
        var sharedStrings = ReadSharedStrings(archive, workbookNamespace);
        var sheetData = worksheet.Root?.Element(workbookNamespace + "sheetData")
            ?? throw new InvalidDataException("The first Excel worksheet contains no data.");
        var rows = new List<string[]>();

        foreach (var row in sheetData.Elements(workbookNamespace + "row"))
        {
            var rowIndex = int.TryParse((string?)row.Attribute("r"), out var parsedRow) ? parsedRow : rows.Count + 1;
            while (rows.Count < rowIndex)
                rows.Add([]);

            var cells = new List<string>();
            foreach (var cell in row.Elements(workbookNamespace + "c"))
            {
                var reference = (string?)cell.Attribute("r")
                    ?? throw new InvalidDataException($"Excel row {rowIndex} contains a cell with no column reference.");
                var columnIndex = GetColumnIndex(reference);
                while (cells.Count <= columnIndex)
                    cells.Add("");

                cells[columnIndex] = ReadCellValue(cell, workbookNamespace, sharedStrings);
            }
            rows[rowIndex - 1] = cells.ToArray();
        }

        return rows;
    }

    private static XDocument ReadXml(ZipArchive archive, string entryPath)
    {
        var entry = archive.GetEntry(entryPath)
            ?? throw new InvalidDataException($"The Excel workbook is missing '{entryPath}'.");
        using var stream = entry.Open();
        return XDocument.Load(stream);
    }

    private static List<string> ReadSharedStrings(ZipArchive archive, XNamespace workbookNamespace)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null)
            return [];

        using var stream = entry.Open();
        var document = XDocument.Load(stream);
        return document.Root?.Elements(workbookNamespace + "si")
            .Select(item => string.Concat(item.Descendants(workbookNamespace + "t").Select(text => text.Value)))
            .ToList() ?? [];
    }

    private static string ReadCellValue(XElement cell, XNamespace workbookNamespace, IReadOnlyList<string> sharedStrings)
    {
        var type = (string?)cell.Attribute("t");
        if (type == "inlineStr")
            return string.Concat(cell.Descendants(workbookNamespace + "t").Select(text => text.Value)).Trim();

        var value = cell.Element(workbookNamespace + "v")?.Value ?? "";
        if (type != "s")
            return value.Trim();

        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) ||
            index < 0 || index >= sharedStrings.Count)
            throw new InvalidDataException("The Excel workbook contains an invalid shared-string reference.");

        return sharedStrings[index].Trim();
    }

    private static int GetColumnIndex(string cellReference)
    {
        var columnIndex = 0;
        var letters = 0;
        foreach (var character in cellReference)
        {
            if (!char.IsLetter(character))
                break;
            columnIndex = checked(columnIndex * 26 + char.ToUpperInvariant(character) - 'A' + 1);
            letters++;
        }

        if (letters == 0)
            throw new InvalidDataException($"Excel cell reference '{cellReference}' is invalid.");
        return columnIndex - 1;
    }

    private static int FindHeaderRow(IReadOnlyList<string[]> rows)
    {
        var searchLimit = Math.Min(rows.Count, 30);
        for (var rowIndex = 0; rowIndex < searchLimit; rowIndex++)
        {
            var normalized = rows[rowIndex].Select(NormalizeHeader).ToArray();
            var hasName = normalized.Any(header => HeaderAliases[0].Contains(header, StringComparer.Ordinal));
            var hasPrice = normalized.Any(header => HeaderAliases[4].Contains(header, StringComparer.Ordinal));
            var hasStock = normalized.Any(header => HeaderAliases[5].Contains(header, StringComparer.Ordinal));
            if (hasName && hasPrice && hasStock)
                return rowIndex;
        }

        throw new InvalidDataException(
            "Could not find a product header row in the first 30 rows. Include product name, price, and stock columns.");
    }

    private static ColumnIndexes ResolveHeaders(IReadOnlyList<string> headers)
    {
        var normalized = headers.Select(NormalizeHeader).ToArray();
        var indexes = HeaderAliases.Select(aliases => Array.FindIndex(normalized,
            header => aliases.Contains(header, StringComparer.Ordinal))).ToArray();

        var isProductReport = indexes[0] >= 0 && indexes[1] < 0 && indexes[3] < 0 &&
                              normalized.Contains("productunitprice", StringComparer.Ordinal) &&
                              normalized.Contains("currentstockamount", StringComparer.Ordinal);
        if (indexes[0] < 0 || indexes[1] < 0 && indexes[3] < 0 && !isProductReport ||
            indexes[4] < 0 || indexes[5] < 0)
        {
            throw new InvalidDataException(
                "Product headers must include name, SKU or barcode, price, and stock. " +
                $"Headers found: {string.Join(", ", headers)}.");
        }

        if (indexes[1] < 0 && indexes[3] < 0 && !isProductReport)
            throw new InvalidDataException("Product headers must include a SKU or barcode column.");

        var mapped = indexes.Where(index => index >= 0).ToArray();
        if (mapped.Distinct().Count() != mapped.Length)
            throw new InvalidDataException("A CSV column was matched to more than one product field. Check the header row.");

        var serialIndex = Array.FindIndex(normalized,
            header => SerialAliases.Contains(header, StringComparer.Ordinal));
        return new ColumnIndexes(indexes[0], indexes[1], indexes[2], indexes[3], indexes[4], indexes[5],
            serialIndex);
    }

    private static string NormalizeHeader(string header) =>
        new(header.Trim().TrimStart('\uFEFF').Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant).ToArray());

    private static string BuildSku(string productName, string serial)
    {
        var normalizedName = string.Join(' ', productName.Trim().Split((char[]?)null,
            StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedName));
        var serialToken = new string(serial.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        if (serialToken.Length == 0)
            throw new InvalidDataException("A product without a SKU needs a serial number to generate a stable SKU.");
        if (serialToken.Length > 16)
            serialToken = serialToken[..16];
        return $"IMP-{Convert.ToHexString(hash.AsSpan(0, 6))}-{serialToken}";
    }

    private static string GetValue(IReadOnlyList<string> fields, int index, int rowNumber, string fieldName)
    {
        if (index < 0 || index >= fields.Count)
            throw new InvalidDataException($"CSV row {rowNumber} is missing the {fieldName} column value.");

        return fields[index].Trim();
    }

    private static bool TryParsePrice(string text, out decimal value)
    {
        var candidate = text.Trim();
        if (decimal.TryParse(candidate, NumberStyles.Currency, CultureInfo.InvariantCulture, out value) ||
            decimal.TryParse(candidate, NumberStyles.Currency, CultureInfo.GetCultureInfo("en-IN"), out value))
            return true;

        candidate = candidate.Replace("₹", "", StringComparison.Ordinal)
            .Replace("S$", "", StringComparison.OrdinalIgnoreCase)
            .Replace("$", "", StringComparison.Ordinal)
            .Replace(",", "", StringComparison.Ordinal)
            .Trim();
        return decimal.TryParse(candidate, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    private sealed record ColumnIndexes(int Name, int Sku, int Category, int Barcode, int Price, int Stock, int Serial);
}
