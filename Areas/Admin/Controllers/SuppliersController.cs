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
                    .ThenInclude(sp => sp.Variant).ThenInclude(v => v.Product)
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
                    .ThenInclude(sp => sp.Variant).ThenInclude(v => v.Product)
                        .ThenInclude(p => p.Category)
                .Include(s => s.SupplierProducts)
                    .ThenInclude(sp => sp.Variant).ThenInclude(v => v.Product)
                        .ThenInclude(p => p.Brand)
                .Include(s => s.Receipts)
                    .ThenInclude(r => r.Store)
                .Include(s => s.Receipts)
                    .ThenInclude(r => r.Items)
                        .ThenInclude(i => i.Variant)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (supplier == null) return NotFound();

            // Lấy danh sách biến thể chưa được gán vào bảng cung ứng của NCC này
            var suppliedVariantIds = supplier.SupplierProducts.Select(sp => sp.VariantId).ToList();
            ViewBag.AvailableVariants = await _context.ProductVariants
                .Include(v => v.Product)
                    .ThenInclude(p => p.Category)
                .Include(v => v.Product)
                    .ThenInclude(p => p.Brand)
                .Where(v => !suppliedVariantIds.Contains(v.Id))
                .OrderBy(v => v.Product.Name)
                .ThenBy(v => v.SKU)
                .ToListAsync();

            return View(supplier);
        }

        // =========================================================================
        // BỔ SUNG SẢN PHẨM VÀO BẢNG CUNG ỨNG (YÊU CẦU 3)
        // =========================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddVariant(int supplierId, int variantId, decimal? suppliedPrice, string? notes)
        {
            var supplier = await _context.Suppliers.FindAsync(supplierId);
            if (supplier == null) return NotFound();

            var variant = await _context.ProductVariants.Include(v => v.Product).FirstOrDefaultAsync(v => v.Id == variantId);
            if (variant == null) return NotFound();

            bool exists = await _context.SupplierProducts.AnyAsync(sp => sp.SupplierId == supplierId && sp.VariantId == variantId);
            if (!exists)
            {
                _context.SupplierProducts.Add(new SupplierProduct
                {
                    SupplierId = supplierId,
                    VariantId = variantId,
                    SuppliedPrice = suppliedPrice,
                    Notes = notes?.Trim(),
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                });
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Đã thêm biến thể \"{variant.Product?.Name} ({variant.SKU})\" vào danh mục cung ứng của {supplier.Name}.";
            }

            return RedirectToAction(nameof(Details), new { id = supplierId });
        }

        // Action quản lý bảng giá cung ứng theo biến thể
        public async Task<IActionResult> SupplierProducts(int? supplierId, int? categoryId, string? searchTerm, bool hasMultipleVariants = false)
        {
            var query = _context.SupplierProducts
                .Include(sp => sp.Supplier)
                .Include(sp => sp.Variant)
                    .ThenInclude(v => v.Product)
                        .ThenInclude(p => p.Category)
                .Include(sp => sp.Variant)
                    .ThenInclude(v => v.Product)
                        .ThenInclude(p => p.Brand)
                .AsQueryable();

            if (supplierId.HasValue && supplierId.Value > 0)
            {
                query = query.Where(sp => sp.SupplierId == supplierId.Value);
            }

            if (categoryId.HasValue && categoryId.Value > 0)
            {
                query = query.Where(sp => sp.Variant != null && sp.Variant.Product != null && sp.Variant.Product.CategoryId == categoryId.Value);
            }

            if (hasMultipleVariants)
            {
                var multiProductIds = await _context.ProductVariants
                    .GroupBy(v => v.ProductId)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToListAsync();

                query = query.Where(sp => sp.Variant != null && multiProductIds.Contains(sp.Variant.ProductId));
            }

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                string kw = searchTerm.Trim().ToLower();
                query = query.Where(sp =>
                    (sp.Variant != null && sp.Variant.SKU.ToLower().Contains(kw)) ||
                    (sp.Variant != null && sp.Variant.Product != null && sp.Variant.Product.Name.ToLower().Contains(kw)) ||
                    (sp.Supplier != null && sp.Supplier.Name.ToLower().Contains(kw)));
            }

            var list = await query
                .OrderBy(sp => sp.Supplier.Name)
                .ThenBy(sp => sp.Variant.Product.Name)
                .ThenBy(sp => sp.Variant.Volume)
                .ToListAsync();

            ViewBag.Suppliers = await _context.Suppliers.OrderBy(s => s.Name).ToListAsync();
            ViewBag.Categories = await _context.Categories.OrderBy(c => c.Name).ToListAsync();
            ViewBag.CurrentSupplierId = supplierId;
            ViewBag.CurrentCategoryId = categoryId;
            ViewBag.CurrentSearchTerm = searchTerm;
            ViewBag.HasMultipleVariants = hasMultipleVariants;

            return View(list);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateVariantPrice(int id, decimal suppliedPrice, string? notes)
        {
            if (suppliedPrice <= 0)
            {
                TempData["ErrorMessage"] = "Đơn giá cung ứng phải là số lớn hơn 0 VNĐ.";
                return RedirectToAction(nameof(SupplierProducts));
            }

            var sp = await _context.SupplierProducts.FindAsync(id);
            if (sp == null) return NotFound();

            sp.SuppliedPrice = suppliedPrice;
            if (!string.IsNullOrWhiteSpace(notes))
            {
                sp.Notes = notes.Trim();
            }
            sp.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Đã cập nhật giá cung ứng biến thể thành công.";
            return RedirectToAction(nameof(SupplierProducts), new { supplierId = sp.SupplierId });
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
            var variants = await _context.SupplierProducts
                .Include(sp => sp.Variant)
                    .ThenInclude(v => v.Product)
                        .ThenInclude(p => p.Category)
                .Where(sp => sp.SupplierId == supplierId && sp.Variant != null && sp.Variant.Product != null)
                .Select(sp => new
                {
                    id = sp.Variant.Product.Id,
                    variantId = sp.VariantId,
                    name = sp.Variant.Product.Name,
                    sku = sp.Variant.SKU,
                    colorName = sp.Variant.ColorName,
                    volume = sp.Variant.Volume,
                    categoryId = sp.Variant.Product.CategoryId,
                    categoryName = sp.Variant.Product.Category != null ? sp.Variant.Product.Category.Name : "",
                    suppliedPrice = sp.SuppliedPrice
                })
                .ToListAsync();

            return Json(new { success = true, data = variants });
        }
    }
}
