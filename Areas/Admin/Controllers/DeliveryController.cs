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
    public class DeliveryController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IDeliveryOptimizationService _deliveryService;
        private readonly ICurrentUserScope _userScope;

        public DeliveryController(
            ApplicationDbContext context,
            IDeliveryOptimizationService deliveryService,
            ICurrentUserScope userScope)
        {
            _context = context;
            _deliveryService = deliveryService;
            _userScope = userScope;
        }

        private async Task<int> GetCurrentUserIdAsync()
        {
            return await _userScope.GetUserIdAsync() ?? 1;
        }

        // =========================================================================
        // 1. QUẢN LÝ KHU VỰC VÀ PHÂN CỤM (DELIVERY ZONES) - delivery.dispatch
        // =========================================================================
        [RequirePermission(AppPermissions.DeliveryDispatch)]
        public async Task<IActionResult> Zones(string? keyword, string? cluster)
        {
            var query = _context.DeliveryZones.AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(z => z.ZoneName.Contains(keyword) || z.ProvinceName.Contains(keyword));
            }

            if (!string.IsNullOrWhiteSpace(cluster))
            {
                query = query.Where(z => z.ClusterCode == cluster);
            }

            var zones = await query.OrderBy(z => z.ClusterCode).ThenBy(z => z.SortOrder).ToListAsync();
            ViewBag.Clusters = await _context.DeliveryZones.Select(z => z.ClusterCode).Distinct().ToListAsync();
            ViewBag.Keyword = keyword;
            ViewBag.CurrentCluster = cluster;

            return View(zones);
        }

        [HttpPost]
        [RequirePermission(AppPermissions.DeliveryDispatch)]
        public async Task<IActionResult> ToggleZone(int id)
        {
            var zone = await _context.DeliveryZones.FindAsync(id);
            if (zone != null)
            {
                zone.IsActive = !zone.IsActive;
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Đã {(zone.IsActive ? "kích hoạt" : "tạm dừng")} khu vực: {zone.ZoneName}.";
            }
            return RedirectToAction(nameof(Zones));
        }

        // =========================================================================
        // 2. LẬP CHUYẾN GIAO HÀNG TỐI ƯU (TRIP PLANNER) - delivery.dispatch
        // =========================================================================
        [RequirePermission(AppPermissions.DeliveryDispatch)]
        public async Task<IActionResult> Planner(DateTime? date, string? shift)
        {
            var tripDate = (date ?? DateTime.Today).Date;
            var currentShift = string.IsNullOrWhiteSpace(shift) ? "Morning" : shift;

            // Lấy danh sách đơn chờ giao trong ca
            var pendingOrders = await _deliveryService.GetPendingDeliveryOrdersAsync(tripDate, currentShift);

            // Thống kê theo cụm khu vực
            var clusterSummary = pendingOrders
                .GroupBy(o => o.DeliveryZone?.ClusterName ?? (o.DeliveryZone?.ClusterCode ?? "Chưa phân cụm"))
                .Select(g => new
                {
                    ClusterName = g.Key,
                    OrderCount = g.Count(),
                    TotalAmount = g.Sum(x => x.FinalAmount),
                    CodCount = g.Count(x => x.PaymentMethod == "COD"),
                    Zones = g.Select(x => x.DeliveryZone?.ZoneName ?? "Khác").Distinct().ToList()
                })
                .ToList();

            // Danh sách các chuyến đã lập trong ca này
            var existingTrips = await _context.DeliveryTrips
                .Include(t => t.ShipperUser)
                .Include(t => t.Stops)
                    .ThenInclude(s => s.Order)
                .Where(t => t.TripDate.Date == tripDate.Date && t.Shift == currentShift)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            // Danh sách Shipper theo vai trò chức năng mới (AppRoles.Shipper)
            ViewBag.Shippers = await _context.Users
                .Include(u => u.Role)
                .Where(u => u.IsActive && u.Role != null && u.Role.Name == AppRoles.Shipper)
                .OrderBy(u => u.FullName)
                .ToListAsync();

            ViewBag.TripDate = tripDate;
            ViewBag.Shift = currentShift;
            ViewBag.ShiftDisplayName = DeliveryConfig.GetShiftDisplayName(currentShift);
            ViewBag.PendingOrders = pendingOrders;
            ViewBag.ClusterSummary = clusterSummary;
            ViewBag.ExistingTrips = existingTrips;
            ViewBag.MaxOrdersPerTrip = DeliveryConfig.MaxOrdersPerTrip;

            return View();
        }

        [HttpPost]
        [RequirePermission(AppPermissions.DeliveryDispatch)]
        public async Task<IActionResult> AutoBatch(DateTime date, string shift, int? maxOrders)
        {
            int userId = await GetCurrentUserIdAsync();
            var result = await _deliveryService.AutoBatchTripsAsync(date, shift, maxOrders, userId);

            if (result.Success)
            {
                TempData["SuccessMessage"] = result.Message;
            }
            else
            {
                TempData["ErrorMessage"] = result.Message;
            }

            return RedirectToAction(nameof(Planner), new { date = date.ToString("yyyy-MM-dd"), shift });
        }

        [HttpPost]
        [RequirePermission(AppPermissions.DeliveryDispatch)]
        public async Task<IActionResult> CreateManualTrip(DateTime date, string shift, string clusterCode, int? shipperUserId, List<int> orderIds)
        {
            int userId = await GetCurrentUserIdAsync();
            var result = await _deliveryService.CreateManualTripAsync(date, shift, clusterCode, shipperUserId, orderIds, userId);

            if (result.Success)
            {
                TempData["SuccessMessage"] = result.Message;
            }
            else
            {
                TempData["ErrorMessage"] = result.Message;
            }

            return RedirectToAction(nameof(Planner), new { date = date.ToString("yyyy-MM-dd"), shift });
        }

        // =========================================================================
        // 3. DANH SÁCH CHUYẾN GIAO & CHI TIẾT HÀNH TRÌNH (TRIPS) - delivery.dispatch
        // =========================================================================
        [RequirePermission(AppPermissions.DeliveryDispatch)]
        public async Task<IActionResult> Trips(DateTime? date, string? shift, string? status, int? shipperId)
        {
            var query = _context.DeliveryTrips
                .Include(t => t.ShipperUser)
                .Include(t => t.CashierUser)
                .Include(t => t.Stops)
                .AsQueryable();

            query = await _userScope.ApplyTripScopeAsync(query);

            if (date.HasValue) query = query.Where(t => t.TripDate.Date == date.Value.Date);
            if (!string.IsNullOrEmpty(shift)) query = query.Where(t => t.Shift == shift);
            if (!string.IsNullOrEmpty(status)) query = query.Where(t => t.Status == status);
            if (shipperId.HasValue) query = query.Where(t => t.ShipperUserId == shipperId.Value);

            var trips = await query.OrderByDescending(t => t.TripDate).ThenByDescending(t => t.CreatedAt).ToListAsync();

            ViewBag.Date = date;
            ViewBag.Shift = shift;
            ViewBag.Status = status;
            ViewBag.ShipperId = shipperId;
            ViewBag.Shippers = await _context.Users
                .Include(u => u.Role)
                .Where(u => u.IsActive && u.Role != null && u.Role.Name == AppRoles.Shipper)
                .ToListAsync();

            return View(trips);
        }

        [RequirePermission(AppPermissions.DeliveryDispatch)]
        public async Task<IActionResult> Detail(int id)
        {
            var trip = await _context.DeliveryTrips
                .Include(t => t.ShipperUser)
                .Include(t => t.CashierUser)
                .Include(t => t.CreatedByUser)
                .Include(t => t.Stops)
                    .ThenInclude(s => s.Order!)
                        .ThenInclude(o => o.DeliveryZone)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (trip == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy chuyến giao hàng.";
                return RedirectToAction(nameof(Trips));
            }

            ViewBag.Shippers = await _context.Users
                .Include(u => u.Role)
                .Where(u => u.IsActive && u.Role != null && u.Role.Name == AppRoles.Shipper)
                .ToListAsync();
            return View(trip);
        }

        [HttpPost]
        [RequirePermission(AppPermissions.DeliveryDispatch)]
        public async Task<IActionResult> AssignShipper(int tripId, int shipperUserId)
        {
            var result = await _deliveryService.AssignShipperAsync(tripId, shipperUserId);
            if (result.Success) TempData["SuccessMessage"] = result.Message;
            else TempData["ErrorMessage"] = result.Message;

            return RedirectToAction(nameof(Detail), new { id = tripId });
        }

        [HttpPost]
        [RequirePermission(AppPermissions.DeliveryDispatch)]
        public async Task<IActionResult> RemoveOrder(int tripId, int orderId, string reason)
        {
            var result = await _deliveryService.RemoveOrderFromTripAsync(tripId, orderId, reason);
            if (result.Success) TempData["SuccessMessage"] = result.Message;
            else TempData["ErrorMessage"] = result.Message;

            return RedirectToAction(nameof(Detail), new { id = tripId });
        }

        [HttpPost]
        [RequirePermission(AppPermissions.DeliveryDispatch)]
        public async Task<IActionResult> ReorderStops(int tripId, [FromBody] List<int> stopIds)
        {
            var result = await _deliveryService.UpdateTripStopsOrderAsync(tripId, stopIds);
            return Json(new { success = result.Success, message = result.Message });
        }

        // =========================================================================
        // 4. MÀN HÌNH NHÂN VIÊN GIAO HÀNG (SHIPPER VIEW) - delivery.execute
        // =========================================================================
        [RequirePermission(AppPermissions.DeliveryExecute)]
        public async Task<IActionResult> MyTrips(DateTime? date)
        {
            int currentUserId = await GetCurrentUserIdAsync();
            bool isAdmin = await _userScope.IsAdminAsync();
            var tripDate = (date ?? DateTime.Today).Date;

            var query = _context.DeliveryTrips
                .Include(t => t.ShipperUser)
                .Include(t => t.Stops)
                    .ThenInclude(s => s.Order!)
                        .ThenInclude(o => o.DeliveryZone)
                .Where(t => t.TripDate.Date == tripDate);

            // Nhân viên giao hàng chỉ thấy chuyến được phân công cho chính mình (Admin thấy tất cả để giám sát)
            if (!isAdmin)
            {
                query = query.Where(t => t.ShipperUserId == currentUserId);
            }

            var trips = await query.OrderBy(t => t.Shift).ToListAsync();

            ViewBag.TripDate = tripDate;
            return View(trips);
        }

        [HttpPost]
        [RequirePermission(AppPermissions.DeliveryExecute)]
        public async Task<IActionResult> StartTrip(int tripId)
        {
            int currentUserId = await GetCurrentUserIdAsync();
            var result = await _deliveryService.StartTripAsync(tripId, currentUserId);

            if (result.Success) TempData["SuccessMessage"] = result.Message;
            else TempData["ErrorMessage"] = result.Message;

            return RedirectToAction(nameof(MyTrips));
        }

        [HttpPost]
        [RequirePermission(AppPermissions.DeliveryExecute)]
        public async Task<IActionResult> UpdateStop(int stopId, string status, decimal? codCollected, string? failureReason, string? notes)
        {
            int currentUserId = await GetCurrentUserIdAsync();
            var result = await _deliveryService.UpdateStopStatusAsync(stopId, status, codCollected, failureReason, notes, currentUserId);

            if (!result.Success && result.Message.Contains("Truy cập bị từ chối"))
            {
                Response.StatusCode = StatusCodes.Status403Forbidden;
                ViewBag.RequiredPermission = "delivery.execute (Chỉ được phép cập nhật chuyến giao của chính mình)";
                return View("~/Views/Account/AccessDenied.cshtml");
            }

            if (result.Success) TempData["SuccessMessage"] = result.Message;
            else TempData["ErrorMessage"] = result.Message;

            return RedirectToAction(nameof(MyTrips));
        }

        // =========================================================================
        // 5. NỘP TIỀN VÀ QUYẾT TOÁN THEO CHUYẾN (SETTLEMENT) - cashier.reconcile
        // =========================================================================
        [RequirePermission(AppPermissions.CashierReconcile)]
        public async Task<IActionResult> Settlement(DateTime? date)
        {
            var tripDate = (date ?? DateTime.Today).Date;

            var query = _context.DeliveryTrips
                .Include(t => t.ShipperUser)
                .Include(t => t.CashierUser)
                .Include(t => t.Stops)
                .Where(t => t.TripDate.Date == tripDate);

            // Lọc chuyến giao theo phạm vi kho của Thu ngân (Admin thấy tất cả)
            query = await _userScope.ApplyTripScopeAsync(query);

            var trips = await query.OrderByDescending(t => t.CreatedAt).ToListAsync();

            ViewBag.TripDate = tripDate;
            ViewBag.Cashiers = await _context.Users
                .Include(u => u.Role)
                .Where(u => u.IsActive && u.Role != null && (u.Role.Name == AppRoles.Cashier || u.Role.Name == AppRoles.Admin))
                .ToListAsync();

            return View(trips);
        }

        [HttpPost]
        [RequirePermission(AppPermissions.CashierReconcile)]
        public async Task<IActionResult> SettleTrip(int tripId, decimal receivedAmount, string? notes)
        {
            int cashierUserId = await GetCurrentUserIdAsync();
            var result = await _deliveryService.SettleTripCodAsync(tripId, cashierUserId, receivedAmount, notes);

            if (!result.Success && result.Message.Contains("Truy cập bị từ chối"))
            {
                Response.StatusCode = StatusCodes.Status403Forbidden;
                ViewBag.RequiredPermission = "cashier.reconcile (Ngoài phạm vi kho)";
                return View("~/Views/Account/AccessDenied.cshtml");
            }

            if (result.Success) TempData["SuccessMessage"] = result.Message;
            else TempData["ErrorMessage"] = result.Message;

            return RedirectToAction(nameof(Settlement));
        }

        // =========================================================================
        // 6. ĐỐI SOÁT CUỐI NGÀY - THU NGÂN NỘP ADMIN (RECONCILIATION) - cashier.reconcile
        // =========================================================================
        [RequirePermission(AppPermissions.CashierReconcile)]
        public async Task<IActionResult> Reconciliation(DateTime? date)
        {
            var recDate = (date ?? DateTime.Today).Date;

            var recQuery = _context.DailyReconciliations
                .Include(r => r.CashierUser)
                .Include(r => r.AdminApprovedUser)
                .Where(r => r.ReconciliationDate.Date == recDate);

            recQuery = await _userScope.ApplyReconciliationScopeAsync(recQuery);
            var rec = await recQuery.FirstOrDefaultAsync();

            // Các chuyến đã nộp tiền trong ngày (lọc theo phạm vi kho của thu ngân)
            var settledTripsQuery = _context.DeliveryTrips
                .Include(t => t.ShipperUser)
                .Include(t => t.Stops)
                .Where(t => t.TripDate.Date == recDate && t.SettlementStatus == "Settled");

            settledTripsQuery = await _userScope.ApplyTripScopeAsync(settledTripsQuery);
            var settledTrips = await settledTripsQuery.ToListAsync();

            ViewBag.RecDate = recDate;
            ViewBag.SettledTrips = settledTrips;
            ViewBag.IsAdmin = await _userScope.IsAdminAsync();

            return View(rec);
        }

        [HttpPost]
        [RequirePermission(AppPermissions.CashierReconcile)]
        public async Task<IActionResult> CreateReconciliation(DateTime date, decimal totalCashSubmitted, string? differenceReason)
        {
            int cashierUserId = await GetCurrentUserIdAsync();
            var result = await _deliveryService.CreateDailyReconciliationAsync(date, cashierUserId, totalCashSubmitted, differenceReason);

            if (result.Success) TempData["SuccessMessage"] = result.Message;
            else TempData["ErrorMessage"] = result.Message;

            return RedirectToAction(nameof(Reconciliation), new { date = date.ToString("yyyy-MM-dd") });
        }

        [HttpPost]
        [RequirePermission(AppPermissions.CashierReconcile)]
        public async Task<IActionResult> ApproveReconciliation(int reconciliationId)
        {
            if (!await _userScope.IsAdminAsync())
            {
                Response.StatusCode = StatusCodes.Status403Forbidden;
                ViewBag.RequiredPermission = "admin.approve_reconciliation";
                ViewBag.RequiredPermissionName = "Phê duyệt và khóa sổ đối soát cuối ngày (Chỉ dành cho Admin)";
                return View("~/Views/Account/AccessDenied.cshtml");
            }

            int adminUserId = await GetCurrentUserIdAsync();
            var result = await _deliveryService.ApproveDailyReconciliationAsync(reconciliationId, adminUserId);

            if (result.Success) TempData["SuccessMessage"] = result.Message;
            else TempData["ErrorMessage"] = result.Message;

            return RedirectToAction(nameof(Reconciliation));
        }
    }
}
