using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using BeautyShop.Data;
using BeautyShop.Models;

namespace BeautyShop.Services
{
    public static class SupplierSeeder
    {
        public static async Task EnsureSeededAsync(ApplicationDbContext db)
        {
            if (await db.Suppliers.AnyAsync()) return;

            var s1 = new Supplier
            {
                Name = "Công ty TNHH L'Oréal Việt Nam",
                ContactPerson = "Nguyễn Thu Hà - Đại diện phân phối",
                Email = "contact@loreal.vn",
                Phone = "028 3936 9142",
                Address = "Tầng 12, Tòa nhà Vincom Center, 72 Lê Thánh Tôn, Phường Bến Nghé, Quận 1, TP. Hồ Chí Minh",
                TaxCode = "0304910242",
                Country = "Pháp",
                IsActive = true,
                Note = "Nhà phân phối độc quyền chính hãng các dòng son, kem dưỡng L'Oreal Paris, Maybelline New York và La Roche-Posay tại Việt Nam.",
                CreatedAt = DateTime.Now
            };

            var s2 = new Supplier
            {
                Name = "Hasaki Beauty & Clinic Distribution",
                ContactPerson = "Trần Minh Tâm - Trưởng phòng thu mua",
                Email = "supply@hasaki.vn",
                Phone = "1800 6324",
                Address = "71 Hoàng Hoa Thám, Phường 13, Quận Tân Bình, TP. Hồ Chí Minh",
                TaxCode = "0313612829",
                Country = "Việt Nam",
                IsActive = true,
                Note = "Đối tác cung ứng mỹ phẩm sỉ nội địa hàng đầu, cung ứng đa nhãn hiệu: Son dưỡng, son kem lì, kem chống nắng, chì kẻ mắt.",
                CreatedAt = DateTime.Now
            };

            var s3 = new Supplier
            {
                Name = "Công ty TNHH Mỹ phẩm Shiseido Việt Nam",
                ContactPerson = "Kenji Sato - Giám đốc cung ứng",
                Email = "inquiry@shiseido.com.vn",
                Phone = "028 3910 4244",
                Address = "Lầu 27, Tòa nhà Vietcombank Tower, Số 5 Công Trường Mê Linh, Bến Nghé, Quận 1, TP. Hồ Chí Minh",
                TaxCode = "0309503412",
                Country = "Nhật Bản",
                IsActive = true,
                Note = "Nhập khẩu và phân phối trực tiếp các sản phẩm cao cấp từ Shiseido Nhật Bản.",
                CreatedAt = DateTime.Now
            };

            var s4 = new Supplier
            {
                Name = "Estée Lauder Companies Vietnam",
                ContactPerson = "Phạm Hoàng Yến - Giám đốc phát triển kinh doanh",
                Email = "elc_vietnam@esteelauder.com",
                Phone = "028 3824 6333",
                Address = "Tầng 10, Tòa nhà Sun Wah Tower, 115 Nguyễn Huệ, Phường Bến Nghé, Quận 1, TP. Hồ Chí Minh",
                TaxCode = "0310245891",
                Country = "Mỹ",
                IsActive = true,
                Note = "Phân phối thương hiệu Estee Lauder, MAC Cosmetics, Clinique chính ngạch quốc tế.",
                CreatedAt = DateTime.Now
            };

            var s5 = new Supplier
            {
                Name = "Công ty Cổ phần Mỹ phẩm Quốc tế MAC & AmorePacific",
                ContactPerson = "Lê Thảo Vy - Quản lý quan hệ đối tác",
                Email = "orders@amorepacific-mac.vn",
                Phone = "028 3815 0244",
                Address = "Lô 12A, KCN Tân Bình, Phường Tây Thạnh, Quận Tân Phú, TP. Hồ Chí Minh",
                TaxCode = "0308112233",
                Country = "Hàn Quốc",
                IsActive = true,
                Note = "Chuyên dòng son môi đình đám MAC Retro Matte, 3CE Velvet Tint, phấn phủ và đồ trang điểm chuẩn Hàn.",
                CreatedAt = DateTime.Now
            };

            db.Suppliers.AddRange(s1, s2, s3, s4, s5);
            await db.SaveChangesAsync();

            // Gán bảng cung ứng trung gian (SupplierProducts)
            var products = await db.Products.Include(p => p.Brand).Include(p => p.Category).ToListAsync();
            var variants = await db.ProductVariants.ToListAsync();

            foreach (var p in products)
            {
                var basePrice = variants.FirstOrDefault(v => v.ProductId == p.Id)?.RetailPrice ?? 200000m;
                var suppliedCost = Math.Round(basePrice * 0.65m / 1000m) * 1000m;

                if (p.Brand?.Name != null && (p.Brand.Name.Contains("L’Oréal") || p.Brand.Name.Contains("L'Oreal") || p.Brand.Name.Contains("Maybelline") || p.Brand.Name.Contains("Roche-Posay")))
                {
                    if (!db.SupplierProducts.Any(sp => sp.SupplierId == s1.Id && sp.ProductId == p.Id))
                    {
                        db.SupplierProducts.Add(new SupplierProduct
                        {
                            SupplierId = s1.Id,
                            ProductId = p.Id,
                            SuppliedPrice = suppliedCost,
                            Notes = "Hợp đồng phân phối chính ngạch L'Oreal VN",
                            CreatedAt = DateTime.Now
                        });
                    }
                }

                if (p.Brand?.Name != null && p.Brand.Name.Contains("Shiseido"))
                {
                    if (!db.SupplierProducts.Any(sp => sp.SupplierId == s3.Id && sp.ProductId == p.Id))
                    {
                        db.SupplierProducts.Add(new SupplierProduct
                        {
                            SupplierId = s3.Id,
                            ProductId = p.Id,
                            SuppliedPrice = suppliedCost,
                            Notes = "Nhập khẩu nguyên chiếc từ Shiseido Japan",
                            CreatedAt = DateTime.Now
                        });
                    }
                }

                if (p.Brand?.Name != null && p.Brand.Name.Contains("Estée Lauder"))
                {
                    if (!db.SupplierProducts.Any(sp => sp.SupplierId == s4.Id && sp.ProductId == p.Id))
                    {
                        db.SupplierProducts.Add(new SupplierProduct
                        {
                            SupplierId = s4.Id,
                            ProductId = p.Id,
                            SuppliedPrice = suppliedCost,
                            Notes = "Hợp đồng độc quyền tập đoàn Estée Lauder",
                            CreatedAt = DateTime.Now
                        });
                    }
                }

                if (p.Brand?.Name != null && (p.Brand.Name.Contains("MAC") || p.Brand.Name.Contains("3CE")))
                {
                    if (!db.SupplierProducts.Any(sp => sp.SupplierId == s5.Id && sp.ProductId == p.Id))
                    {
                        db.SupplierProducts.Add(new SupplierProduct
                        {
                            SupplierId = s5.Id,
                            ProductId = p.Id,
                            SuppliedPrice = suppliedCost,
                            Notes = "Cung ứng mỹ phẩm trang điểm chuẩn Hàn Quốc",
                            CreatedAt = DateTime.Now
                        });
                    }
                }

                if (p.Category?.Name != null && (p.Category.Name.Contains("Son") || p.Category.Name.Contains("Kem Chống Nắng")))
                {
                    if (!db.SupplierProducts.Any(sp => sp.SupplierId == s2.Id && sp.ProductId == p.Id))
                    {
                        db.SupplierProducts.Add(new SupplierProduct
                        {
                            SupplierId = s2.Id,
                            ProductId = p.Id,
                            SuppliedPrice = Math.Round(suppliedCost * 1.05m / 1000m) * 1000m,
                            Notes = "Đối tác đại lý tổng Hasaki",
                            CreatedAt = DateTime.Now
                        });
                    }
                }
            }

            await db.SaveChangesAsync();

            // Cập nhật các phiếu nhập cũ (nếu có) để gắn với SupplierId phù hợp
            var receipts = await db.StockReceipts.ToListAsync();
            foreach (var r in receipts)
            {
                if (r.SupplierId == null)
                {
                    if (!string.IsNullOrEmpty(r.Supplier))
                    {
                        var matched = await db.Suppliers.FirstOrDefaultAsync(s => s.Name.ToLower().Contains(r.Supplier.ToLower()) || r.Supplier.ToLower().Contains(s.Name.ToLower()));
                        if (matched != null)
                        {
                            r.SupplierId = matched.Id;
                        }
                        else
                        {
                            r.SupplierId = s2.Id;
                        }
                    }
                    else
                    {
                        r.SupplierId = s2.Id;
                        r.Supplier = s2.Name;
                    }
                }
            }
            await db.SaveChangesAsync();
        }
    }
}
