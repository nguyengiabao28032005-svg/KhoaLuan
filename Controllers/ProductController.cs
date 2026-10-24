using BeautyShop.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace BeautyShop.Controllers
{
    public class ProductController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ProductController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index(
             decimal? minPrice,
             decimal? maxPrice,
             string? keyword,
             int? categoryId,
             int? brandId,
             string? sortBy) // Thêm tham số nhận kiểu sắp xếp
        {
            var products = _context.Products
                .Include(x => x.Brand)
                .Include(x => x.Category)
                .Include(x => x.ProductVariants!)
                    .ThenInclude(v => v.Inventories)
                .Where(x => x.IsActive)
                .AsQueryable();

            if (!string.IsNullOrEmpty(keyword))
            {
                string rawKey = keyword.Trim().ToLower();

                // Tách các từ khóa ra
                var terms = rawKey.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                products = products.Where(x =>
                    x.Name.ToLower().Contains(rawKey) ||
                    (x.Brand != null && x.Brand.Name.ToLower().Contains(rawKey)) ||
                    (x.Description != null && x.Description.ToLower().Contains(rawKey)) ||

                    terms.All(term =>
                        x.Name.ToLower().Contains(term) ||
                        (x.Brand != null && x.Brand.Name.ToLower().Contains(term)) ||
                        (x.Description != null && x.Description.ToLower().Contains(term))
                    ) ||

                    (rawKey.Contains("chống nắng") && (x.Name.ToLower().Contains("chống") && x.Name.ToLower().Contains("nắng"))) ||
                    ((rawKey.Contains("sữa rửa mặt") || rawKey.Contains("rửa mặt")) && (x.Name.ToLower().Contains("rửa") && x.Name.ToLower().Contains("mặt")))
                );
            }

            // 2. Lọc theo Category
            if (categoryId.HasValue)
            {
                products = products.Where(x => x.CategoryId == categoryId);
            }

            // 3. Lọc theo Brand
            if (brandId.HasValue)
            {
                products = products.Where(x => x.BrandId == brandId);
            }

            // 4. Lọc theo giá
            if (minPrice.HasValue)
            {
                products = products.Where(x => x.ProductVariants!.Any(v => v.RetailPrice >= minPrice));
            }
            if (maxPrice.HasValue)
            {
                products = products.Where(x => x.ProductVariants!.Any(v => v.RetailPrice <= maxPrice));
            }

            // 5. Xử lý Sắp xếp (Sort By)
            switch (sortBy)
            {
                case "newest":
                    products = products.OrderByDescending(x => x.Id);
                    break;
                case "price_asc":
                    // Chuyển sang xử lý trên memory bằng AsEnumerable() để SQLite không bị lỗi dịch cú pháp decimal Min
                    var listForAsc = products.ToList();
                    var sortedAsc = listForAsc.OrderBy(x => x.ProductVariants?.Min(v => v.RetailPrice) ?? 0).ToList();
                    ViewBag.Categories = _context.Categories.ToList();
                    ViewBag.Brands = _context.Brands.ToList();
                    ViewBag.Keyword = keyword;
                    ViewBag.SortBy = sortBy;
                    return View(sortedAsc);

                case "price_desc":
                    var listForDesc = products.ToList();
                    var sortedDesc = listForDesc.OrderByDescending(x => x.ProductVariants?.Max(v => v.RetailPrice) ?? 0).ToList();
                    ViewBag.Categories = _context.Categories.ToList();
                    ViewBag.Brands = _context.Brands.ToList();
                    ViewBag.Keyword = keyword;
                    ViewBag.SortBy = sortBy;
                    return View(sortedDesc);

                case "popular":
                default:
                    var userId = HttpContext?.Session?.GetInt32("UserId");
                    if (userId != null && string.IsNullOrEmpty(keyword) && !categoryId.HasValue && !brandId.HasValue)
                    {
                        var behaviors = _context.UserBehaviors
                            .Include(x => x.Product)
                            .Where(x => x.UserId == userId.Value)
                            .ToList();

                        if (behaviors.Any())
                        {
                            var topCategory = behaviors
                                .Where(x => x.Product != null)
                                .GroupBy(x => x.Product.CategoryId)
                                .Select(g => new {
                                    CategoryId = g.Key,
                                    Score = g.Sum(x => x.ActionType == "View" ? 1 : 3)
                                })
                                .OrderByDescending(x => x.Score)
                                .FirstOrDefault();

                            var topBrand = behaviors
                                .Where(x => x.Product != null)
                                .GroupBy(x => x.Product.BrandId)
                                .Select(g => new {
                                    BrandId = g.Key,
                                    Score = g.Sum(x => x.ActionType == "View" ? 1 : 3)
                                })
                                .OrderByDescending(x => x.Score)
                                .FirstOrDefault();

                            int? topCatId = topCategory?.CategoryId;
                            int? topBrnId = topBrand?.BrandId;

                            products = products
                                .OrderByDescending(p => p.CategoryId == topCatId)
                                .ThenByDescending(p => p.BrandId == topBrnId)
                                .ThenByDescending(p => p.Id);
                            break;
                        }
                    }
                    products = products.OrderByDescending(p => p.Id);
                    break;
            }

            ViewBag.Categories = _context.Categories.ToList();
            ViewBag.Brands = _context.Brands.ToList();
            ViewBag.Keyword = keyword;
            ViewBag.SortBy = sortBy;

            return View(products.ToList());
        }

        public IActionResult Detail(int id)
        {
            var product = _context.Products
                .Include(x => x.Brand)
                .Include(x => x.Category)
                .Include(x => x.ProductVariants!)
                    .ThenInclude(v => v.Inventories)
                .FirstOrDefault(x => x.Id == id);

            if (product == null)
            {
                return NotFound();
            }

            // Sắp xếp và lấy thông tin tồn kho chính xác cho biến thể mặc định
            if (product.ProductVariants != null && product.ProductVariants.Any())
            {
                product.ProductVariants = product.ProductVariants.OrderBy(v => v.Id).ToList();
                var variant = product.ProductVariants.First();

                int stock = variant.Inventories?.Sum(i => i.StockQuantity) ?? 0;
                if (stock == 0)
                {
                    stock = _context.Inventories.Where(i => i.VariantId == variant.Id).Sum(i => (int?)i.StockQuantity) ?? 0;
                }

                ViewBag.IsAvailable = stock > 0;
                ViewBag.StockQuantity = stock;
            }
            else
            {
                ViewBag.IsAvailable = false;
                ViewBag.StockQuantity = 0;
            }

            var userId = HttpContext?.Session?.GetInt32("UserId");
            if (userId.HasValue)
            {
                _context.UserBehaviors.Add(new BeautyShop.Models.UserBehavior
                {
                    UserId = userId.Value,
                    ProductId = id,
                    ActionType = "View",
                    CreatedAt = DateTime.Now
                });

                _context.ProductViews.Add(new BeautyShop.Models.ProductView
                {
                    UserId = userId.Value,
                    ProductId = id,
                    ViewedAt = DateTime.Now
                });

                _context.SaveChanges();
            }

            ViewBag.Reviews = _context.Reviews
                .Include(x => x.User)
                .Where(x => x.ProductId == id && x.IsApproved && !x.IsHidden)
                .OrderByDescending(x => x.CreatedAt)
                .ToList();

            var now = DateTime.Now;
            var activeVouchers = _context.Vouchers
                .Where(v => v.IsActive && now >= v.StartDate && now <= v.EndDate && (v.TotalUsageLimit == null || v.UsedCount < v.TotalUsageLimit))
                .ToList();

            ViewBag.AvailableVouchers = activeVouchers
                .OrderBy(v => v.MinOrderValue)
                .Take(4)
                .ToList();

            return View(product);
        }
    }
}