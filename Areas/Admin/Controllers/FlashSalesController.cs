using BeautyShop.Data;
using BeautyShop.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BeautyShop.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class FlashSalesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public FlashSalesController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            var data = _context.FlashSales
                .OrderByDescending(x => x.Id)
                .ToList();

            return View(data);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Create(FlashSale model)
        {
            if (!ModelState.IsValid)
                return View(model);

            _context.FlashSales.Add(model);
            _context.SaveChanges();

            return RedirectToAction(nameof(Index));
        }

        public IActionResult ToggleStatus(int id)
        {
            var flash = _context.FlashSales.Find(id);

            if (flash == null)
                return RedirectToAction(nameof(Index));

            flash.IsActive = !flash.IsActive;

            _context.SaveChanges();

            return RedirectToAction(nameof(Index));
        }

        public IActionResult Delete(int id)
        {
            var flash = _context.FlashSales.Find(id);

            if (flash != null)
            {
                _context.FlashSales.Remove(flash);
                _context.SaveChanges();
            }

            return RedirectToAction(nameof(Index));
        }
        public IActionResult ManageItems(int id)
        {
            var flashSale = _context.FlashSales
                .Include(x => x.FlashSaleItems)
                .ThenInclude(x => x.Variant)
                .ThenInclude(x => x.Product)
                .FirstOrDefault(x => x.Id == id);

            if (flashSale == null)
                return NotFound();

            ViewBag.FlashSaleId = id;

            ViewBag.Variants = _context.ProductVariants
                .Include(x => x.Product)
                .ToList();

            return View(flashSale);
        }
        [HttpPost]
        public IActionResult AddItem(
    int flashSaleId,
    int variantId,
    decimal promotionPrice,
    int quantityLimit,
    int maxQuantityPerUser)
        {
            FlashSaleItem item = new FlashSaleItem
            {
                FlashSaleId = flashSaleId,
                VariantId = variantId,
                PromotionPrice = promotionPrice,
                QuantityLimit = quantityLimit,
                MaxQuantityPerUser = maxQuantityPerUser,
                SoldQuantity = 0
            };

            _context.FlashSaleItems.Add(item);

            _context.SaveChanges();

            return RedirectToAction(
                nameof(ManageItems),
                new { id = flashSaleId });
        }
    }
}