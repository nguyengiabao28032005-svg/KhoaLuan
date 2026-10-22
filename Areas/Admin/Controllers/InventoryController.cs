using BeautyShop.Data;
using BeautyShop.Models;
using BeautyShop.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BeautyShop.Areas.Admin.Controllers
{
    [Area("Admin")]
    [RequirePermission(AppPermissions.InventoryReceive)]
    public class InventoryController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IInventoryService _inventoryService;
        private readonly ICurrentUserScope _userScope;

        public InventoryController(
            ApplicationDbContext context,
            IInventoryService inventoryService,
            ICurrentUserScope userScope)
        {
            _context = context;
            _inventoryService = inventoryService;
            _userScope = userScope;
        }

        // Danh sach ton kho tat ca bien the san pham (lọc theo phạm vi kho của user)
        public async Task<IActionResult> Index(string keyword, string stockStatus, int? storeId)
        {
            var inventoriesQuery = _context.Inventories
                .Include(i => i.Variant)
                    .ThenInclude(v => v.Product)
                .Include(i => i.Store)
                .AsQueryable();

            // Bộ lọc phạm vi dữ liệu tập trung theo kho được gán (Admin thấy tất cả)
            inventoriesQuery = await _userScope.ApplyInventoryScopeAsync(inventoriesQuery);

            if (storeId.HasValue && storeId.Value > 0)
            {
                inventoriesQuery = inventoriesQuery.Where(i => i.StoreId == storeId.Value);
            }

            // Lọc theo tên sản phẩm hoặc SKU
            if (!string.IsNullOrEmpty(keyword))
            {
                inventoriesQuery = inventoriesQuery.Where(i =>
                    i.Variant.Product.Name.Contains(keyword) ||
                    i.Variant.SKU.Contains(keyword));
            }

            // Lọc theo trạng thái tồn kho
            if (!string.IsNullOrEmpty(stockStatus))
            {
                if (stockStatus == "out")
                    inventoriesQuery = inventoriesQuery.Where(i => i.StockQuantity == 0);
                else if (stockStatus == "low")
                    inventoriesQuery = inventoriesQuery.Where(i => i.StockQuantity > 0 && i.StockQuantity <= 5);
                else if (stockStatus == "ok")
                    inventoriesQuery = inventoriesQuery.Where(i => i.StockQuantity > 5);
            }

            var inventoryList = await inventoriesQuery
                .OrderBy(i => i.Variant.Product.Name)
                .ThenBy(i => i.StoreId)
                .ToListAsync();

            bool isAdmin = await _userScope.IsAdminAsync();
            var allowedStoreIds = await _userScope.GetAllowedStoreIdsAsync();

            // Lay them cac bien the chua co ban ghi Inventory (chỉ khi Admin xem và không lọc)
            var existingVariantIds = inventoryList.Select(i => i.VariantId).ToHashSet();
            List<BeautyShop.Models.ProductVariant> missingVariants = new();

            if (isAdmin && string.IsNullOrEmpty(keyword) && string.IsNullOrEmpty(stockStatus) && !storeId.HasValue)
            {
                missingVariants = await _context.ProductVariants
                    .Include(v => v.Product)
                    .Where(v => !existingVariantIds.Contains(v.Id))
                    .ToListAsync();
            }

            var scopedBaseQuery = await _userScope.ApplyInventoryScopeAsync(_context.Inventories.AsQueryable());
            if (storeId.HasValue && storeId.Value > 0)
            {
                scopedBaseQuery = scopedBaseQuery.Where(i => i.StoreId == storeId.Value);
            }

            ViewBag.MissingVariants = missingVariants;
            ViewBag.Keyword = keyword;
            ViewBag.CurrentStockStatus = stockStatus;
            ViewBag.SelectedStoreId = storeId;
            ViewBag.TotalCount = inventoryList.Count;
            ViewBag.OutOfStock = await scopedBaseQuery.CountAsync(i => i.StockQuantity == 0);
            ViewBag.LowStock = await scopedBaseQuery.CountAsync(i => i.StockQuantity > 0 && i.StockQuantity <= 5);
            ViewBag.AllVariants = await _context.ProductVariants
                .Include(v => v.Product)
                .OrderBy(v => v.Product.Name)
                .ToListAsync();

            var storesQuery = _context.Stores.Where(s => s.IsActive);
            if (!isAdmin)
            {
                storesQuery = storesQuery.Where(s => allowedStoreIds.Contains(s.Id));
            }
            ViewBag.Stores = await storesQuery.OrderBy(s => s.Id).ToListAsync();

            return View(inventoryList);
        }

        // =========================================================================
        // BẢNG NHẬP KHO: DANH SÁCH TOÀN BỘ PHIẾU NHẬP KHO (LỌC THEO PHẠM VI KHO)
        // =========================================================================
        public async Task<IActionResult> Receipts(string? keyword, DateTime? fromDate, DateTime? toDate, string? supplier)
        {
            var query = _context.StockReceipts
                .Include(r => r.Variant)
                    .ThenInclude(v => v.Product)
                .Include(r => r.CreatedByUser)
                .Include(r => r.Store)
                .Include(r => r.ImportPort)
                .AsQueryable();

            // Bộ lọc phạm vi dữ liệu tập trung theo kho
            query = await _userScope.ApplyReceiptScopeAsync(query);

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(r =>
                    r.ReceiptCode.Contains(keyword) ||
                    r.Variant.Product.Name.Contains(keyword) ||
                    r.Variant.SKU.Contains(keyword) ||
                    (r.CustomsDeclarationNumber != null && r.CustomsDeclarationNumber.Contains(keyword)) ||
                    (r.CreatedByName != null && r.CreatedByName.Contains(keyword)));
            }

            if (!string.IsNullOrWhiteSpace(supplier))
            {
                query = query.Where(r => r.Supplier != null && r.Supplier.Contains(supplier));
            }

            if (fromDate.HasValue)
            {
                var fDate = fromDate.Value.Date;
                query = query.Where(r => r.ImportDate >= fDate);
            }

            if (toDate.HasValue)
            {
                var tDate = toDate.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(r => r.ImportDate <= tDate);
            }

            var receipts = await query.OrderByDescending(r => r.ImportDate).ToListAsync();

            ViewBag.Keyword = keyword;
            ViewBag.Supplier = supplier;
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");

            ViewBag.TotalReceipts = receipts.Count;
            ViewBag.TotalQuantity = receipts.Sum(r => r.Quantity);
            ViewBag.TotalValue = receipts.Sum(r => r.Quantity * r.ImportPrice);

            ViewBag.AllVariants = await _context.ProductVariants
                .Include(v => v.Product)
                .OrderBy(v => v.Product.Name)
                .ToListAsync();

            bool isAdmin = await _userScope.IsAdminAsync();
            var allowedStoreIds = await _userScope.GetAllowedStoreIdsAsync();
            var storesQuery = _context.Stores.Where(s => s.IsActive);
            if (!isAdmin)
            {
                storesQuery = storesQuery.Where(s => allowedStoreIds.Contains(s.Id));
            }
            ViewBag.Stores = await storesQuery.OrderBy(s => s.Name).ToListAsync();

            ViewBag.ImportPorts = await _context.ImportPorts
                .OrderBy(p => p.ProvinceName)
                .ToListAsync();

            return View(receipts);
        }

        // =========================================================================
        // XEM CHI TIẾT PHIẾU NHẬP KHO (CHẶN TRUY CẬP TRỰC TIẾP ID KHO KHÁC)
        // =========================================================================
        [HttpGet]
        public async Task<IActionResult> GetReceiptDetail(int id)
        {
            var r = await _context.StockReceipts
                .Include(r => r.Variant)
                    .ThenInclude(v => v.Product)
                .Include(r => r.Store)
                .Include(r => r.ImportPort)
                .Include(r => r.CreatedByUser)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (r == null) return NotFound(new { success = false, message = "Không tìm thấy phiếu nhập kho." });

            bool isAdmin = await _userScope.IsAdminAsync();
            if (!isAdmin)
            {
                var allowedStores = await _userScope.GetAllowedStoreIdsAsync();
                if (!allowedStores.Contains(r.StoreId))
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new
                    {
                        success = false,
                        message = "Truy cập bị từ chối (403): Phiếu nhập kho này thuộc kho ngoài phạm vi được phân công của bạn."
                    });
                }
            }

            return Json(new
            {
                success = true,
                data = new
                {
                    receiptCode = r.ReceiptCode,
                    receiptType = r.ReceiptType,
                    receiptTypeLabel = r.ReceiptType == "Import" ? "Nhập khẩu quốc tế" : "Nhập trong nước",
                    importDate = r.ImportDate.ToString("dd/MM/yyyy HH:mm"),
                    createdAt = r.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                    createdByName = r.CreatedByName ?? "N/A",
                    productName = r.Variant?.Product?.Name ?? "N/A",
                    sku = r.Variant?.SKU ?? "N/A",
                    variantDetails = (!string.IsNullOrEmpty(r.Variant?.ColorName) ? $"Màu: {r.Variant.ColorName} " : "") + (!string.IsNullOrEmpty(r.Variant?.Volume) ? $"Dung tích: {r.Variant.Volume}" : ""),
                    quantity = r.Quantity,
                    importPrice = r.ImportPrice,
                    totalAmount = r.Quantity * r.ImportPrice,
                    supplier = r.Supplier ?? "—",
                    storeName = r.Store?.Name ?? "Kho chính",
                    storeAddress = r.Store?.Address ?? "—",
                    storeProvince = r.Store?.ProvinceName ?? "—",
                    storeRegion = r.Store?.Region ?? "—",
                    storeType = r.Store?.StoreType ?? "—",
                    portName = r.ImportPort?.Name ?? "—",
                    portCode = r.ImportPort?.PortCode ?? "—",
                    portType = r.ImportPort?.PortType == "Sea" ? "Cảng biển" : (r.ImportPort?.PortType == "Air" ? "Sân bay quốc tế" : "—"),
                    customsDeclarationNumber = r.CustomsDeclarationNumber ?? "—",
                    note = r.Note ?? "—"
                }
            });
        }

        // =========================================================================
        // TẠO PHIẾU NHẬP KHO MỚI (KIỂM TRA PHẠM VI KHO)
        // =========================================================================
        [HttpPost]
        public async Task<IActionResult> CreateReceipt(
            int variantId,
            int quantity,
            decimal importPrice,
            string? supplier,
            DateTime? importDate,
            string? note,
            int storeId = 1,
            string receiptType = "Domestic",
            int? importPortId = null,
            string? customsDeclarationNumber = null)
        {
            bool isAdmin = await _userScope.IsAdminAsync();
            if (!isAdmin)
            {
                var allowedStores = await _userScope.GetAllowedStoreIdsAsync();
                if (!allowedStores.Contains(storeId))
                {
                    Response.StatusCode = StatusCodes.Status403Forbidden;
                    TempData["ErrorMessage"] = "Truy cập bị từ chối: Bạn không có phạm vi quản lý kho đã chọn.";
                    return View("~/Views/Account/AccessDenied.cshtml");
                }
            }

            int? userId = await _userScope.GetUserIdAsync();
            string userName = HttpContext?.Session?.GetString("FullName") ?? "Nhân viên kho";

            var result = await _inventoryService.CreateReceiptAsync(variantId, quantity, importPrice, supplier, importDate, userId, userName, note, storeId, receiptType, importPortId, customsDeclarationNumber);

            if (result.Success)
            {
                TempData["SuccessMessage"] = result.Message;
            }
            else
            {
                TempData["ErrorMessage"] = result.Message;
            }

            string? returnUrl = Request?.Headers["Referer"].ToString();
            if (!string.IsNullOrEmpty(returnUrl) && returnUrl.Contains("/Admin/Inventory"))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction(nameof(Receipts));
        }

        // Nhap/xuat kho thu cong hoac cap nhat so luong
        [HttpPost]
        public async Task<IActionResult> Adjust(int variantId, int quantity, string note, string changeType = "Import", int storeId = 1)
        {
            if (quantity == 0)
            {
                TempData["ErrorMessage"] = "Số lượng phải khác 0.";
                return RedirectToAction("Index");
            }

            bool isAdmin = await _userScope.IsAdminAsync();
            if (!isAdmin)
            {
                var allowedStores = await _userScope.GetAllowedStoreIdsAsync();
                if (!allowedStores.Contains(storeId))
                {
                    Response.StatusCode = StatusCodes.Status403Forbidden;
                    return View("~/Views/Account/AccessDenied.cshtml");
                }
            }

            var store = await _context.Stores.FirstOrDefaultAsync(s => s.Id == storeId) ?? await _context.Stores.FirstOrDefaultAsync();
            if (store == null)
            {
                store = new Store { Name = "Kho chính", Address = "BeautyShop HQ", IsActive = true };
                _context.Stores.Add(store);
                await _context.SaveChangesAsync();
            }

            var inventory = await _context.Inventories
                .FirstOrDefaultAsync(i => i.VariantId == variantId && i.StoreId == store.Id);

            if (inventory == null)
            {
                inventory = new Inventory
                {
                    StoreId       = store.Id,
                    VariantId     = variantId,
                    StockQuantity = 0,
                    ReservedQuantity = 0
                };
                _context.Inventories.Add(inventory);
                await _context.SaveChangesAsync();
            }

            int before = inventory.StockQuantity;
            inventory.StockQuantity = Math.Max(0, inventory.StockQuantity + quantity);
            int actualChange = inventory.StockQuantity - before;

            string userName = HttpContext?.Session?.GetString("FullName") ?? "Nhân viên";

            _context.InventoryLogs.Add(new InventoryLog
            {
                InventoryId     = inventory.Id,
                ChangeType      = changeType,
                QuantityChanged = actualChange,
                Note            = (string.IsNullOrEmpty(note) ? $"{changeType} thủ công" : note) + $" (bởi {userName})",
                CreatedAt       = DateTime.Now
            });

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Cập nhật tồn kho thành công. Số lượng hiện tại: {inventory.StockQuantity}";
            return RedirectToAction("Index");
        }

        // Dat lai so luong chinh xac (set = value)
        [HttpPost]
        public async Task<IActionResult> SetQuantity(int variantId, int quantity, string note, int storeId = 1)
        {
            bool isAdmin = await _userScope.IsAdminAsync();
            if (!isAdmin)
            {
                var allowedStores = await _userScope.GetAllowedStoreIdsAsync();
                if (!allowedStores.Contains(storeId))
                {
                    Response.StatusCode = StatusCodes.Status403Forbidden;
                    return View("~/Views/Account/AccessDenied.cshtml");
                }
            }

            string userName = HttpContext?.Session?.GetString("FullName") ?? "Quản trị viên";

            var store = await _context.Stores.FirstOrDefaultAsync(s => s.Id == storeId) ?? await _context.Stores.FirstOrDefaultAsync();
            if (store == null)
            {
                store = new Store { Name = "Kho chính", Address = "BeautyShop HQ", IsActive = true };
                _context.Stores.Add(store);
                await _context.SaveChangesAsync();
            }

            var inventory = await _context.Inventories
                .FirstOrDefaultAsync(i => i.VariantId == variantId && i.StoreId == store.Id);

            int before = inventory?.StockQuantity ?? 0;

            if (inventory == null)
            {
                inventory = new Inventory
                {
                    StoreId          = store.Id,
                    VariantId        = variantId,
                    StockQuantity    = quantity,
                    ReservedQuantity = 0
                };
                _context.Inventories.Add(inventory);
            }
            else
            {
                inventory.StockQuantity = quantity;
            }

            await _context.SaveChangesAsync();

            _context.InventoryLogs.Add(new InventoryLog
            {
                InventoryId     = inventory.Id,
                ChangeType      = "SetQuantity",
                QuantityChanged = quantity - before,
                Note            = (string.IsNullOrEmpty(note) ? "Điều chỉnh tồn kho" : note) + $" (bởi {userName})",
                CreatedAt       = DateTime.Now
            });
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã cập nhật số lượng tồn kho thành {quantity}.";
            return RedirectToAction("Index");
        }

        // Lich su thay doi ton kho cua 1 bien the (lọc theo phạm vi kho)
        public async Task<IActionResult> Logs(int variantId, int? storeId)
        {
            var variant = await _context.ProductVariants
                .Include(v => v.Product)
                .FirstOrDefaultAsync(v => v.Id == variantId);

            if (variant == null) return NotFound();

            var invQuery = _context.Inventories.Where(i => i.VariantId == variantId);
            invQuery = await _userScope.ApplyInventoryScopeAsync(invQuery);
            if (storeId.HasValue)
            {
                invQuery = invQuery.Where(i => i.StoreId == storeId.Value);
            }

            var inventory = await invQuery.FirstOrDefaultAsync();
            if (inventory == null && !(await _userScope.IsAdminAsync()))
            {
                Response.StatusCode = StatusCodes.Status403Forbidden;
                return View("~/Views/Account/AccessDenied.cshtml");
            }

            var logs = inventory != null
                ? await _context.InventoryLogs
                    .Where(l => l.InventoryId == inventory.Id)
                    .OrderByDescending(l => l.CreatedAt)
                    .ToListAsync()
                : new List<InventoryLog>();

            ViewBag.Variant  = variant;
            ViewBag.Inventory = inventory;
            return View(logs);
        }
    }
}
