using System.Diagnostics;
using BeautyShop.Data;
using BeautyShop.Models;
using BeautyShop.Services;
using BeautyShop.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BeautyShop.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;
        private readonly AprioriService _apriori;

        public HomeController(
            ILogger<HomeController> logger,
            ApplicationDbContext context,
            AprioriService apriori)
        {
            _logger = logger;
            _context = context;
            _apriori = apriori;
        }

        public IActionResult Index()
        {
            HomeViewModel vm = new HomeViewModel();

            vm.NewProducts = _context.Products
                .Include(p => p.Brand)
                .Include(p => p.Category)
                .Include(x => x.ProductVariants!)
                    .ThenInclude(v => v.Inventories)
                .Where(p => p.IsActive)
                .OrderByDescending(p => p.Id)
                .Take(8)
                .ToList();

            vm.FeaturedProducts = _context.Products
                .Include(p => p.Brand)
                .Include(p => p.Category)
                .Include(x => x.ProductVariants!)
                    .ThenInclude(v => v.Inventories)
                .Where(p => p.IsActive)
                .Take(8)
                .ToList();

            vm.Brands = _context.Brands
                .Take(8)
                .ToList();


            // --- LẤY SẢN PHẨM BÁN CHẠY DỰA TRÊN SỐ LƯỢNG ĐÃ BÁN ---
            var bestSellerIds = _context.OrderDetails
                .Join(_context.ProductVariants,
                      od => od.VariantId,
                      v => v.Id,
                      (od, v) => new { od.Quantity, v.ProductId })
                .GroupBy(x => x.ProductId)
                .Select(g => new {
                    ProductId = g.Key,
                    TotalSold = g.Sum(x => x.Quantity)
                })
                .OrderByDescending(x => x.TotalSold)
                .Take(8)
                .Select(x => x.ProductId)
                .ToList();

            if (!bestSellerIds.Any())
            {
                bestSellerIds = _context.Products
                    .Where(p => p.IsActive)
                    .OrderByDescending(p => p.Id)
                    .Take(8)
                    .Select(p => p.Id)
                    .ToList();
            }

            var bestSellerProducts = _context.Products
                .Include(p => p.Brand)
                .Include(p => p.Category)
                .Include(x => x.ProductVariants!)
                    .ThenInclude(v => v.Inventories)
                .Where(p => bestSellerIds.Contains(p.Id))
                .ToList();

            bestSellerProducts = bestSellerProducts
                .OrderBy(p => bestSellerIds.IndexOf(p.Id))
                .ToList();

            ViewBag.BestSellers = bestSellerProducts;

            // --- THUAT TOAN TINH TOAN GOI Y (RECOMMENDATION) CHO TRANG CHU ---
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId != null)
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
                        .Select(g => new { CategoryId = g.Key, Score = g.Sum(x => x.ActionType == "View" ? 1 : 3) })
                        .OrderByDescending(x => x.Score)
                        .FirstOrDefault();

                    var topBrand = behaviors
                        .Where(x => x.Product != null && x.Product.BrandId != null)
                        .GroupBy(x => x.Product.BrandId)
                        .Select(g => new { BrandId = g.Key, Score = g.Sum(x => x.ActionType == "View" ? 1 : 3) })
                        .OrderByDescending(x => x.Score)
                        .FirstOrDefault();

                    int? topCatId = topCategory?.CategoryId;
                    int? topBrnId = topBrand?.BrandId;

                    vm.RecommendedProducts = _context.Products
                        .Include(p => p.Brand)
                        .Include(p => p.Category)
                        .Include(x => x.ProductVariants!)
                    .ThenInclude(v => v.Inventories)
                        .Where(p => p.IsActive && (p.CategoryId == topCatId || p.BrandId == topBrnId))
                        .OrderByDescending(p => p.CategoryId == topCatId)
                        .ThenByDescending(p => p.BrandId == topBrnId)
                        .Take(4)
                        .ToList();
                }
            }


            // =============================================
            // COMBO THONG MINH - THUAT TOAN APRIORI
            // =============================================
            vm.SmartCombos = _apriori.FindFrequentCombos(
                minSupport: 2,
                maxComboSize: 3,
                maxCombos: 4
            );

            return View(vm);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(
            Duration = 0,
            Location = ResponseCacheLocation.None,
            NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ??
                            HttpContext.TraceIdentifier
            });
        }

    }
}