using System;
using System.IO;
using HyperPOS.Desktop.Data;

namespace HyperPOS.Desktop;

public static class SaraaSqlImporter
{
    public static void ImportSaraaProducts(PosDatabase db)
    {
        // 1. Seed store categories from Saraa Boutique dump (58 categories)
        var categories = new (int Id, string Name, string Slug)[]
        {
            (1, "Sarees", "sarees"),
            (2, "Premium Silk Sarees", "premium-silk-sarees"),
            (3, "Celebrity Collections", "celebrity-collections"),
            (4, "Blouse Collections", "blouse-collections"),
            (5, "Kurtis & Dupatta", "kurtis-dupatta"),
            (6, "Nightwear", "nightwear"),
            (7, "Kids Wear", "kids-wear"),
            (8, "Men’s Wear", "mens-wear"),
            (9, "Jewellery", "jewellery"),
            (10, "Artificial Flowers", "artificial-flowers"),
            (11, "Accessories", "accessories"),
            (12, "Handbags", "handbags"),
            (13, "Salwar Sets", "salwar-sets"),
            (14, "Bridal Collections", "bridal-collections"),
            (15, "Occasion Collections", "occasion-collections"),
            (16, "Office Wear", "office-wear"),
            (17, "Traditional Collections", "traditional-collections"),
            (18, "Regular Wear", "regular-wear"),
            (19, "Bridal Collections 2", "bridal-collections-2"),
            (20, "Occasion Collections 2", "occasion-collections-2"),
            (21, "Traditional Collections 2", "traditional-collections-2"),
            (22, "Silk Saree", "silk-saree"),
            (23, "Silk Cotton", "silk-cotton"),
            (24, "Readymade", "readymade"),
            (25, "Unstitched Blouse", "unstitched-blouse"),
            (26, "Aari Blouse Unstitched", "aari-blouse-unstitched"),
            (27, "Embroidery Blouse Unstitched", "embroidery-blouse-unstitched"),
            (28, "Anarkali Kurti", "anarkali-kurti"),
            (29, "Stright Cut", "stright-cut"),
            (30, "Aline Kurti", "aline-kurti"),
            (31, "Short Kurti", "short-kurti"),
            (32, "Cord Set", "cord-set"),
            (33, "Cotton Dupatta", "cotton-dupatta"),
            (34, "Silk Dupatta", "silk-dupatta"),
            (35, "Chiffon Dupatta", "chiffon-dupatta"),
            (36, "3 Piece Salwar Set", "3-piece-salwar-set"),
            (37, "Unstitched Salwar Material", "unstitched-salwar-material"),
            (38, "Cotton Frock Nighty", "cotton-frock-nighty"),
            (39, "Cotton Regular Nighty", "cotton-regular-nighty"),
            (40, "T shirt", "t-shirt"),
            (41, "Track Pants", "track-pants"),
            (42, "Kids Frock", "kids-frock"),
            (43, "Kids Cord Set", "kids-cord-set"),
            (44, "Kids Pawadai Set", "kids-pawadai-set"),
            (45, "Dhotis", "dhotis"),
            (46, "Earings", "earings"),
            (47, "Chain", "chain"),
            (48, "Small Necklace", "small-necklace"),
            (49, "Long Chain", "long-chain"),
            (50, "Bridal Necklace", "bridal-necklace"),
            (51, "Anti Tarnish Chain", "anti-tarnish-chain"),
            (52, "Artificial Hair Flowers", "artificial-hair-flowers"),
            (53, "Artificial Hair Clips", "artificial-hair-clips"),
            (54, "Hair Clips", "hair-clips"),
            (55, "Korean Hair clips", "korean-hair-clips"),
            (56, "Sling Bag", "sling-bag"),
            (57, "Tote Bag", "tote-bag"),
            (58, "Office bag", "office-bag")
        };

        foreach (var cat in categories)
        {
            try
            {
                db.SeedCategory(cat.Id, cat.Name, cat.Slug);
            }
            catch
            {
            }
        }

        // 2. Seed admins from Saraa Boutique dump (including Praveen Balaji as Super Admin)
        var admins = new (int Id, string Name, string Email, string Phone, int RoleId, string RoleName, string PasswordHash)[]
        {
            (1, "Douzzo Dev", "support@mindzmap.com", "+919940619605", 1, "Developer", "$2y$10$4ds0FVwgscbgVgNeusbQp.0dTYuwQoHZ1Cu4DMoD567h0gPajKgku"),
            (2, "Jeyasri Selvaraj", "jeyasrimindzmap@gmail.com", "+919940619605", 2, "Admin", "$2y$10$4v68nc8H6c.saAKxgNP0Ce.Ftrdm8BmVBdCk75/7XeKk.8E9mEdDW"),
            (3, "Parthiban", "parthibanvs28@gmail.com", "+919940619605", 6, "Cashier", "$2y$10$6.AABo/vWZ2I/dGfVZdXfuLMrBTf.5m.VhZdqbdKNQn0Q6kWS77W."),
            (4, "Saraa Boutique", "saraaboutiqueoff@gmail.com", "+919003031575", 6, "Store Owner", "$2y$10$SQ58r3GdYq/aE4Jmr1gDbe2GwWq5ewTPRnEjDcuSVg.KX4IfTcY9O"),
            (5, "Balaji", "balajim02626@gmail.com", "+919940619605", 6, "Cashier", "$2y$10$a6N47XNwHD6W/HctiKBdIugYLQei/LvcVZ4RAlUDyS04yxQoFAcBa"),
            (6, "Dharshini", "dharshinimindzmap@gmail.com", "+919940619605", 2, "Admin", "$2y$10$kudPiS6DZwdusmccYGIP6eCfuxxXewHJmDdNe7wkHvW4uDhnuyg1m"),
            (7, "Sathish", "sathishrmindzmap@gmail.com", "+919940619605", 6, "Cashier", "$2y$10$foYjom.y6C5yBj5kq27eFOgA4TnLTh0Ifthsz5sHtDTNGmJnMemK2"),
            (8, "Praveen Balaji", "praveenbalajimindzmap@gmail.com", "+919940619605", 2, "Super Admin", "$2y$10$3fVSsQ3Z85A5eYdMsg/ghOnbziSQZhjty.VUMmXNmoc1DY2R3BOoK")
        };

        foreach (var admin in admins)
        {
            try
            {
                db.SeedAdmin(admin.Id, admin.Name, admin.Email, admin.Phone, admin.RoleId, admin.RoleName, admin.PasswordHash);
            }
            catch
            {
            }
        }

        // 3. Seed real products from Saraa Boutique dump with exact image URLs, stock, prices, and 10% discount
        var items = new[]
        {
            (Name: "Mul Cotton With Checked Saree", Category: "Sarees", Sku: "SB0007", OriginalPrice: 1500.00m, Discount: 10.00m, DiscountType: "percent", SellingPrice: 1350.00m, Stock: 97, ImageUrl: "https://saraaboutique.com/storage/product/thumbnail/2026-10-10-6ac9c86765c06.webp"),
            (Name: "Mul Cotton With Tissue Saree", Category: "Sarees", Sku: "SB0008", OriginalPrice: 1550.00m, Discount: 10.00m, DiscountType: "percent", SellingPrice: 1395.00m, Stock: 89, ImageUrl: "https://saraaboutique.com/storage/product/thumbnail/2026-10-10-6ac9c85de6a46.webp"),
            (Name: "Mal Cotton Saree", Category: "Sarees", Sku: "SB0006", OriginalPrice: 1050.00m, Discount: 10.00m, DiscountType: "percent", SellingPrice: 945.00m, Stock: 6, ImageUrl: "https://saraaboutique.com/storage/product/thumbnail/2026-10-10-6ac9c77a1127a.webp"),
            (Name: "linen cotton with garrife print", Category: "Sarees", Sku: "SB0005", OriginalPrice: 899.00m, Discount: 10.00m, DiscountType: "percent", SellingPrice: 809.10m, Stock: 4, ImageUrl: "https://saraaboutique.com/storage/product/thumbnail/2026-10-10-6ac9c6443b4c2.webp"),
            (Name: "Nayanthara Saree", Category: "Celebrity Collections", Sku: "SB0009", OriginalPrice: 899.00m, Discount: 10.00m, DiscountType: "percent", SellingPrice: 809.10m, Stock: 4, ImageUrl: "https://saraaboutique.com/storage/product/thumbnail/2026-10-10-6ac9c60a9072b.webp"),
            (Name: "linen cotton checked with digital flowers", Category: "Sarees", Sku: "SB0004", OriginalPrice: 899.00m, Discount: 10.00m, DiscountType: "percent", SellingPrice: 809.10m, Stock: 5, ImageUrl: "https://saraaboutique.com/storage/product/thumbnail/2026-10-10-6ac9c525b852e.webp"),
            (Name: "Chinon Mirror Work Saree", Category: "Sarees", Sku: "SB0010", OriginalPrice: 999.00m, Discount: 10.00m, DiscountType: "percent", SellingPrice: 899.00m, Stock: 8, ImageUrl: "https://saraaboutique.com/storage/product/thumbnail/2026-10-10-6ac9c4964d797.webp"),
            (Name: "linen cotton emerald green", Category: "Sarees", Sku: "SB0003", OriginalPrice: 899.00m, Discount: 10.00m, DiscountType: "percent", SellingPrice: 809.10m, Stock: 7, ImageUrl: "https://saraaboutique.com/storage/product/thumbnail/2026-10-10-6ac9c44c43dcf.webp"),
            (Name: "linen cotton checked with digital flowers", Category: "Sarees", Sku: "SB0002", OriginalPrice: 899.00m, Discount: 10.00m, DiscountType: "percent", SellingPrice: 809.10m, Stock: 4, ImageUrl: "https://saraaboutique.com/storage/product/thumbnail/2026-10-10-6ac9c3663e24e.webp"),
            (Name: "Mul Kolam Saree", Category: "Sarees", Sku: "SB0011", OriginalPrice: 1500.00m, Discount: 10.00m, DiscountType: "percent", SellingPrice: 1350.00m, Stock: 4, ImageUrl: "https://saraaboutique.com/storage/product/thumbnail/2026-10-10-6ac9c2d0e6941.webp"),
            (Name: "linen cotton checked with digital flowers", Category: "Sarees", Sku: "SB0001", OriginalPrice: 899.00m, Discount: 10.00m, DiscountType: "percent", SellingPrice: 809.10m, Stock: 10, ImageUrl: "https://saraaboutique.com/storage/product/thumbnail/2026-10-10-6ac9c1e14e15c.webp"),
        };

        foreach (var item in items)
        {
            try
            {
                db.AddProduct(item.Name, item.Category, item.Sku, item.Sku, item.SellingPrice, item.Stock, item.ImageUrl, item.OriginalPrice, item.Discount, item.DiscountType);
            }
            catch
            {
                // Already added
            }
            db.UpdateProductPricing(item.Sku, item.OriginalPrice, item.Discount, item.DiscountType, item.SellingPrice, item.ImageUrl, item.Stock);
        }
    }
}
