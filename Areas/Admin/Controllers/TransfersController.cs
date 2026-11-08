using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeautyShop.Data;
using BeautyShop.Models;
using BeautyShop.Services;

namespace BeautyShop.Areas.Admin.Controllers
{
    [Area("Admin")]
    [RequirePermission(AppPermissions.InventoryTransfer)]
    public class TransfersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ITransferService _transferService;
        private readonly ICurrentUserScope _userScope;

        public TransfersController(
            ApplicationDbContext context,
            ITransferService transferService,
            ICurrentUserScope userScope)
        {
            _context = context;
            _transferService = transferService;
            _userScope = userScope;
        }

        // =========================================================================
        // 1. DANH SÁCH PHIẾU CHUYỂN KHO (LỌC THEO PHẠM VI KHO ĐI HOẶC KHO ĐẾN)
        // =========================================================================
        public async Task<IActionResult> Index(string? status, int? fromStoreId, int? toStoreId, string? keyword)
        {
            var query = _context.StockTransfers
                .Include(t => t.FromStore)
                .Include(t => t.ToStore)
                .Include(t => t.FinalDestinationStore)
                .Include(t => t.Lines)
                .ThenInclude(l => l.Variant)
                .ThenInclude(v => v!.Product)
                .AsQueryable();

            // Bộ lọc phạm vi dữ liệu tập trung: chỉ thấy phiếu có FromStoreId hoặc ToStoreId thuộc kho được gán
            query = await _userScope.ApplyTransferScopeAsync(query);

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(t => t.Status == status);
            }

            if (fromStoreId.HasValue && fromStoreId.Value > 0)
            {
                query = query.Where(t => t.FromStoreId == fromStoreId.Value);
            }

            if (toStoreId.HasValue && toStoreId.Value > 0)
            {
                query = query.Where(t => t.ToStoreId == toStoreId.Value);
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                keyword = keyword.Trim().ToLower();
                query = query.Where(t =>
                    t.TransferCode.ToLower().Contains(keyword) ||
                    t.BatchCode.ToLower().Contains(keyword) ||
                    (t.Note != null && t.Note.ToLower().Contains(keyword)) ||
                    t.Lines.Any(l => l.Variant != null && l.Variant.Product != null && l.Variant.Product.Name.ToLower().Contains(keyword)));
            }

            var transfers = await query.OrderByDescending(t => t.CreatedAt).ToListAsync();

            var holdingLocationMap = transfers.ToDictionary(
                t => t.Id,
                t => _transferService.GetCurrentHoldingLocation(t)
            );
            ViewBag.HoldingLocations = holdingLocationMap;

            ViewBag.Status = status;
            ViewBag.FromStoreId = fromStoreId;
            ViewBag.ToStoreId = toStoreId;
            ViewBag.Keyword = keyword;
            ViewBag.Stores = await _context.Stores.Where(s => s.IsActive).ToListAsync();
            ViewBag.AllowedStoreIds = await _userScope.GetAllowedStoreIdsAsync();
            ViewBag.IsAdmin = await _userScope.IsAdminAsync();

            var scopedBase = await _userScope.ApplyTransferScopeAsync(_context.StockTransfers.AsQueryable());
            ViewBag.TotalTransfers = await scopedBase.CountAsync();
            ViewBag.InTransitCount = await scopedBase.CountAsync(t => t.Status == "InTransit");
            ViewBag.ReceivedCount = await scopedBase.CountAsync(t => t.Status == "Received");
            ViewBag.WaitingHubCount = (await _transferService.GetTransitWaitingBatchesAsync(5)).Count;

            return View(transfers);
        }

        // =========================================================================
        // 2. TẠO PHIẾU CHUYỂN KHO (FORM)
        // =========================================================================
        public async Task<IActionResult> Create(int? originStoreId = null, int? targetStoreId = null, string? batchCode = null, int? orderId = null, int? variantId = null)
        {
            bool isAdmin = await _userScope.IsAdminAsync();
            var allowedStoreIds = await _userScope.GetAllowedStoreIdsAsync();

            var allStores = await _context.Stores.Where(s => s.IsActive).ToListAsync();
            // Kho gửi chỉ cho phép chọn trong phạm vi kho của nhân viên (nếu không phải Admin)
            ViewBag.Stores = isAdmin
                ? allStores
                : allStores.Where(s => allowedStoreIds.Contains(s.Id)).ToList();
            ViewBag.AllDestinationStores = allStores;

            ViewBag.Categories = await _context.Categories.OrderBy(c => c.Name).ToListAsync();
            ViewBag.Brands = await _context.Brands.OrderBy(b => b.Name).ToListAsync();

            ViewBag.Variants = await _context.ProductVariants
                .Include(v => v.Product)
                .Where(v => v.Product != null && v.Product.IsActive)
                .OrderBy(v => v.Product!.Name)
                .ToListAsync();

            ViewBag.OriginStoreId = originStoreId ?? (!isAdmin && allowedStoreIds.Any() ? allowedStoreIds.First() : null);
            ViewBag.TargetStoreId = targetStoreId;
            ViewBag.BatchCode = batchCode;

            // KỊCH BẢN 1: Bắt nguồn từ Đơn đặt hàng của khách
            if (orderId.HasValue && orderId.Value > 0)
            {
                var order = await _context.Orders
                    .Include(o => o.OrderDetails!)
                        .ThenInclude(d => d.Variant!)
                            .ThenInclude(v => v!.Product)
                    .Include(o => o.Store)
                    .FirstOrDefaultAsync(o => o.Id == orderId.Value);

                if (order != null)
                {
                    ViewBag.OrderId = order.Id;
                    ViewBag.OrderCode = order.OrderCode;
                    ViewBag.CustomerName = order.CustomerName;
                    targetStoreId = order.StoreId ?? 1;
                    ViewBag.TargetStoreId = targetStoreId;
                    ViewBag.FinalDestinationStoreId = targetStoreId;
                    ViewBag.OrderNote = $"Điều phối hàng bổ sung theo Đơn đặt hàng #{order.OrderCode} (Khách: {order.CustomerName})";

                    var orderVariantIds = order.OrderDetails?.Select(d => d.VariantId).ToList() ?? new List<int>();
                    var candidateStores = await _context.Inventories
                        .Where(i => orderVariantIds.Contains(i.VariantId) && (i.StockQuantity - i.ReservedQuantity) > 0 && i.StoreId != targetStoreId)
                        .Select(i => i.StoreId)
                        .Distinct()
                        .ToListAsync();

                    if (candidateStores.Any() && !originStoreId.HasValue)
                    {
                        originStoreId = candidateStores.First();
                        ViewBag.OriginStoreId = originStoreId;
                    }

                    ViewBag.PresetOrderItems = order.OrderDetails?.Select(d => new
                    {
                        variantId = d.VariantId,
                        productName = d.Variant?.Product?.Name,
                        sku = d.Variant?.SKU,
                        color = d.Variant?.ColorName,
                        volume = d.Variant?.Volume,
                        quantity = d.Quantity
                    }).ToList();
                }
            }

            // KỊCH BẢN 2: Chi nhánh thiếu hàng -> Điều chuyển từ chi nhánh khác có sẵn
            if (variantId.HasValue && variantId.Value > 0)
            {
                var reqVariant = await _context.ProductVariants
                    .Include(v => v.Product)
                    .FirstOrDefaultAsync(v => v.Id == variantId.Value);

                if (reqVariant != null)
                {
                    ViewBag.RequestedVariantId = variantId.Value;
                    ViewBag.RequestedVariantName = reqVariant.Product?.Name;
                    ViewBag.RequestedVariantSKU = reqVariant.SKU;

                    int effectiveTarget = targetStoreId ?? 6;
                    var candidateStores = await _context.Inventories
                        .Include(i => i.Store)
                        .Where(i => i.VariantId == variantId.Value && i.StoreId != effectiveTarget && (i.StockQuantity - i.ReservedQuantity) > 0)
                        .Select(i => new { storeId = i.StoreId, storeName = i.Store!.Name, available = i.StockQuantity - i.ReservedQuantity })
                        .ToListAsync();

                    ViewBag.CandidateOriginStores = candidateStores;

                    if (!originStoreId.HasValue && candidateStores.Any())
                    {
                        originStoreId = candidateStores.First().storeId;
                        ViewBag.OriginStoreId = originStoreId;
                    }
                }
            }

            // KỊCH BẢN 3: Tạo chặng tiếp theo từ một mã lô có sẵn
            if (!string.IsNullOrWhiteSpace(batchCode))
            {
                var previousLegs = await _transferService.GetBatchHistoryAsync(batchCode);
                ViewBag.PreviousLegs = previousLegs;
                var lastLeg = previousLegs.LastOrDefault();
                if (lastLeg != null)
                {
                    ViewBag.OriginStoreId = lastLeg.ToStoreId; // Kho gửi của chặng này chính là kho nhận của chặng trước
                    ViewBag.TargetStoreId = lastLeg.FinalDestinationStoreId;
                    ViewBag.FinalDestinationStoreId = lastLeg.FinalDestinationStoreId;
                    ViewBag.PresetItems = lastLeg.Lines.ToList();
                }
            }

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(
            int fromStoreId,
            int toStoreId,
            int finalDestinationStoreId,
            string? batchCode,
            int[] variantIds,
            int[] quantities,
            string? note)
        {
            bool isAdmin = await _userScope.IsAdminAsync();
            if (!isAdmin)
            {
                var allowedStores = await _userScope.GetAllowedStoreIdsAsync();
                if (!allowedStores.Contains(fromStoreId))
                {
                    Response.StatusCode = StatusCodes.Status403Forbidden;
                    ViewBag.RequiredPermission = "inventory.transfer (Phạm vi kho gửi)";
                    ViewBag.RequiredPermissionName = "Lập phiếu chuyển kho từ kho thuộc phạm vi quản lý";
                    return View("~/Views/Account/AccessDenied.cshtml");
                }
            }

            var items = new List<(int VariantId, int Quantity)>();
            if (variantIds != null && quantities != null)
            {
                for (int i = 0; i < variantIds.Length; i++)
                {
                    if (variantIds[i] > 0 && quantities.Length > i && quantities[i] > 0)
                    {
                        items.Add((variantIds[i], quantities[i]));
                    }
                }
            }

            int? userId = await _userScope.GetUserIdAsync();
            string userName = HttpContext?.Session?.GetString("FullName") ?? "Nhân viên kho";

            var result = await _transferService.CreateTransferAsync(
                fromStoreId, toStoreId, finalDestinationStoreId, batchCode, items, userId, userName, note);

            if (result.Success)
            {
                TempData["SuccessMessage"] = result.Message;
                return RedirectToAction("Detail", new { id = result.Transfer!.Id });
            }
            else
            {
                TempData["ErrorMessage"] = result.Message;
                return RedirectToAction("Create", new { originStoreId = fromStoreId, targetStoreId = finalDestinationStoreId, batchCode });
            }
        }

        // =========================================================================
        // 3. CHI TIẾT PHIẾU CHUYỂN KHO & HÀNH TRÌNH LÔ HÀNG (CHẶN TRUY CẬP ID KHO KHÁC)
        // =========================================================================
        public async Task<IActionResult> Detail(int id)
        {
            var transfer = await _context.StockTransfers
                .Include(t => t.FromStore)
                .Include(t => t.ToStore)
                .Include(t => t.FinalDestinationStore)
                .Include(t => t.Lines)
                .ThenInclude(l => l.Variant)
                .ThenInclude(v => v!.Product)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (transfer == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy phiếu chuyển kho yêu cầu.";
                return RedirectToAction(nameof(Index));
            }

            // Kiểm tra phạm vi kho: chỉ được xem nếu có phạm vi ở FromStoreId hoặc ToStoreId (hoặc là Admin)
            if (!await _userScope.CanViewTransferAsync(transfer))
            {
                Response.StatusCode = StatusCodes.Status403Forbidden;
                ViewBag.RequiredPermission = "inventory.transfer (Ngoài phạm vi kho)";
                ViewBag.RequiredPermissionName = "Xem phiếu chuyển kho thuộc phạm vi kho được phân công";
                return View("~/Views/Account/AccessDenied.cshtml");
            }

            var batchJourney = await _transferService.GetBatchHistoryAsync(transfer.BatchCode);
            ViewBag.BatchJourney = batchJourney;
            ViewBag.CanReceive = await _userScope.CanReceiveTransferAsync(transfer);
            ViewBag.CanCancel = await _userScope.CanCancelTransferAsync(transfer);
            ViewBag.StockComparison = await _transferService.GetTransferStockComparisonAsync(id);
            ViewBag.CurrentHoldingLocation = _transferService.GetCurrentHoldingLocation(transfer);

            return View(transfer);
        }

        // =========================================================================
        // 4. XÁC NHẬN NHẬN HÀNG (CHỈ CHO NGƯỜI CÓ PHẠM VI KHO ĐẾN - ToStoreId)
        // =========================================================================
        [HttpPost]
        public async Task<IActionResult> Receive(int id, string? note)
        {
            var transfer = await _context.StockTransfers.FirstOrDefaultAsync(t => t.Id == id);
            if (transfer == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy phiếu chuyển kho.";
                return RedirectToAction(nameof(Index));
            }

            if (!await _userScope.CanReceiveTransferAsync(transfer))
            {
                Response.StatusCode = StatusCodes.Status403Forbidden;
                ViewBag.RequiredPermission = "inventory.transfer (Yêu cầu phạm vi Kho đến)";
                ViewBag.RequiredPermissionName = "Xác nhận tiếp nhận hàng tại kho đích của chặng";
                return View("~/Views/Account/AccessDenied.cshtml");
            }

            int? userId = await _userScope.GetUserIdAsync();
            string userName = HttpContext?.Session?.GetString("FullName") ?? "Nhân viên kho";

            var result = await _transferService.ReceiveTransferAsync(id, userId, userName, note);

            if (result.Success)
            {
                TempData["SuccessMessage"] = result.Message;
            }
            else
            {
                TempData["ErrorMessage"] = result.Message;
            }

            return RedirectToAction("Detail", new { id });
        }

        // =========================================================================
        // 5. HỦY PHIẾU CHUYỂN KHO (CHỈ CHO NGƯỜI CÓ PHẠM VI KHO ĐI - FromStoreId)
        // =========================================================================
        [HttpPost]
        public async Task<IActionResult> Cancel(int id, string? reason)
        {
            var transfer = await _context.StockTransfers.FirstOrDefaultAsync(t => t.Id == id);
            if (transfer == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy phiếu chuyển kho.";
                return RedirectToAction(nameof(Index));
            }

            if (!await _userScope.CanCancelTransferAsync(transfer))
            {
                Response.StatusCode = StatusCodes.Status403Forbidden;
                ViewBag.RequiredPermission = "inventory.transfer (Yêu cầu phạm vi Kho đi)";
                ViewBag.RequiredPermissionName = "Hủy phiếu chuyển kho xuất phát từ kho gửi";
                return View("~/Views/Account/AccessDenied.cshtml");
            }

            int? userId = await _userScope.GetUserIdAsync();
            string userName = HttpContext?.Session?.GetString("FullName") ?? "Nhân viên kho";

            var result = await _transferService.CancelTransferAsync(id, userId, userName, reason);

            if (result.Success)
            {
                TempData["SuccessMessage"] = result.Message;
            }
            else
            {
                TempData["ErrorMessage"] = result.Message;
            }

            return RedirectToAction("Detail", new { id });
        }

        // =========================================================================
        // 6. MÀN HÌNH "HÀNG CHỜ TẠI KHO TRUNG CHUYỂN" (HUB ĐÀ NẴNG)
        // =========================================================================
        public async Task<IActionResult> TransitHub(int hubStoreId = 5)
        {
            bool isAdmin = await _userScope.IsAdminAsync();
            if (!isAdmin)
            {
                var allowedStores = await _userScope.GetAllowedStoreIdsAsync();
                if (!allowedStores.Contains(hubStoreId))
                {
                    Response.StatusCode = StatusCodes.Status403Forbidden;
                    ViewBag.RequiredPermission = $"inventory.transfer (Phạm vi Kho trung chuyển #{hubStoreId})";
                    return View("~/Views/Account/AccessDenied.cshtml");
                }
            }

            var hubStore = await _context.Stores.FirstOrDefaultAsync(s => s.Id == hubStoreId);
            ViewBag.HubStore = hubStore;

            var waitingBatches = await _transferService.GetTransitWaitingBatchesAsync(hubStoreId);
            return View(waitingBatches);
        }

        // =========================================================================
        // 7. QUẢN LÝ DANH MỤC TUYẾN CHUYỂN KHO (CHỈ DÀNH CHO ADMIN - route.manage)
        // =========================================================================
        [RequirePermission(AppPermissions.RouteManage)]
        public async Task<IActionResult> Routes()
        {
            var routes = await _context.TransferRoutes
                .Include(r => r.FromStore)
                .Include(r => r.ToStore)
                .Include(r => r.TransitStore)
                .OrderBy(r => r.FromStoreId)
                .ThenBy(r => r.ToStoreId)
                .ToListAsync();

            return View(routes);
        }

        [HttpPost]
        [RequirePermission(AppPermissions.RouteManage)]
        public async Task<IActionResult> ToggleRoute(int id)
        {
            var result = await _transferService.ToggleRouteStatusAsync(id);
            if (result.Success)
            {
                TempData["SuccessMessage"] = result.Message;
            }
            else
            {
                TempData["ErrorMessage"] = result.Message;
            }

            return RedirectToAction(nameof(Routes));
        }

        // =========================================================================
        // API HELPER (AJAX CHO FORM TẠO PHIẾU)
        // =========================================================================
        [HttpGet]
        public async Task<IActionResult> GetDestinations(int fromStoreId)
        {
            var destinations = await _transferService.GetValidDestinationsAsync(fromStoreId);
            return Json(destinations);
        }

        [HttpGet]
        public async Task<IActionResult> GetStoreStock(int storeId)
        {
            bool isAdmin = await _userScope.IsAdminAsync();
            if (!isAdmin)
            {
                var allowedStores = await _userScope.GetAllowedStoreIdsAsync();
                if (!allowedStores.Contains(storeId))
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new { error = "Ngoài phạm vi kho được gán." });
                }
            }

            var stockItems = await _context.Inventories
                .Include(i => i.Variant)
                .ThenInclude(v => v!.Product)
                .Where(i => i.StoreId == storeId && (i.StockQuantity - i.ReservedQuantity) > 0)
                .Select(i => new
                {
                    variantId = i.VariantId,
                    productName = i.Variant!.Product!.Name,
                    sku = i.Variant.SKU,
                    color = i.Variant.ColorName,
                    volume = i.Variant.Volume,
                    availableStock = i.StockQuantity - i.ReservedQuantity,
                    totalStock = i.StockQuantity
                })
                .ToListAsync();

            return Json(stockItems);
        }

        [HttpGet]
        public async Task<IActionResult> GetBulkStockItems(int fromStoreId, int? targetStoreId, int? categoryId, int? brandId, string? keyword, bool onlyReceiverShortage = false)
        {
            bool isAdmin = await _userScope.IsAdminAsync();
            if (!isAdmin)
            {
                var allowedStores = await _userScope.GetAllowedStoreIdsAsync();
                if (!allowedStores.Contains(fromStoreId))
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new { error = "Ngoài phạm vi kho được gán." });
                }
            }

            var query = _context.Inventories
                .Include(i => i.Variant)
                    .ThenInclude(v => v!.Product)
                        .ThenInclude(p => p!.Category)
                .Include(i => i.Variant)
                    .ThenInclude(v => v!.Product)
                        .ThenInclude(p => p!.Brand)
                .Where(i => i.StoreId == fromStoreId && (i.StockQuantity - i.ReservedQuantity) > 0);

            if (categoryId.HasValue && categoryId.Value > 0)
            {
                query = query.Where(i => i.Variant!.Product!.CategoryId == categoryId.Value);
            }

            if (brandId.HasValue && brandId.Value > 0)
            {
                query = query.Where(i => i.Variant!.Product!.BrandId == brandId.Value);
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = keyword.Trim().ToLower();
                query = query.Where(i => i.Variant!.Product!.Name.ToLower().Contains(kw) || i.Variant.SKU.ToLower().Contains(kw));
            }

            var items = await query.ToListAsync();

            Dictionary<int, int> targetStockMap = new();
            if (targetStoreId.HasValue && targetStoreId.Value > 0)
            {
                targetStockMap = await _context.Inventories
                    .Where(i => i.StoreId == targetStoreId.Value)
                    .ToDictionaryAsync(i => i.VariantId, i => i.StockQuantity);
            }

            var result = items.Select(i =>
            {
                bool hasTargetRecord = targetStockMap.ContainsKey(i.VariantId);
                int targetStock = hasTargetRecord ? targetStockMap[i.VariantId] : 0;
                string targetStatus = !hasTargetRecord ? "Chưa có tại chi nhánh" : (targetStock == 0 ? "Hết hàng" : $"Còn {targetStock}");
                bool isShortage = (!hasTargetRecord || targetStock == 0);

                return new
                {
                    variantId = i.VariantId,
                    productName = i.Variant!.Product!.Name,
                    sku = i.Variant.SKU,
                    categoryName = i.Variant.Product.Category?.Name ?? "—",
                    brandName = i.Variant.Product.Brand?.Name ?? "—",
                    color = i.Variant.ColorName,
                    volume = i.Variant.Volume,
                    availableStock = i.StockQuantity - i.ReservedQuantity,
                    targetStock = targetStock,
                    targetStatus = targetStatus,
                    isTargetShortage = isShortage
                };
            });

            if (onlyReceiverShortage)
            {
                result = result.Where(x => x.isTargetShortage);
            }

            return Json(result);
        }
    }
}
