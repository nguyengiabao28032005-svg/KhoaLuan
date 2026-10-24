using BeautyShop.Data;
using BeautyShop.Models;
using BeautyShop.Services;
using BeautyShop.ViewModels;
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
                else if (stockStatus == "not_stocked")
                    inventoriesQuery = inventoriesQuery.Where(i => false);
            }

            var inventoryList = await inventoriesQuery
                .OrderBy(i => i.Variant.Product.Name)
                .ThenBy(i => i.StoreId)
                .ToListAsync();

            // Tính số lượng hàng đang trên đường chuyển đến cho từng biến thể tại kho được chọn
            var incomingTransfersQuery = _context.StockTransfers
                .Where(t => t.Status == "InTransit");

            if (storeId.HasValue && storeId.Value > 0)
            {
                incomingTransfersQuery = incomingTransfersQuery.Where(t => t.ToStoreId == storeId.Value);
            }

            var incomingTransfers = await incomingTransfersQuery
                .SelectMany(t => t.Lines)
                .GroupBy(l => l.VariantId)
                .Select(g => new { VariantId = g.Key, IncomingQty = g.Sum(l => l.Quantity) })
                .ToDictionaryAsync(x => x.VariantId, x => x.IncomingQty);

            ViewBag.IncomingTransfers = incomingTransfers;

            bool isAdmin = await _userScope.IsAdminAsync();
            var allowedStoreIds = await _userScope.GetAllowedStoreIdsAsync();

            // Lấy các biến thể chưa có bản ghi Inventory tại kho được chọn (Mặt hàng chưa từng phân phối về kho này)
            var existingVariantIds = inventoryList.Select(i => i.VariantId).ToHashSet();
            List<BeautyShop.Models.ProductVariant> missingVariants = new();

            if (string.IsNullOrEmpty(stockStatus) || stockStatus == "not_stocked" || stockStatus == "incoming")
            {
                var missingQuery = _context.ProductVariants
                    .Include(v => v.Product)
                    .Where(v => v.Product != null && v.Product.IsActive && !existingVariantIds.Contains(v.Id));

                if (!string.IsNullOrEmpty(keyword))
                {
                    missingQuery = missingQuery.Where(v => v.Product!.Name.Contains(keyword) || v.SKU.Contains(keyword));
                }

                if (stockStatus == "incoming")
                {
                    var incomingIds = incomingTransfers.Keys.ToList();
                    missingQuery = missingQuery.Where(v => incomingIds.Contains(v.Id));
                }
                else if (stockStatus == "not_stocked")
                {
                    var incomingIds = incomingTransfers.Keys.ToList();
                    missingQuery = missingQuery.Where(v => !incomingIds.Contains(v.Id));
                }

                missingVariants = await missingQuery.OrderBy(v => v.Product!.Name).ToListAsync();
            }

            if (stockStatus == "incoming")
            {
                inventoryList = inventoryList.Where(i => incomingTransfers.ContainsKey(i.VariantId) || i.InTransitQuantity > 0).ToList();
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
            ViewBag.TotalCount = inventoryList.Count + missingVariants.Count;
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
        public async Task<IActionResult> Receipts(
            string? keyword,
            DateTime? fromDate,
            DateTime? toDate,
            string? supplier,
            int? supplierId,
            string? paymentStatus,
            string? receiptType,
            int? storeId)
        {
            var query = _context.StockReceipts
                .Include(r => r.Variant)
                    .ThenInclude(v => v.Product)
                .Include(r => r.CreatedByUser)
                .Include(r => r.Store)
                .Include(r => r.ImportPort)
                .Include(r => r.SupplierRef)
                .Include(r => r.Items)
                    .ThenInclude(i => i.Variant)
                        .ThenInclude(v => v.Product)
                .AsQueryable();

            // Bộ lọc phạm vi dữ liệu tập trung theo kho
            query = await _userScope.ApplyReceiptScopeAsync(query);

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                string kw = keyword.Trim().ToLower();
                query = query.Where(r =>
                    r.ReceiptCode.ToLower().Contains(kw) ||
                    (r.Supplier != null && r.Supplier.ToLower().Contains(kw)) ||
                    (r.InvoiceNumber != null && r.InvoiceNumber.ToLower().Contains(kw)) ||
                    (r.CustomsDeclarationNumber != null && r.CustomsDeclarationNumber.ToLower().Contains(kw)) ||
                    (r.CreatedByName != null && r.CreatedByName.ToLower().Contains(kw)) ||
                    r.Items.Any(i => i.Variant.Product.Name.ToLower().Contains(kw) || i.Variant.SKU.ToLower().Contains(kw)) ||
                    (r.Variant != null && (r.Variant.Product.Name.ToLower().Contains(kw) || r.Variant.SKU.ToLower().Contains(kw))));
            }

            if (supplierId.HasValue && supplierId.Value > 0)
            {
                query = query.Where(r => r.SupplierId == supplierId.Value);
            }
            else if (!string.IsNullOrWhiteSpace(supplier))
            {
                query = query.Where(r => r.Supplier != null && r.Supplier.Contains(supplier));
            }

            if (!string.IsNullOrWhiteSpace(paymentStatus))
            {
                query = query.Where(r => r.PaymentStatus == paymentStatus);
            }

            if (!string.IsNullOrWhiteSpace(receiptType))
            {
                query = query.Where(r => r.ReceiptType == receiptType);
            }

            if (storeId.HasValue && storeId.Value > 0)
            {
                query = query.Where(r => r.StoreId == storeId.Value);
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
            ViewBag.SupplierId = supplierId;
            ViewBag.PaymentStatus = paymentStatus;
            ViewBag.ReceiptType = receiptType;
            ViewBag.SelectedStoreId = storeId;
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");

            ViewBag.TotalReceipts = receipts.Count;
            ViewBag.TotalQuantity = receipts.Sum(r => r.Items.Any() ? r.Items.Sum(i => i.Quantity) : r.Quantity);
            ViewBag.TotalValue = receipts.Sum(r => r.TotalAmount > 0 ? r.TotalAmount : r.Quantity * r.ImportPrice);
            ViewBag.TotalPaid = receipts.Sum(r => r.PaidAmount > 0 ? r.PaidAmount : (r.PaymentStatus == "Paid" ? (r.TotalAmount > 0 ? r.TotalAmount : r.Quantity * r.ImportPrice) : 0));
            ViewBag.TotalDebt = receipts.Sum(r => r.DebtAmount > 0 ? r.DebtAmount : (r.PaymentStatus == "Unpaid" ? (r.TotalAmount > 0 ? r.TotalAmount : r.Quantity * r.ImportPrice) : 0));

            ViewBag.AllVariants = await _context.ProductVariants
                .Include(v => v.Product)
                .OrderBy(v => v.Product.Name)
                .ToListAsync();

            ViewBag.Categories = await _context.Categories
                .OrderBy(c => c.Name)
                .ToListAsync();

            ViewBag.Suppliers = await _context.Suppliers
                .Where(s => s.IsActive)
                .OrderBy(s => s.Name)
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
        // XEM CHI TIẾT PHIẾU NHẬP KHO (YÊU CẦU 1, 2, 5, 6, 7, 10)
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
                .Include(r => r.SupplierRef)
                .Include(r => r.Items)
                    .ThenInclude(i => i.Variant)
                        .ThenInclude(v => v.Product)
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

            var itemsList = r.Items.Any()
                ? r.Items.Select(i => new ReceiptItemDetailViewModel
                {
                    VariantId = i.VariantId,
                    ProductName = i.Variant?.Product?.Name ?? "N/A",
                    SKU = i.Variant?.SKU ?? "N/A",
                    ColorName = i.Variant?.ColorName,
                    Volume = i.Variant?.Volume,
                    CategoryName = i.Variant?.Product?.Category?.Name,
                    BrandName = i.Variant?.Product?.Brand?.Name,
                    Quantity = i.Quantity,
                    ImportPrice = i.ImportPrice,
                    TotalPrice = i.TotalPrice > 0 ? i.TotalPrice : i.Quantity * i.ImportPrice,
                    OldRetailPrice = i.OldRetailPrice,
                    NewRetailPrice = i.NewRetailPrice,
                    CurrentRetailPrice = i.Variant?.RetailPrice
                }).ToList()
                : new List<ReceiptItemDetailViewModel>
                {
                    new ReceiptItemDetailViewModel
                    {
                        VariantId = r.VariantId,
                        ProductName = r.Variant?.Product?.Name ?? "N/A",
                        SKU = r.Variant?.SKU ?? "N/A",
                        ColorName = r.Variant?.ColorName,
                        Volume = r.Variant?.Volume,
                        CategoryName = r.Variant?.Product?.Category?.Name,
                        BrandName = r.Variant?.Product?.Brand?.Name,
                        Quantity = r.Quantity,
                        ImportPrice = r.ImportPrice,
                        TotalPrice = r.Quantity * r.ImportPrice,
                        OldRetailPrice = null,
                        NewRetailPrice = null,
                        CurrentRetailPrice = r.Variant?.RetailPrice
                    }
                };

            decimal totalAmount = r.TotalAmount > 0 ? r.TotalAmount : r.Quantity * r.ImportPrice;
            decimal paidAmount = r.PaidAmount > 0 ? r.PaidAmount : (r.PaymentStatus == "Paid" ? totalAmount : 0);
            decimal debtAmount = r.DebtAmount > 0 ? r.DebtAmount : (r.PaymentStatus == "Unpaid" ? totalAmount : Math.Max(0, totalAmount - paidAmount));

            return Json(new
            {
                success = true,
                data = new
                {
                    id = r.Id,
                    receiptCode = r.ReceiptCode,
                    receiptType = r.ReceiptType,
                    receiptTypeLabel = r.ReceiptType == "Import" ? "Nhập khẩu quốc tế" : "Nhập trong nước",
                    importDate = r.ImportDate.ToString("dd/MM/yyyy HH:mm"),
                    createdAt = r.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                    createdByName = r.CreatedByName ?? "N/A",
                    supplierId = r.SupplierId,
                    supplier = r.Supplier ?? r.SupplierRef?.Name ?? "—",
                    supplierAddress = r.SupplierRef?.Address ?? "—",
                    supplierPhone = r.SupplierRef?.Phone ?? "—",
                    invoiceNumber = r.InvoiceNumber ?? "—",
                    originCountry = r.OriginCountry ?? "Việt Nam",
                    paymentStatus = r.PaymentStatus,
                    paymentStatusLabel = r.PaymentStatus == "Paid" ? "Đã thanh toán" : (r.PaymentStatus == "Unpaid" ? "Chưa thanh toán (Công nợ)" : "Thanh toán một phần"),
                    totalAmount = totalAmount,
                    paidAmount = paidAmount,
                    debtAmount = debtAmount,
                    storeId = r.StoreId,
                    storeName = r.Store?.Name ?? "Kho chính",
                    storeAddress = r.Store?.Address ?? "—",
                    storeProvince = r.Store?.ProvinceName ?? "—",
                    storeRegion = r.Store?.Region ?? "—",
                    storeType = r.Store?.StoreType ?? "—",
                    portName = r.ImportPort?.Name ?? "—",
                    portCode = r.ImportPort?.PortCode ?? "—",
                    portType = r.ImportPort?.PortType == "Sea" ? "Cảng biển" : (r.ImportPort?.PortType == "Air" ? "Sân bay quốc tế" : "—"),
                    customsDeclarationNumber = r.CustomsDeclarationNumber ?? "—",
                    note = r.Note ?? "—",
                    items = itemsList
                }
            });
        }

        // =========================================================================
        // TẠO PHIẾU NHẬP KHO NHIỀU SẢN PHẨM (YÊU CẦU 1, 2, 5, 6, 7, 10)
        // =========================================================================
        [HttpPost]
        public async Task<IActionResult> CreateMultiReceipt([FromBody] CreateStockReceiptViewModel model)
        {
            if (model == null)
            {
                return Json(new { success = false, message = "Dữ liệu phiếu nhập không hợp lệ." });
            }

            bool isAdmin = await _userScope.IsAdminAsync();
            if (!isAdmin)
            {
                var allowedStores = await _userScope.GetAllowedStoreIdsAsync();
                if (!allowedStores.Contains(model.StoreId))
                {
                    return Json(new { success = false, message = "Truy cập bị từ chối: Bạn không có phạm vi quản lý kho đã chọn." });
                }
            }

            int? userId = await _userScope.GetUserIdAsync();
            string userName = HttpContext?.Session?.GetString("FullName") ?? "Nhân viên kho";

            var result = await _inventoryService.CreateMultiItemReceiptAsync(model, userId, userName);
            return Json(new { success = result.Success, message = result.Message });
        }

        // =========================================================================
        // TẠO PHIẾU NHẬP KHO MỚI (DUY TRÌ TƯƠNG THÍCH NGƯỢC VỚI TESTS CŨ)
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

        // =========================================================================
        // API: LỌC SẢN PHẨM THEO LOẠI & NHÀ CUNG CẤP (YÊU CẦU 3 & 4)
        // =========================================================================
        [HttpGet]
        public async Task<IActionResult> GetProductsByCategoryAndSupplier(int? categoryId, int? supplierId)
        {
            var query = _context.ProductVariants
                .Include(v => v.Product)
                    .ThenInclude(p => p.Category)
                .Include(v => v.Product)
                    .ThenInclude(p => p.Brand)
                .AsQueryable();

            if (supplierId.HasValue && supplierId.Value > 0)
            {
                var suppliedProductIds = await _context.SupplierProducts
                    .Where(sp => sp.SupplierId == supplierId.Value)
                    .Select(sp => sp.ProductId)
                    .ToListAsync();

                query = query.Where(v => suppliedProductIds.Contains(v.ProductId));
            }

            if (categoryId.HasValue && categoryId.Value > 0)
            {
                query = query.Where(v => v.Product.CategoryId == categoryId.Value);
            }

            var list = await query
                .OrderBy(v => v.Product.Name)
                .ThenBy(v => v.SKU)
                .Select(v => new
                {
                    variantId = v.Id,
                    productId = v.ProductId,
                    productName = v.Product.Name,
                    sku = v.SKU,
                    colorName = v.ColorName,
                    volume = v.Volume,
                    categoryId = v.Product.CategoryId,
                    categoryName = v.Product.Category != null ? v.Product.Category.Name : "",
                    brandName = v.Product.Brand != null ? v.Product.Brand.Name : "",
                    currentRetailPrice = v.RetailPrice ?? 0,
                    originalPrice = v.OriginalPrice ?? 0
                })
                .ToListAsync();

            return Json(new { success = true, data = list });
        }

        // =========================================================================
        // API: LỊCH SỬ NHẬP CỦA NHÀ CUNG CẤP ĐỂ ĐỐI CHIẾU TRÁNH TRÙNG & SO SÁNH GIÁ (YÊU CẦU 8)
        // =========================================================================
        [HttpGet]
        public async Task<IActionResult> GetSupplierReceiptHistory(int supplierId)
        {
            var history = await _context.StockReceipts
                .Include(r => r.Items)
                    .ThenInclude(i => i.Variant)
                        .ThenInclude(v => v.Product)
                .Where(r => r.SupplierId == supplierId)
                .OrderByDescending(r => r.ImportDate)
                .Take(5)
                .Select(r => new
                {
                    receiptCode = r.ReceiptCode,
                    importDate = r.ImportDate.ToString("dd/MM/yyyy HH:mm"),
                    totalAmount = r.TotalAmount,
                    paymentStatus = r.PaymentStatus,
                    itemCount = r.Items.Count,
                    items = r.Items.Select(i => new
                    {
                        productName = i.Variant.Product.Name,
                        sku = i.Variant.SKU,
                        quantity = i.Quantity,
                        importPrice = i.ImportPrice,
                        totalPrice = i.TotalPrice
                    })
                })
                .ToListAsync();

            return Json(new { success = true, data = history });
        }

        // =========================================================================
        // API: GỢI Ý GIÁ NHẬP TỪ NƠI KHÁC / SẢN PHẨM TƯƠNG TỰ (YÊU CẦU 9)
        // =========================================================================
        [HttpGet]
        public async Task<IActionResult> GetPriceComparison(int variantId)
        {
            var comparisons = await _inventoryService.GetPriceComparisonAsync(variantId);
            return Json(new { success = true, data = comparisons });
        }

        // =========================================================================
        // CẬP NHẬT THANH TOÁN VÀ CÔNG NỢ (YÊU CẦU 5)
        // =========================================================================
        [HttpPost]
        public async Task<IActionResult> UpdatePayment(int receiptId, decimal paymentAmount, string? note)
        {
            var result = await _inventoryService.UpdatePaymentAsync(receiptId, paymentAmount, note);
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new { success = result.Success, message = result.Message });
            }

            if (result.Success)
            {
                TempData["SuccessMessage"] = result.Message;
            }
            else
            {
                TempData["ErrorMessage"] = result.Message;
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
