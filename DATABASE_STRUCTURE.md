# Saraa Boutique POS Desktop - Database Schema & Data Documentation

- **Portable Database Path:** `c:\Users\prave\OneDrive\Documents\Projects\Desktop POS\HyperPOS_Windows_CSharp\Saraa_Boutique_POS_Portable\hyperpos.db`
- **Project Root Database File:** `c:\Users\prave\OneDrive\Documents\Projects\Desktop POS\HyperPOS_Windows_CSharp\hyperpos.db`
- **Application Data Database:** `C:\Users\prave\AppData\Local\HyperPOS.Desktop\hyperpos.db`
- **SQL Script Dump File:** `c:\Users\prave\OneDrive\Documents\Projects\Desktop POS\HyperPOS_Windows_CSharp\database_dump.sql`
- **Shareable Standalone Folder:** `c:\Users\prave\OneDrive\Documents\Projects\Desktop POS\HyperPOS_Windows_CSharp\Saraa_Boutique_POS_Portable`
- **Shareable Standalone ZIP:** `c:\Users\prave\OneDrive\Documents\Projects\Desktop POS\HyperPOS_Windows_CSharp\Saraa_Boutique_POS_Portable.zip`

> [!NOTE]
> You can open `hyperpos.db` directly in **DB Browser for SQLite**, **DBeaver**, **VS Code SQLite Viewer**, or any SQLite/database client to view all tables and records.

---

## 1. Tables Summary

| Table Name | Rows | Purpose / Content |
|---|---|---|
| **`admins`** | **8** | Store admin & cashier profiles from MySQL dump (including Super Admin Praveen Balaji) |
| **`categories`** | **58** | Complete Saraa Boutique categories catalog from the latest database dump |
| **`products`** | **11** | Saraa Boutique product catalog with CDN image thumbnails, original prices, discounts, and selling prices |
| **`customers`** | **0** | Registered customer profiles, phone numbers, emails, and billing addresses |
| **`pos_shifts`** | **2** | Cashier shift register, opening cash, closing cash, difference, and business date |
| **`sales`** | **2** | Completed POS orders, receipt numbers, payment methods, and cashier attribution |
| **`sale_items`** | **9** | Individual line items per receipt (product SKU, price, quantity) |
| **`held_orders`** | **0** | Suspended/held carts waiting to be resumed |
| **`held_order_items`** | **0** | Line items for held orders |
| **`shift_cash_counts`** | **0** | Currency denomination breakdown counted at shift day-end settlement |

---

## 2. Super Admin & Staff Accounts (`admins` table)

| ID | Name | Email | Phone | Role | Status |
|---|---|---|---|---|---|
| **8** | **Praveen Balaji** | **`praveenbalajimindzmap@gmail.com`** | `+919940619605` | **Super Admin** | Active |
| 1 | Douzzo Dev | `support@mindzmap.com` | `+919940619605` | Developer | Active |
| 2 | Jeyasri Selvaraj | `jeyasrimindzmap@gmail.com` | `+919940619605` | Admin | Active |
| 3 | Parthiban | `parthibanvs28@gmail.com` | `+919940619605` | Cashier | Active |
| 4 | Saraa Boutique | `saraaboutiqueoff@gmail.com` | `+919003031575` | Store Owner | Active |
| 5 | Balaji | `balajim02626@gmail.com` | `+919940619605` | Cashier | Active |
| 6 | Dharshini | `dharshinimindzmap@gmail.com` | `+919940619605` | Admin | Active |
| 7 | Sathish | `sathishrmindzmap@gmail.com` | `+919940619605` | Cashier | Active |

---

## 3. Products Catalog (`products` table)

| ID | SKU | Product Name | Category | Orig. Price | Discount | Selling Price | Stock | Image WebP CDN |
|---|---|---|---|---|---|---|---|---|
| 1 | `SB0001` | linen cotton checked with digital flowers | Sarees | ₹899.00 | ₹100 flat | **₹799.00** | 10 | [Thumbnail](https://saraaboutique.com/storage/product/thumbnail/2026-10-10-6ac9c1e14e15c.webp) |
| 2 | `SB0002` | linen cotton checked with digital flowers | Sarees | ₹899.00 | ₹100 flat | **₹799.00** | 4 | [Thumbnail](https://saraaboutique.com/storage/product/thumbnail/2026-10-10-6ac9c3663e24e.webp) |
| 3 | `SB0003` | linen cotton emerald green | Sarees | ₹899.00 | ₹100 flat | **₹799.00** | 7 | [Thumbnail](https://saraaboutique.com/storage/product/thumbnail/2026-10-10-6ac9c44c43dcf.webp) |
| 4 | `SB0004` | linen cotton checked with digital flowers | Sarees | ₹899.00 | ₹100 flat | **₹799.00** | 5 | [Thumbnail](https://saraaboutique.com/storage/product/thumbnail/2026-10-10-6ac9c525b852e.webp) |
| 5 | `SB0005` | linen cotton with garrife print | Sarees | ₹899.00 | ₹100 flat | **₹799.00** | 4 | [Thumbnail](https://saraaboutique.com/storage/product/thumbnail/2026-10-10-6ac9c6443b4c2.webp) |
| 6 | `SB0006` | Mal Cotton Saree | Sarees | ₹1,050.00 | ₹100 flat | **₹950.00** | 6 | [Thumbnail](https://saraaboutique.com/storage/product/thumbnail/2026-10-10-6ac9c77a1127a.webp) |
| 7 | `SB0007` | Mul Cotton With Checked Saree | Sarees | ₹1,500.00 | ₹100 flat | **₹1,400.00** | 100 | [Thumbnail](https://saraaboutique.com/storage/product/thumbnail/2026-10-10-6ac9c86765c06.webp) |
| 8 | `SB0008` | Mul Cotton With Tissue Saree | Sarees | ₹1,550.00 | ₹100 flat | **₹1,450.00** | 98 | [Thumbnail](https://saraaboutique.com/storage/product/thumbnail/2026-10-10-6ac9c85de6a46.webp) |
| 9 | `SB0009` | Nayanthara Saree | Celebrity Collections | ₹899.00 | ₹100 flat | **₹799.00** | 4 | [Thumbnail](https://saraaboutique.com/storage/product/thumbnail/2026-10-10-6ac9c60a9072b.webp) |
| 10 | `SB0010` | Chinon Mirror Work Saree | Sarees | ₹999.00 | ₹100 flat | **₹899.00** | 8 | [Thumbnail](https://saraaboutique.com/storage/product/thumbnail/2026-10-10-6ac9c4964d797.webp) |
| 11 | `SB0011` | Mul Kolam Saree | Sarees | ₹1,500.00 | ₹100 flat | **₹1,400.00** | 5 | [Thumbnail](https://saraaboutique.com/storage/product/thumbnail/2026-10-10-6ac9c2d0e6941.webp) |

---

## 4. Categories (`categories` table - 58 Total)

1. Sarees
2. Premium Silk Sarees
3. Celebrity Collections
4. Blouse Collections
5. Kurtis & Dupatta
6. Nightwear
7. Kids Wear
8. Men’s Wear
9. Jewellery
10. Artificial Flowers
11. Accessories
12. Handbags
13. Salwar Sets
14. Bridal Collections
15. Occasion Collections
16. Office Wear
17. Traditional Collections
18. Regular Wear
19. Bridal Collections 2
20. Occasion Collections 2
21. Traditional Collections 2
22. Silk Saree
23. Silk Cotton
24. Readymade
25. Unstitched Blouse
26. Aari Blouse Unstitched
27. Embroidery Blouse Unstitched
28. Anarkali Kurti
29. Stright Cut
30. Aline Kurti
31. Short Kurti
32. Cord Set
33. Cotton Dupatta
34. Silk Dupatta
35. Chiffon Dupatta
36. 3 Piece Salwar Set
37. Unstitched Salwar Material
38. Cotton Frock Nighty
39. Cotton Regular Nighty
40. T shirt
41. Track Pants
42. Kids Frock
43. Kids Cord Set
44. Kids Pawadai Set
45. Dhotis
46. Earings
47. Chain
48. Small Necklace
49. Long Chain
50. Bridal Necklace
51. Anti Tarnish Chain
52. Artificial Hair Flowers
53. Artificial Hair Clips
54. Hair Clips
55. Korean Hair clips
56. Sling Bag
57. Tote Bag
58. Office bag

---

## 5. Portable Distribution

To run this project on any other Windows 10/11 64-bit device:
1. Copy the folder `Saraa_Boutique_POS_Portable` OR the zip archive `Saraa_Boutique_POS_Portable.zip`.
2. Extract the zip if copying the archive.
3. Double-click `Saraa_Boutique_POS.exe` or `Launch_POS.bat`.
4. No prerequisites, no .NET install, and no database setup required. The pre-loaded `hyperpos.db` SQLite database is included in the folder.
