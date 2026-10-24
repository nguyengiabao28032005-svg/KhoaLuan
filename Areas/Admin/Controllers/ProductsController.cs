using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using BeautyShop.Data;
using BeautyShop.Models;
using BeautyShop.Services;
using BeautyShop.ViewModels;

namespace BeautyShop.Areas.Admin.Controllers
{
    [Area("Admin")]
    [RequirePermission(AppPermissions.ProductManage)]
    public class ProductsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IProductService _productService;

        public ProductsController(ApplicationDbContext context, IProductService productService)
        {
            _context = context;
            _productService = productService;
        }

        // GET: Admin/Products
        public async Task<IActionResult> Index(string keyword, int? categoryId, int? brandId)
        {
            var query = _context.Products
                .Include(x => x.Brand)
                .Include(x => x.Category)
                .Include(x => x.ProductVariants)
                .AsQueryable();

            if (!string.IsNullOrEmpty(keyword))
            {
                query = query.Where(x =>
                    x.Name.Contains(keyword) ||
                    x.ProductVariants.Any(v => v.SKU.Contains(keyword)));
            }

            if (categoryId.HasValue)
            {
                query = query.Where(x => x.CategoryId == categoryId.Value);
            }

            if (brandId.HasValue)
            {
                query = query.Where(x => x.BrandId == brandId.Value);
            }

            ViewBag.CurrentSearch = keyword;
            ViewBag.Categories = new SelectList(_context.Categories, "Id", "Name", categoryId);
            ViewBag.Brands = new SelectList(_context.Brands, "Id", "Name", brandId);

            return View(await query.ToListAsync());
        }

        // GET: Admin/Products/Details/5
        public IActionResult Detail(int id)
        {
            var product = _context.Products
                .Include(x => x.Brand)
                .Include(x => x.Category)
                .Include(x => x.ProductVariants)
                .FirstOrDefault(x => x.Id == id);

            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }

        // GET: Admin/Products/Create
        public IActionResult Create()
        {
            ViewData["BrandId"] = new SelectList(_context.Brands, "Id", "Name");
            ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Name");
            return View(new CreateProductViewModel());
        }

        // POST: Admin/Products/Create
        // Form tạo sản phẩm mới không chứa giá gốc, giá bán hay tồn kho (nghiệp vụ tách riêng)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateProductViewModel model)
        {
            if (ModelState.IsValid)
            {
                var result = await _productService.CreateProductAsync(model);
                if (result.Success)
                {
                    TempData["Success"] = result.Message;
                    return RedirectToAction(nameof(Index));
                }
                ModelState.AddModelError("", result.Message);
            }

            ViewData["BrandId"] = new SelectList(_context.Brands, "Id", "Name", model.BrandId);
            ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Name", model.CategoryId);
            return View(model);
        }

        // GET: Admin/Products/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            ViewData["BrandId"] = new SelectList(_context.Brands, "Id", "Name", product.BrandId);
            ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Name", product.CategoryId);
            return View(product);
        }

        // POST: Admin/Products/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Description,Ingredients,HowToUse,IsActive,CategoryId,BrandId,ThumbnailUrl")] Product product)
        {
            if (id != product.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(product);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = $"Cập nhật thông tin \"{product.Name}\" thành công!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ProductExists(product.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }

            ViewData["BrandId"] = new SelectList(_context.Brands, "Id", "Name", product.BrandId);
            ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Name", product.CategoryId);
            return View(product);
        }

        // GET: Admin/Products/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var product = await _context.Products
                .Include(p => p.Brand)
                .Include(p => p.Category)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }

        // POST: Admin/Products/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return RedirectToAction(nameof(Index));
            }

            try
            {
                _context.Products.Remove(product);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Xóa sản phẩm thành công";
            }
            catch
            {
                TempData["Error"] = "Không thể xóa sản phẩm vì đang có dữ liệu đơn hàng hoặc biến thể liên quan.";
            }

            return RedirectToAction(nameof(Index));
        }

        // =========================================================================
        // QUẢN LÝ BIẾN THỂ SẢN PHẨM (KHÔNG CHỨA GIÁ VÀ TỒN KHO)
        // =========================================================================

        // GET: Admin/Products/Variants?productId=5
        public async Task<IActionResult> Variants(int productId)
        {
            var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == productId);
            if (product == null)
            {
                return NotFound();
            }

            var variants = await _context.ProductVariants
                .Where(v => v.ProductId == productId)
                .ToListAsync();

            ViewBag.Variants = variants;
            return View(product);
        }

        // POST: Admin/Products/AddVariant
        // Thêm biến thể mới qua ProductService: không nhập giá, không nhập tồn kho
        [HttpPost]
        public async Task<IActionResult> AddVariant(CreateVariantViewModel model)
        {
            if (model.ProductId <= 0)
            {
                TempData["Error"] = "Mã sản phẩm không hợp lệ.";
                return RedirectToAction(nameof(Index));
            }

            var result = await _productService.CreateVariantAsync(model);
            if (result.Success)
            {
                TempData["Success"] = result.Message;
            }
            else
            {
                TempData["Error"] = result.Message;
            }

            return RedirectToAction(nameof(Variants), new { productId = model.ProductId });
        }

        // GET: Admin/Products/DeleteVariant/5?productId=2
        public async Task<IActionResult> DeleteVariant(int id, int productId)
        {
            var variant = await _context.ProductVariants.FindAsync(id);
            if (variant != null)
            {
                _context.ProductVariants.Remove(variant);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Đã xóa biến thể thành công.";
            }

            return RedirectToAction(nameof(Variants), new { productId = productId });
        }

        // =========================================================================
        // MÀN HÌNH THIẾT LẬP GIÁ BÁN (GIÁ BÁN LẺ & GIÁ BÁN SỈ)
        // Chỉ thiết lập khi biến thể đã có hàng trong kho
        // =========================================================================

        // GET: Admin/Products/Pricing
        [RequirePermission(AppPermissions.PriceSet)]
        public async Task<IActionResult> Pricing(int? productId, bool? onlyUnpriced)
        {
            var variants = await _productService.GetVariantsForPricingAsync(productId, onlyUnpriced);

            ViewBag.Products = new SelectList(await _context.Products.OrderBy(p => p.Name).ToListAsync(), "Id", "Name", productId);
            ViewBag.SelectedProductId = productId;
            ViewBag.OnlyUnpriced = onlyUnpriced ?? false;

            return View(variants);
        }

        // POST: Admin/Products/SetPricing
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission(AppPermissions.PriceSet)]
        public async Task<IActionResult> SetPricing(SetPricingViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var error = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage ?? "Dữ liệu nhập vào không hợp lệ.";
                TempData["Error"] = error;
                return RedirectToAction(nameof(Pricing));
            }

            var result = await _productService.SetVariantPricingAsync(model);
            if (result.Success)
            {
                TempData["Success"] = result.Message;
            }
            else
            {
                TempData["Error"] = result.Message;
            }

            return RedirectToAction(nameof(Pricing));
        }

        private bool ProductExists(int id)
        {
            return _context.Products.Any(e => e.Id == id);
        }
    }
}