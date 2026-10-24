using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeautyShop.Data;
using BeautyShop.Models;
using BeautyShop.Services;

namespace BeautyShop.Areas.Admin.Controllers
{
    [Area("Admin")]
    [RequirePermission(AppPermissions.InventoryReceive)]
    public class SuppliersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SuppliersController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =========================================================================
        // DANH SÁCH NHÀ CUNG CẤP (YÊU CẦU 2 & 3)
        // =========================================================================
        public async Task<IActionResult> Index(string? keyword, string? country, bool? isActive)
        {
            var query = _context.Suppliers
                .Include(s => s.SupplierProducts)
                    .ThenInclude(sp => sp.Product)
                .Include(s => s.Receipts)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                string kw = keyword.Trim().ToLower();
                query = query.Where(s =>
                    s.Name.ToLower().Contains(kw) ||
                    (s.Phone != null && s.Phone.Contains(kw)) ||
                    s.Address.ToLower().Contains(kw) ||
                    (s.ContactPerson != null && s.ContactPerson.ToLower().Contains(kw)) ||
                    (s.TaxCode != null && s.TaxCode.ToLower().Contains(kw)));
            }

            if (!string.IsNullOrWhiteSpace(country))
            {
                query = query.Where(s => s.Country == country);
            }

            if (isActive.HasValue)
            {
                query = query.Where(s => s.IsActive == isActive.Value);
            }

            var suppliers = await query.OrderByDescending(s => s.CreatedAt).ToListAsync();

            ViewBag.Keyword = keyword;
            ViewBag.Country = country;
            ViewBag.IsActive = isActive;

            // Thống kê nhanh
            ViewBag.TotalSuppliers = suppliers.Count;
            ViewBag.ActiveSuppliers = suppliers.Count(s => s.IsActive);
            ViewBag.TotalDebt = suppliers.Sum(s => s.Receipts.Sum(r => r.DebtAmount));
            ViewBag.Countries = await _context.Suppliers.Select(s => s.Country).Distinct().ToListAsync();

            return View(suppliers);
        }

        // =========================================================================
        // TẠO NHÀ CUNG CẤP MỚI (YÊU CẦU 2: CẦN LƯU ĐỊA CHỈ ĐỂ PHẢN HỒI KHI HÀNG LỖI)
        // =========================================================================
        [HttpGet]
        public IActionResult Create()
        {
            return View(new Supplier { Country = "Việt Nam", IsActive = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Supplier supplier)
        {
            if (string.IsNullOrWhiteSpace(supplier.Address))
            {
                ModelState.AddModelError("Address", "Địa chỉ nhà cung cấp là bắt buộc để gửi phản hồi / khiếu nại và đổi trả khi hàng lỗi.");
            }

            if (await _context.Suppliers.AnyAsync(s => s.Name.ToLower() == supplier.Name.Trim().ToLower()))
            {
                ModelState.AddModelError("Name", "Nhà cung cấp có tên này đã tồn tại trong hệ thống.");
            }

            if (ModelState.IsValid)
            {
                supplier.Name = supplier.Name.Trim();
                supplier.Address = supplier.Address.Trim();
                supplier.CreatedAt = DateTime.Now;

                _context.Suppliers.Add(supplier);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Đã thêm thành công nhà cung cấp \"{supplier.Name}\".";
                return RedirectToAction(nameof(Details), new { id = supplier.Id });
            }

            return View(supplier);
        }

        // =========================================================================
        // SỬA THÔNG TIN NHÀ CUNG CẤP
        // =========================================================================
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier == null) return NotFound();
            return View(supplier);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Supplier model)
        {
            if (id != model.Id) return BadRequest();

            if (string.IsNullOrWhiteSpace(model.Address))
            {
                ModelState.AddModelError("Address", "Địa chỉ nhà cung cấp là bắt buộc để phản hồi khi hàng lỗi.");
            }

            if (await _context.Suppliers.AnyAsync(s => s.Name.ToLower() == model.Name.Trim().ToLower() && s.Id != id))
            {
                ModelState.AddModelError("Name", "Tên nhà cung cấp này đã được sử dụng bởi đơn vị khác.");
            }

            if (ModelState.IsValid)
            {
                var supplier = await _context.Suppliers.FindAsync(id);
                if (supplier == null) return NotFound();

                supplier.Name = model.Name.Trim();
                supplier.ContactPerson = model.ContactPerson?.Trim();
                supplier.Email = model.Email?.Trim();
                supplier.Phone = model.Phone?.Trim();
                supplier.Address = model.Address.Trim();
                supplier.TaxCode = model.TaxCode?.Trim();
                supplier.Country = string.IsNullOrWhiteSpace(model.Country) ? "Việt Nam" : model.Country.Trim();
                supplier.IsActive = model.IsActive;
                supplier.Note = model.Note?.Trim();

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Đã cập nhật thông tin nhà cung cấp \"{supplier.Name}\".";
                return RedirectToAction(nameof(Details), new { id = supplier.Id });
            }

            return View(model);
        }

        // =========================================================================
        // CHI TIẾT NHÀ CUNG CẤP & BẢNG CUNG ỨNG (YÊU CẦU 2 & 3 & 8)
        // =========================================================================
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var supplier = await _context.Suppliers
                .Include(s => s.SupplierProducts)
                    .ThenInclude(sp => sp.Product)
                        .ThenInclude(p => p.Category)
                .Include(s => s.SupplierProducts)
                    .ThenInclude(sp => sp.Product)
                        .ThenInclude(p => p.Brand)
                .Include(s => s.Receipts)
                    .ThenInclude(r => r.Store)
                .Include(s => s.Receipts)
                    .ThenInclude(r => r.Items)
                        .ThenInclude(i => i.Variant)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (supplier == null) return NotFound();

            // Lấy danh sách sản phẩm chưa được gán vào bảng cung ứng của NCC này
            var suppliedProductIds = supplier.SupplierProducts.Select(sp => sp.ProductId).ToList();
            ViewBag.AvailableProducts = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Brand)
                .Where(p => !suppliedProductIds.Contains(p.Id))
                .OrderBy(p => p.Name)
                .ToListAsync();

            return View(supplier);
        }

        // =========================================================================
        // BỔ SUNG SẢN PHẨM VÀO BẢNG CUNG ỨNG (YÊU CẦU 3)
        // =========================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddProduct(int supplierId, int productId, decimal? suppliedPrice, string? notes)
        {
            var supplier = await _context.Suppliers.FindAsync(supplierId);
            if (supplier == null) return NotFound();

            var product = await _context.Products.FindAsync(productId);
            if (product == null) return NotFound();

            bool exists = await _context.SupplierProducts.AnyAsync(sp => sp.SupplierId == supplierId && sp.ProductId == productId);
            if (!exists)
            {
                _context.SupplierProducts.Add(new SupplierProduct
                {
                    SupplierId = supplierId,
                    ProductId = productId,
                    SuppliedPrice = suppliedPrice,
                    Notes = notes?.Trim(),
                    CreatedAt = DateTime.Now
                });
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Đã thêm sản phẩm \"{product.Name}\" vào danh mục cung ứng của {supplier.Name}.";
            }

            return RedirectToAction(nameof(Details), new { id = supplierId });
        }

        // =========================================================================
        // XÓA SẢN PHẨM KHỎI BẢNG CUNG ỨNG
        // =========================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveProduct(int id, int supplierId)
        {
            var item = await _context.SupplierProducts.FirstOrDefaultAsync(sp => sp.Id == id && sp.SupplierId == supplierId);
            if (item != null)
            {
                _context.SupplierProducts.Remove(item);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Đã hủy liên kết sản phẩm khỏi danh mục cung ứng của nhà cung cấp.";
            }

            return RedirectToAction(nameof(Details), new { id = supplierId });
        }

        // =========================================================================
        // BẬT / TẮT TRẠNG THÁI HOẠT ĐỘNG
        // =========================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier == null) return NotFound();

            supplier.IsActive = !supplier.IsActive;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã {(supplier.IsActive ? "kích hoạt" : "tạm ngưng")} nhà cung cấp \"{supplier.Name}\".";
            return RedirectToAction(nameof(Index));
        }

        // =========================================================================
        // API: LẤY DANH SÁCH SẢN PHẨM ĐƯỢC CUNG CẤP BỞI NCC (YÊU CẦU 3)
        // =========================================================================
        [HttpGet]
        public async Task<IActionResult> GetProductsBySupplier(int supplierId)
        {
            var products = await _context.SupplierProducts
                .Include(sp => sp.Product)
                    .ThenInclude(p => p.Category)
                .Include(sp => sp.Product)
                    .ThenInclude(p => p.ProductVariants)
                .Where(sp => sp.SupplierId == supplierId)
                .Select(sp => new
                {
                    id = sp.Product.Id,
                    name = sp.Product.Name,
                    categoryId = sp.Product.CategoryId,
                    categoryName = sp.Product.Category != null ? sp.Product.Category.Name : "",
                    suppliedPrice = sp.SuppliedPrice,
                    variants = sp.Product.ProductVariants != null ? sp.Product.ProductVariants.Select(v => new
                    {
                        variantId = v.Id,
                        sku = v.SKU,
                        colorName = v.ColorName,
                        volume = v.Volume,
                        retailPrice = v.RetailPrice,
                        originalPrice = v.OriginalPrice
                    }) : null
                })
                .ToListAsync();

            return Json(new { success = true, data = products });
        }
    }
}
