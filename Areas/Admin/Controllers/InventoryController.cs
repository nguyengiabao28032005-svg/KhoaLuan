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
        private readonly ReceiptSettings _receiptSettings;
        private readonly ICurrentUserScope _userScope;

        public InventoryController(
            ApplicationDbContext context,
            IInventoryService inventoryService,
            ICurrentUserScope userScope,
            Microsoft.Extensions.Options.IOptions<ReceiptSettings>? receiptSettings = null)
        {
            _context = context;
            _inventoryService = inventoryService;
            _userScope = userScope;
            _receiptSettings = receiptSettings?.Value ?? new ReceiptSettings();
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
            int? storeId,
            string? stage)
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
                    (r.SupplierDeliveryNoteCode != null && r.SupplierDeliveryNoteCode.ToLower().Contains(kw)) ||
                    (r.CustomsDeclarationNumber != null && r.CustomsDeclarationNumber.ToLower().Contains(kw)) ||
                    (r.CreatedByName != null && r.CreatedByName.ToLower().Contains(kw)) ||
                    r.Items.Any(i => i.Variant.Product.Name.ToLower().Contains(kw) || i.Variant.SKU.ToLower().Contains(kw)) ||
                    (r.Variant != null && (r.Variant.Product.Name.ToLower().Contains(kw) || r.Variant.SKU.ToLower().Contains(kw))));
            }

            if (!string.IsNullOrWhiteSpace(stage) && stage != "all")
            {
                if (stage == "orders")
                {
                    query = query.Where(r => r.ReceiptStage == "PendingApproval" || r.ReceiptStage == "Approved" || r.ReceiptStage == "Ordered");
                }
                else if (stage == "received")
                {
                    query = query.Where(r => r.ReceiptStage == "Received" || r.ReceiptStage == null);
                }
                else
                {
                    query = query.Where(r => r.ReceiptStage == stage);
                }
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
            ViewBag.Stage = stage;
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");

            ViewBag.TotalReceipts = receipts.Count;
            ViewBag.TotalQuantity = receipts.Sum(r => r.Items.Any() ? r.Items.Sum(i => i.Quantity) : r.Quantity);
            ViewBag.TotalValue = receipts.Sum(r => r.TotalAmount > 0 ? r.TotalAmount : r.Quantity * r.ImportPrice);
            ViewBag.TotalPaid = receipts.Sum(r => r.PaidAmount > 0 ? r.PaidAmount : (r.PaymentStatus == "Paid" ? (r.TotalAmount > 0 ? r.TotalAmount : r.Quantity * r.ImportPrice) : 0));
            ViewBag.TotalDebt = receipts.Sum(r => r.DebtAmount > 0 ? r.DebtAmount : (r.PaymentStatus == "Unpaid" ? (r.TotalAmount > 0 ? r.TotalAmount : r.Quantity * r.ImportPrice) : 0));

            ViewBag.PendingOrdersCount = await _context.StockReceipts.CountAsync(r => r.ReceiptStage == "PendingApproval");
            ViewBag.ApprovedOrdersCount = await _context.StockReceipts.CountAsync(r => r.ReceiptStage == "Approved");
            ViewBag.OrderedCount = await _context.StockReceipts.CountAsync(r => r.ReceiptStage == "Ordered");
            ViewBag.ReceivedCount = await _context.StockReceipts.CountAsync(r => r.ReceiptStage == "Received" || r.ReceiptStage == null);

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
            ViewBag.Stores = await storesQuery
                .OrderBy(s => s.Id == 1 ? 0 : 1)
                .ThenBy(s => s.Name)
                .ToListAsync();

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
                    ActualQuantity = i.ActualQuantity > 0 ? i.ActualQuantity : (r.ReceiptStage == "Received" ? i.Quantity : 0),
                    CancelledQuantity = i.CancelledQuantity,
                    ReceivedDate = i.ReceivedDate?.ToString("dd/MM/yyyy HH:mm"),
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
                        ActualQuantity = r.Quantity,
                        CancelledQuantity = 0,
                        ReceivedDate = r.ImportDate.ToString("dd/MM/yyyy HH:mm"),
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
                    receiptStage = r.ReceiptStage ?? "Received",
                    receiptType = r.ReceiptType,
                    receiptTypeLabel = r.ReceiptType == "Import" ? "Nhập khẩu quốc tế" : "Nhập trong nước",
                    importDate = r.ImportDate.ToString("dd/MM/yyyy HH:mm"),
                    createdAt = r.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                    createdByName = r.CreatedByName ?? "N/A",
                    approvedByName = r.ApprovedByName ?? "—",
                    approvedAt = r.ApprovedAt?.ToString("dd/MM/yyyy HH:mm") ?? "—",
                    depositAmount = r.DepositAmount,
                    depositTransferDate = r.DepositTransferDate?.ToString("dd/MM/yyyy") ?? "—",
                    depositReceivedDate = r.DepositReceivedDate?.ToString("dd/MM/yyyy") ?? "—",
                    paymentTransferDate = r.PaymentTransferDate?.ToString("dd/MM/yyyy") ?? "—",
                    paymentReceivedDate = r.PaymentReceivedDate?.ToString("dd/MM/yyyy") ?? "—",
                    paymentMethod = r.PaymentMethod ?? "—",
                    supplierDeliveryNoteCode = r.SupplierDeliveryNoteCode ?? "—",
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
        // QUẢN LÝ DUYỆT PHIẾU ĐẶT HÀNG (YÊU CẦU 2)
        // =========================================================================
        [HttpPost]
        public async Task<IActionResult> ApprovePurchaseOrder(int receiptId, string? note)
        {
            int? userId = await _userScope.GetUserIdAsync();
            var currentUser = await _userScope.GetCurrentUserAsync();
            string userName = currentUser?.FullName ?? User?.Identity?.Name ?? "Quản lý kho";

            var result = await _inventoryService.ApprovePurchaseOrderAsync(receiptId, userId, userName, note);
            return Json(new { success = result.Success, message = result.Message });
        }

        // =========================================================================
        // GỬI NHÀ CUNG CẤP & GHI NHẬN ĐẶT CỌC (YÊU CẦU 2)
        // =========================================================================
        [HttpPost]
        public async Task<IActionResult> SendToSupplier(
            int receiptId,
            decimal depositAmount,
            DateTime? depositTransferDate,
            DateTime? depositReceivedDate,
            string? paymentMethod,
            string? note)
        {
            var result = await _inventoryService.SendToSupplierAsync(receiptId, depositAmount, depositTransferDate, depositReceivedDate, paymentMethod, note);
            return Json(new { success = result.Success, message = result.Message });
        }

        // =========================================================================
        // ĐỐI CHIẾU PHIẾU NHẬP VỚI PHIẾU ĐẶT & LƯU VÀO KHO (YÊU CẦU 2)
        // =========================================================================
        [HttpPost]
        public async Task<IActionResult> ReconcileAndReceive([FromBody] ReconcileStockReceiptViewModel model)
        {
            if (model == null) return Json(new { success = false, message = "Dữ liệu đối chiếu không hợp lệ." });

            int? userId = await _userScope.GetUserIdAsync();
            var currentUser = await _userScope.GetCurrentUserAsync();
            string userName = currentUser?.FullName ?? User?.Identity?.Name ?? "Thủ kho";

            var result = await _inventoryService.ReconcileAndReceiveReceiptAsync(model, userId, userName);
            return Json(new { success = result.Success, message = result.Message });
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
            var currentUser = await _userScope.GetCurrentUserAsync();
            string userName = currentUser?.FullName ?? User?.Identity?.Name ?? HttpContext?.Session?.GetString("FullName") ?? "Quản trị viên";

            var result = await _inventoryService.CreateMultiItemReceiptAsync(model, userId, userName);
            return Json(new { success = result.Success, message = result.Message });
        }

        [HttpPost]
        public async Task<IActionResult> CreateMultiItemReceipt([FromBody] CreateStockReceiptViewModel model)
        {
            return await CreateMultiReceipt(model);
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
            var currentUser = await _userScope.GetCurrentUserAsync();
            string userName = currentUser?.FullName ?? User?.Identity?.Name ?? HttpContext?.Session?.GetString("FullName") ?? "Quản trị viên";

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
        // API: DANH MỤC SẢN PHẨM TỒN KHO GỢI Ý NHẬP HÀNG (LỌC THEO LOẠI, CHECKBOX & NCC LIÊN QUAN)
        // =========================================================================
        [HttpGet]
        public async Task<IActionResult> GetInventorySuggestionCatalog(
            int storeId = 1,
            int? categoryId = null,
            string? keyword = null,
            string? stockStatus = null,
            int? supplierId = null)
        {
            // 1. Lấy tồn kho thực tế tại kho được chỉ định
            var storeInventories = await _context.Inventories
                .Where(i => i.StoreId == storeId)
                .ToDictionaryAsync(i => i.VariantId, i => new { i.StockQuantity, i.InTransitQuantity });

            // 1b. Lấy tổng tồn kho trên toàn chuỗi (tất cả các kho) để người dùng nắm rõ bức tranh tồn kho tổng thể
            var chainInventories = await _context.Inventories
                .GroupBy(i => i.VariantId)
                .Select(g => new { VariantId = g.Key, TotalStock = g.Sum(x => x.StockQuantity) })
                .ToDictionaryAsync(x => x.VariantId, x => x.TotalStock);

            // 2. Lấy dữ liệu đơn giá nhập lần gần nhất của từng biến thể từ tất cả các nhà cung cấp (để so sánh đa NCC)
            var allPastReceiptItems = await _context.StockReceiptItems
                .Include(i => i.StockReceipt)
                .Where(i => i.StockReceipt != null && i.StockReceipt.SupplierId.HasValue)
                .OrderByDescending(i => i.StockReceipt!.ImportDate)
                .ThenByDescending(i => i.Id)
                .ToListAsync();

            var pastReceiptByVariantAndSupplier = allPastReceiptItems
                .GroupBy(i => (i.VariantId, SupplierId: i.StockReceipt!.SupplierId!.Value))
                .ToDictionary(g => g.Key, g => g.First());

            var currentSupplierPastImports = (supplierId.HasValue && supplierId.Value > 0)
                ? allPastReceiptItems.Where(i => i.StockReceipt!.SupplierId == supplierId.Value)
                    .GroupBy(i => i.VariantId)
                    .ToDictionary(g => g.Key, g => g.First())
                : new Dictionary<int, StockReceiptItem>();

            // 3. Truy vấn danh sách biến thể kèm Product, Category, Brand, và Nhà cung cấp liên quan (SupplierProducts)
            var query = _context.ProductVariants
                .Include(v => v.Product)
                    .ThenInclude(p => p.Category)
                .Include(v => v.Product)
                    .ThenInclude(p => p.Brand)
                .Include(v => v.SupplierProducts)
                    .ThenInclude(sp => sp.Supplier)
                .Where(v => v.Product != null && v.Product.IsActive);

            if (categoryId.HasValue && categoryId.Value > 0)
            {
                query = query.Where(v => v.Product.CategoryId == categoryId.Value);
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                string kw = keyword.Trim().ToLower();
                query = query.Where(v =>
                    v.Product.Name.ToLower().Contains(kw) ||
                    v.SKU.ToLower().Contains(kw) ||
                    (v.ColorName != null && v.ColorName.ToLower().Contains(kw)) ||
                    (v.Volume != null && v.Volume.ToLower().Contains(kw)) ||
                    (v.Product.Brand != null && v.Product.Brand.Name.ToLower().Contains(kw)));
            }

            if (supplierId.HasValue && supplierId.Value > 0)
            {
                query = query.Where(v => v.SupplierProducts.Any(sp => sp.SupplierId == supplierId.Value));
            }

            var variants = await query
                .OrderBy(v => v.Product.CategoryId)
                .ThenBy(v => v.Product.Name)
                .ThenBy(v => v.SKU)
                .ToListAsync();

            var list = variants.Select(v =>
            {
                int stockQty = 0;
                int inTransitQty = 0;
                if (storeInventories.TryGetValue(v.Id, out var inv))
                {
                    stockQty = inv.StockQuantity;
                    inTransitQty = inv.InTransitQuantity;
                }

                int totalChain = chainInventories.TryGetValue(v.Id, out var cStock) ? cStock : 0;

                string status = "normal";
                if (stockQty == 0)
                {
                    status = totalChain > 0 ? "out_of_stock_local" : "out_of_stock";
                }
                else if (stockQty <= (_receiptSettings.LowStockThreshold > 0 ? _receiptSettings.LowStockThreshold : 10))
                {
                    status = "low_stock";
                }

                // XỬ LÝ ĐƠN GIÁ NHẬP CHO NCC ĐANG CHỌN (Yêu cầu 1.7):
                // Lấy giá nhập lần trước làm gợi ý, chưa có thì ghi "Chưa nhập"
                bool hasPrevImport = currentSupplierPastImports.TryGetValue(v.Id, out var lastItem);
                decimal? lastPrice = hasPrevImport ? lastItem!.ImportPrice : null;
                string? lastDate = hasPrevImport ? lastItem!.StockReceipt?.ImportDate.ToString("dd/MM/yyyy") : null;
                string? lastSupplier = hasPrevImport ? lastItem!.StockReceipt?.Supplier : null;
                string? lastCode = hasPrevImport ? lastItem!.StockReceipt?.ReceiptCode : null;
                string statusNote = hasPrevImport ? $"Lần trước: {lastPrice:N0} đ" : "Chưa nhập";

                decimal? defaultPrice = null;
                if (hasPrevImport && lastPrice.HasValue && lastPrice.Value > 0)
                {
                    defaultPrice = lastPrice.Value;
                }
                else
                {
                    statusNote = "Chưa nhập";
                }

                // Danh sách tất cả các nhà cung cấp phân phối biến thể này kèm so sánh giá chi tiết
                // TÁCH BẠCH RÕ RÀNG: "Giá chào hiện tại" (từ bảng cung ứng) và "Giá nhập lần gần nhất" (từ phiếu nhập)
                // "Giá tốt nhất" và "So sánh với giá tốt nhất" CHỈ tính trên Giá chào hiện tại, không trộn với giá nhập cũ
                var rawSuppliers = v.SupplierProducts
                    .Where(sp => sp.Supplier != null && sp.Supplier.IsActive)
                    .Select(sp =>
                    {
                        bool hasPast = pastReceiptByVariantAndSupplier.TryGetValue((v.Id, sp.SupplierId), out var pastItem);
                        decimal? pastPrice = hasPast ? pastItem!.ImportPrice : null;
                        string? pastDate = hasPast ? pastItem!.StockReceipt?.ImportDate.ToString("dd/MM/yyyy") : null;
                        string? pastCode = hasPast ? pastItem!.StockReceipt?.ReceiptCode : null;

                        decimal suppliedPrice = sp.SuppliedPrice ?? 0;
                        DateTime updatedDate = sp.UpdatedAt ?? sp.CreatedAt;
                        string suppliedPriceDate = updatedDate.ToString("dd/MM/yyyy");

                        // Chênh lệch giữa Giá chào hiện tại và Giá nhập lần trước của CÙNG nhà cung cấp
                        decimal? diffFromPast = null;
                        double? diffFromPastPercent = null;
                        if (suppliedPrice > 0 && hasPast && pastPrice.HasValue && pastPrice.Value > 0)
                        {
                            diffFromPast = suppliedPrice - pastPrice.Value;
                            diffFromPastPercent = Math.Round((double)((diffFromPast.Value / pastPrice.Value) * 100), 1);
                        }

                        return new
                        {
                            supplierId = sp.SupplierId,
                            supplierName = sp.Supplier.Name,
                            country = sp.Supplier.Country ?? "Việt Nam",
                            address = sp.Supplier.Address ?? "—",
                            phone = sp.Supplier.Phone ?? "—",
                            suppliedPrice = suppliedPrice,
                            suppliedPriceDate = suppliedPriceDate,
                            hasPastImport = hasPast,
                            lastImportPrice = pastPrice,
                            lastImportDate = pastDate,
                            lastReceiptCode = pastCode,
                            diffFromPastImport = diffFromPast,
                            diffFromPastImportPercent = diffFromPastPercent,
                            notes = sp.Notes ?? ""
                        };
                    })
                    .ToList();

                // "Giá tốt nhất" CHỈ tính trên "Giá chào hiện tại" (SuppliedPrice > 0), không trộn với giá nhập cũ
                decimal minSuppliedPrice = rawSuppliers.Where(s => s.suppliedPrice > 0)
                    .Select(s => s.suppliedPrice)
                    .DefaultIfEmpty(0)
                    .Min();

                var relatedSuppliers = rawSuppliers.Select(s => new
                {
                    s.supplierId,
                    s.supplierName,
                    s.country,
                    s.address,
                    s.phone,
                    s.suppliedPrice,
                    s.suppliedPriceDate,
                    s.hasPastImport,
                    s.lastImportPrice,
                    s.lastImportDate,
                    s.lastReceiptCode,
                    s.diffFromPastImport,
                    s.diffFromPastImportPercent,
                    s.notes,
                    isLowestPrice = (minSuppliedPrice > 0 && s.suppliedPrice == minSuppliedPrice),
                    differenceFromLowest = (minSuppliedPrice > 0 && s.suppliedPrice > 0) ? (s.suppliedPrice - minSuppliedPrice) : 0,
                    differencePercent = (minSuppliedPrice > 0 && s.suppliedPrice > minSuppliedPrice)
                        ? Math.Round(((s.suppliedPrice - minSuppliedPrice) / minSuppliedPrice) * 100, 1)
                        : 0
                }).OrderBy(s => s.suppliedPrice > 0 ? s.suppliedPrice : decimal.MaxValue).ToList();

                return new
                {
                    variantId = v.Id,
                    productId = v.ProductId,
                    productName = v.Product.Name,
                    thumbnailUrl = !string.IsNullOrEmpty(v.ImageUrl) ? v.ImageUrl : (!string.IsNullOrEmpty(v.Product.ThumbnailUrl) ? v.Product.ThumbnailUrl : "/images/no-image.png"),
                    sku = v.SKU,
                    colorName = v.ColorName ?? "",
                    volume = v.Volume ?? "",
                    categoryId = v.Product.CategoryId,
                    categoryName = v.Product.Category != null ? v.Product.Category.Name : "Khác",
                    brandName = v.Product.Brand != null ? v.Product.Brand.Name : "",
                    currentRetailPrice = v.RetailPrice ?? 0,
                    originalPrice = v.OriginalPrice ?? 0,
                    stockQuantity = stockQty,
                    totalChainStock = totalChain,
                    inTransitQuantity = inTransitQty,
                    stockStatus = status,
                    hasPreviousImport = hasPrevImport,
                    lastImportPrice = lastPrice,
                    lastImportDate = lastDate,
                    lastSupplierName = lastSupplier,
                    lastReceiptCode = lastCode,
                    importPriceNote = statusNote,
                    defaultImportPrice = defaultPrice,
                    relatedSuppliers = relatedSuppliers,
                    lowestSuppliedPrice = minSuppliedPrice,
                    bestSupplierName = relatedSuppliers.FirstOrDefault(s => s.isLowestPrice)?.supplierName,
                    bestSupplierId = relatedSuppliers.FirstOrDefault(s => s.isLowestPrice)?.supplierId ?? 0
                };
            }).AsEnumerable();

            if (!string.IsNullOrWhiteSpace(stockStatus))
            {
                if (stockStatus == "out_of_stock")
                {
                    list = list.Where(r => r.stockQuantity == 0);
                }
                else
                {
                    list = list.Where(r => r.stockStatus == stockStatus);
                }
            }

            return Json(list.ToList());
        }

        [HttpGet]
        public async Task<IActionResult> GetVariantsForReceiptSelection(
            int storeId = 1,
            int? categoryId = null,
            string? keyword = null,
            string? stockStatus = null,
            int? supplierId = null)
        {
            return await GetInventorySuggestionCatalog(storeId, categoryId, keyword, stockStatus, supplierId);
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
                .Include(v => v.SupplierProducts)
                    .ThenInclude(sp => sp.Supplier)
                .AsQueryable();

            if (supplierId.HasValue && supplierId.Value > 0)
            {
                var suppliedVariantIds = await _context.SupplierProducts
                    .Where(sp => sp.SupplierId == supplierId.Value)
                    .Select(sp => sp.VariantId)
                    .ToListAsync();

                query = query.Where(v => suppliedVariantIds.Contains(v.Id));
            }

            if (categoryId.HasValue && categoryId.Value > 0)
            {
                query = query.Where(v => v.Product.CategoryId == categoryId.Value);
            }

            var variants = await query
                .OrderBy(v => v.Product.Name)
                .ThenBy(v => v.SKU)
                .ToListAsync();

            var variantIds = variants.Select(v => v.Id).ToList();

            var pastReceiptItems = await _context.StockReceiptItems
                .Include(i => i.StockReceipt)
                .Where(i => variantIds.Contains(i.VariantId) && i.StockReceipt != null)
                .OrderByDescending(i => i.StockReceipt!.ImportDate)
                .ThenByDescending(i => i.Id)
                .ToListAsync();

            var lastImportByVariant = pastReceiptItems
                .GroupBy(i => i.VariantId)
                .ToDictionary(g => g.Key, g => g.First());

            var list = variants.Select(v =>
            {
                bool hasPrev = lastImportByVariant.TryGetValue(v.Id, out var lastItem);
                decimal? lastPrice = hasPrev ? lastItem!.ImportPrice : null;
                string? lastDate = hasPrev ? lastItem!.StockReceipt?.ImportDate.ToString("dd/MM/yyyy") : null;
                string? lastSupplier = hasPrev ? lastItem!.StockReceipt?.Supplier : null;
                string note = hasPrev ? $"Lần trước: {lastPrice:N0} đ" : "chưa nhập";

                decimal defaultPrice = 0;
                if (hasPrev && lastPrice.HasValue && lastPrice.Value > 0)
                {
                    defaultPrice = lastPrice.Value;
                }
                else if (supplierId.HasValue && supplierId.Value > 0)
                {
                    var sp = v.SupplierProducts.FirstOrDefault(s => s.SupplierId == supplierId.Value);
                    if (sp != null && sp.SuppliedPrice.HasValue && sp.SuppliedPrice.Value > 0)
                    {
                        defaultPrice = sp.SuppliedPrice.Value;
                    }
                }
                else if (v.SupplierProducts.Any(sp => sp.SuppliedPrice.HasValue && sp.SuppliedPrice.Value > 0))
                {
                    defaultPrice = v.SupplierProducts.Where(sp => sp.SuppliedPrice.HasValue && sp.SuppliedPrice.Value > 0).Min(sp => sp.SuppliedPrice!.Value);
                }
                else if (v.OriginalPrice.HasValue && v.OriginalPrice.Value > 0)
                {
                    defaultPrice = v.OriginalPrice.Value;
                }

                return new
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
                    originalPrice = v.OriginalPrice ?? 0,
                    hasPreviousImport = hasPrev,
                    lastImportPrice = lastPrice,
                    lastImportDate = lastDate,
                    lastSupplierName = lastSupplier,
                    importPriceNote = note,
                    defaultImportPrice = defaultPrice
                };
            }).ToList();

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

            var targetVariant = await _context.ProductVariants
                .Include(v => v.Product)
                    .ThenInclude(p => p.Category)
                .Include(v => v.Product)
                    .ThenInclude(p => p.Brand)
                .FirstOrDefaultAsync(v => v.Id == variantId);

            var lastReceiptItem = await _context.StockReceiptItems
                .Include(i => i.StockReceipt)
                .Where(i => i.VariantId == variantId && i.StockReceipt != null)
                .OrderByDescending(i => i.StockReceipt!.ImportDate)
                .ThenByDescending(i => i.Id)
                .FirstOrDefaultAsync();

            bool hasPreviousImport = (lastReceiptItem != null);
            decimal? lastImportPrice = hasPreviousImport ? lastReceiptItem!.ImportPrice : null;
            string? lastImportDate = hasPreviousImport ? lastReceiptItem!.StockReceipt?.ImportDate.ToString("dd/MM/yyyy") : null;
            string? lastSupplierName = hasPreviousImport ? lastReceiptItem!.StockReceipt?.Supplier : null;
            string? lastReceiptCode = hasPreviousImport ? lastReceiptItem!.StockReceipt?.ReceiptCode : null;
            string importStatusNote = hasPreviousImport 
                ? $"Lần trước: {lastImportPrice:N0} đ" 
                : "chưa nhập";

            decimal lowestPrice = comparisons.Any() ? comparisons.Min(c => c.ImportPrice) : 0;
            var bestItem = comparisons.FirstOrDefault(c => c.IsLowestPrice);

            return Json(new
            {
                success = true,
                data = comparisons,
                target = targetVariant == null ? null : new
                {
                    variantId = targetVariant.Id,
                    productId = targetVariant.ProductId,
                    productName = targetVariant.Product?.Name,
                    categoryName = targetVariant.Product?.Category?.Name,
                    brandName = targetVariant.Product?.Brand?.Name,
                    sku = targetVariant.SKU,
                    colorName = targetVariant.ColorName,
                    volume = targetVariant.Volume,
                    currentRetailPrice = targetVariant.RetailPrice ?? 0
                },
                hasPreviousImport = hasPreviousImport,
                lastImportPrice = lastImportPrice,
                lastImportDate = lastImportDate,
                lastSupplierName = lastSupplierName,
                lastReceiptCode = lastReceiptCode,
                importStatusNote = importStatusNote,
                lowestPrice = lowestPrice,
                bestSupplierName = bestItem?.SupplierName,
                bestSupplierId = bestItem?.SupplierId
            });
        }

        // =========================================================================
        // API: TÌM KIẾM NHANH BIẾN THỂ CHO BẢNG DÒNG HÀNG (YÊU CẦU ĐƠN GIẢN HÓA)
        // =========================================================================
        [HttpGet]
        public async Task<IActionResult> SearchVariantsForReceipt(string? keyword, int? supplierId)
        {
            var query = _context.ProductVariants
                .Include(v => v.Product)
                    .ThenInclude(p => p.Category)
                .Include(v => v.Product)
                    .ThenInclude(p => p.Brand)
                .Include(v => v.SupplierProducts)
                .Where(v => v.Product != null && v.Product.IsActive);

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                string kw = keyword.Trim().ToLower();
                query = query.Where(v =>
                    v.Product.Name.ToLower().Contains(kw) ||
                    v.SKU.ToLower().Contains(kw) ||
                    (v.ColorName != null && v.ColorName.ToLower().Contains(kw)) ||
                    (v.Volume != null && v.Volume.ToLower().Contains(kw)) ||
                    (v.Product.Brand != null && v.Product.Brand.Name.ToLower().Contains(kw)));
            }

            var variants = await query
                .OrderBy(v => v.Product.Name)
                .ThenBy(v => v.SKU)
                .Take(20)
                .ToListAsync();

            var variantIds = variants.Select(v => v.Id).ToList();

            var pastReceiptItems = await _context.StockReceiptItems
                .Include(i => i.StockReceipt)
                .Where(i => variantIds.Contains(i.VariantId) && i.StockReceipt != null)
                .OrderByDescending(i => i.StockReceipt!.ImportDate)
                .ThenByDescending(i => i.Id)
                .ToListAsync();

            var lastImportDict = pastReceiptItems
                .GroupBy(i => new { i.VariantId, SupplierId = i.StockReceipt?.SupplierId })
                .ToDictionary(g => g.Key, g => g.First());

            var anyLastImportDict = pastReceiptItems
                .GroupBy(i => i.VariantId)
                .ToDictionary(g => g.Key, g => g.First());

            var result = variants.Select(v =>
            {
                decimal defaultPrice = 0;
                string priceNote = "chưa nhập";

                if (supplierId.HasValue && supplierId.Value > 0 &&
                    lastImportDict.TryGetValue(new { VariantId = v.Id, SupplierId = (int?)supplierId.Value }, out var itemWithSup))
                {
                    defaultPrice = itemWithSup.ImportPrice;
                    priceNote = $"Lần trước (NCC này): {defaultPrice:N0} đ";
                }
                else if (anyLastImportDict.TryGetValue(v.Id, out var anyItem))
                {
                    defaultPrice = anyItem.ImportPrice;
                    priceNote = $"Lần trước (NCC khác): {defaultPrice:N0} đ";
                }
                else if (supplierId.HasValue && supplierId.Value > 0 &&
                    v.SupplierProducts.Any(sp => sp.SupplierId == supplierId.Value && sp.SuppliedPrice > 0))
                {
                    defaultPrice = v.SupplierProducts.First(sp => sp.SupplierId == supplierId.Value).SuppliedPrice!.Value;
                    priceNote = $"Giá cung ứng: {defaultPrice:N0} đ";
                }
                else if (v.OriginalPrice.HasValue && v.OriginalPrice > 0)
                {
                    defaultPrice = v.OriginalPrice.Value;
                    priceNote = $"Giá vốn niêm yết: {defaultPrice:N0} đ";
                }
                else if (v.RetailPrice.HasValue && v.RetailPrice > 0)
                {
                    defaultPrice = Math.Round(v.RetailPrice.Value * 0.65m, 0);
                    priceNote = "Ước tính 65% giá bán";
                }

                string variantDetail = "";
                if (!string.IsNullOrEmpty(v.ColorName) && v.ColorName != "Mặc định") variantDetail += v.ColorName;
                if (!string.IsNullOrEmpty(v.Volume) && v.Volume != "Mặc định") variantDetail += (variantDetail.Length > 0 ? " - " : "") + v.Volume;
                if (string.IsNullOrEmpty(variantDetail)) variantDetail = "Tiêu chuẩn";

                return new
                {
                    variantId = v.Id,
                    productId = v.ProductId,
                    productName = v.Product.Name,
                    variantName = variantDetail,
                    sku = v.SKU,
                    thumbnailUrl = !string.IsNullOrEmpty(v.ImageUrl) ? v.ImageUrl : (!string.IsNullOrEmpty(v.Product.ThumbnailUrl) ? v.Product.ThumbnailUrl : "/images/no-image.png"),
                    categoryName = v.Product.Category?.Name ?? "Khác",
                    brandName = v.Product.Brand?.Name ?? "",
                    currentRetailPrice = v.RetailPrice ?? 0,
                    suggestedPrice = defaultPrice,
                    priceNote = priceNote,
                    colorCode = v.ColorCode
                };
            }).ToList();

            return Json(new { success = true, data = result });
        }

        // =========================================================================
        // API: LẤY GIÁ NHẬP LẦN GẦN NHẤT CỦA BIẾN THỂ THEO NHÀ CUNG CẤP CỤ THỂ
        // =========================================================================
        [HttpGet]
        public async Task<IActionResult> GetLastImportPrice(int variantId, int? supplierId)
        {
            var (lastPrice, supplierName, importDate, receiptCode) = await _inventoryService.GetLastImportPriceAsync(variantId, supplierId);
            return Json(new
            {
                success = true,
                lastPrice = lastPrice ?? 0,
                hasHistory = lastPrice.HasValue && lastPrice.Value > 0,
                supplierName = supplierName,
                importDate = importDate?.ToString("dd/MM/yyyy"),
                receiptCode = receiptCode
            });
        }

        // =========================================================================
        // TẢI FILE MẪU EXCEL/CSV ĐỂ NHẬP HÀNG LOẠT DÒNG HÀNG
        // =========================================================================
        [HttpGet]
        public IActionResult DownloadSampleExcel()
        {
            var sampleVariants = _context.ProductVariants
                .Include(v => v.Product)
                .Where(v => v.Product != null && v.Product.IsActive)
                .Take(5)
                .ToList();

            var csvBuilder = new System.Text.StringBuilder();
            // Header with UTF-8 BOM
            csvBuilder.AppendLine("STT,Mã SKU,Tên sản phẩm tham khảo,Số lượng,Đơn giá nhập");
            int idx = 1;
            foreach (var v in sampleVariants)
            {
                decimal price = v.OriginalPrice ?? (v.RetailPrice.HasValue ? Math.Round(v.RetailPrice.Value * 0.65m, 0) : 150000);
                csvBuilder.AppendLine($"{idx},\"{v.SKU}\",\"{v.Product?.Name}\",20,{price}");
                idx++;
            }

            var bytes = System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(csvBuilder.ToString())).ToArray();
            return File(bytes, "text/csv; charset=utf-8", "Mau_Nhap_Kho_BeautyShop.csv");
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

        // =========================================================================
        // MA TRẬN SO SÁNH GIÁ ĐA NHÀ CUNG CẤP (YÊU CẦU ĐIỀU CHỈNH GIAI ĐOẠN 1 - MỤC 2 & 3)
        // Hàng là Biến thể, Cột là Nhà cung cấp, Tô đậm giá thấp nhất trên từng dòng
        // =========================================================================
        [HttpGet]
        public async Task<IActionResult> GetPriceComparisonMatrix(int? categoryId)
        {
            var suppliers = await _context.Suppliers
                .Where(s => s.IsActive)
                .OrderBy(s => s.Name)
                .Select(s => new
                {
                    id = s.Id,
                    name = s.Name,
                    country = s.Country ?? "Việt Nam",
                    phone = s.Phone ?? ""
                })
                .ToListAsync();

            var supplierIds = suppliers.Select(s => s.id).ToList();

            var variantQuery = _context.ProductVariants
                .Include(v => v.Product)
                    .ThenInclude(p => p.Category)
                .Include(v => v.Product)
                    .ThenInclude(p => p.Brand)
                .Include(v => v.SupplierProducts)
                .Where(v => v.Product != null && v.Product.IsActive);

            if (categoryId.HasValue && categoryId.Value > 0)
            {
                variantQuery = variantQuery.Where(v => v.Product.CategoryId == categoryId.Value);
            }

            var variants = await variantQuery
                .OrderBy(v => v.Product.CategoryId)
                .ThenBy(v => v.Product.Name)
                .ThenBy(v => v.SKU)
                .ToListAsync();

            // Lấy toàn bộ lịch sử các lần nhập gần nhất của từng cặp (VariantId, SupplierId)
            var lastReceipts = await _context.StockReceiptItems
                .Include(i => i.StockReceipt)
                .Where(i => i.StockReceipt != null && i.StockReceipt.SupplierId != null)
                .OrderByDescending(i => i.StockReceipt!.ImportDate)
                .ThenByDescending(i => i.Id)
                .ToListAsync();

            var lastReceiptDict = lastReceipts
                .GroupBy(i => (i.VariantId, SupplierId: i.StockReceipt!.SupplierId!.Value))
                .ToDictionary(g => g.Key, g => g.First());

            var rows = variants.Select(v =>
            {
                var priceCells = new Dictionary<int, object>();
                decimal minRowPrice = decimal.MaxValue;

                foreach (var sup in suppliers)
                {
                    decimal? price = null;
                    string dateStr = "";
                    string source = "none";
                    bool isImported = false;

                    // 1. Kiểm tra lần nhập gần nhất của cặp (VariantId, SupplierId)
                    if (lastReceiptDict.TryGetValue((v.Id, sup.id), out var lastItem))
                    {
                        price = lastItem.ImportPrice;
                        dateStr = lastItem.StockReceipt?.ImportDate.ToString("dd/MM/yyyy") ?? "";
                        source = $"Phiếu nhập {lastItem.StockReceipt?.ReceiptCode}";
                        isImported = true;
                    }
                    else
                    {
                        // 2. Kiểm tra giá cung ứng tham chiếu trong bảng SupplierProducts
                        var sp = v.SupplierProducts?.FirstOrDefault(s => s.SupplierId == sup.id);
                        if (sp != null && sp.SuppliedPrice.HasValue && sp.SuppliedPrice.Value > 0)
                        {
                            price = sp.SuppliedPrice.Value;
                            dateStr = sp.UpdatedAt?.ToString("dd/MM/yyyy") ?? sp.CreatedAt.ToString("dd/MM/yyyy");
                            isImported = false;
                            source = "Bảng cung ứng tham khảo";
                        }
                    }

                    if (price.HasValue && price.Value > 0 && price.Value < minRowPrice)
                    {
                        minRowPrice = price.Value;
                    }

                    priceCells[sup.id] = new
                    {
                        supplierId = sup.id,
                        price = price,
                        formattedPrice = price.HasValue ? price.Value.ToString("N0") + " đ" : "-",
                        date = dateStr,
                        source = source,
                        isImported = isImported
                    };
                }

                if (minRowPrice == decimal.MaxValue) minRowPrice = 0;

                return new
                {
                    variantId = v.Id,
                    productName = v.Product.Name,
                    sku = v.SKU,
                    categoryName = v.Product.Category?.Name ?? "—",
                    brandName = v.Product.Brand?.Name ?? "—",
                    colorName = v.ColorName ?? "",
                    volume = v.Volume ?? "",
                    lowestPrice = minRowPrice,
                    supplierPrices = priceCells
                };
            }).ToList();

            return Json(new { success = true, suppliers = suppliers, rows = rows });
        }

    }
}
