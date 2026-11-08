using BeautyShop.Data;
using BeautyShop.Models;
using BeautyShop.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace BeautyShop.Areas.Admin.Controllers
{
    [Area("Admin")]
    [RequirePermission(AppPermissions.ReportView)]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentUserScope _userScope;
        private readonly IRevenueAnalyticsService _revenueService;

        public DashboardController(ApplicationDbContext context, ICurrentUserScope userScope, IRevenueAnalyticsService revenueService)
        {
            _context = context;
            _userScope = userScope;
            _revenueService = revenueService;
        }

        public async Task<IActionResult> Index(string? reportDate, string? statusGroup)
        {
            var now = DateTime.Now;

            // Bộ lọc phạm vi dữ liệu tập trung theo kho (Admin thấy toàn bộ, Thu ngân chỉ thấy kho của mình)
            var ordersQuery = await _userScope.ApplyOrderScopeAsync(_context.Orders.AsQueryable());

            // ===== 1. THỐNG KÊ KPI KHỞI TẠO =====
            ViewBag.TotalProducts = await _context.Products.CountAsync();
            ViewBag.TotalUsers = await _context.Users.CountAsync();
            ViewBag.TotalOrders = await ordersQuery.CountAsync(); // Tổng số đơn hàng (dạng số nguyên)

            // Doanh thu chỉ được ghi nhận khi đơn HOÀN THÀNH (giao thành công)
            ViewBag.TotalRevenue = await _revenueService.GetRevenueAsync(ordersQuery);

            // Doanh thu hôm nay (tính theo CompletedAt trong ngày)
            var todayStart = now.Date;
            var todayEnd = now.Date.AddDays(1).AddTicks(-1);
            ViewBag.TodayRevenue = await _revenueService.GetRevenueAsync(ordersQuery, todayStart, todayEnd);
            ViewBag.TodayOrders = await _revenueService.GetCompletedOrderCountAsync(ordersQuery, todayStart, todayEnd);

            // Doanh thu tháng này (tính theo CompletedAt trong tháng)
            var monthStart = new DateTime(now.Year, now.Month, 1);
            var monthEnd = monthStart.AddMonths(1).AddTicks(-1);
            ViewBag.RevenueThisMonth = await _revenueService.GetRevenueAsync(ordersQuery, monthStart, monthEnd);
            ViewBag.OrdersThisMonth = await _revenueService.GetCompletedOrderCountAsync(ordersQuery, monthStart, monthEnd);

            // Giá vốn hàng bán và lợi nhuận gộp tích lũy
            ViewBag.TotalCogs = await _revenueService.GetCogsAsync(ordersQuery);
            ViewBag.TotalGrossProfit = await _revenueService.GetGrossProfitAsync(ordersQuery);

            // Đơn hàng chờ xử lý
            ViewBag.PendingOrdersCount = await ordersQuery
                .CountAsync(x => x.OrderStatus == "Pending" || x.OrderStatus == "Chờ xác nhận" || x.OrderStatus == "Chờ duyệt");

            // ===== 2. BIỂU ĐỒ MẶC ĐỊNH (7 NGÀY GẦN NHẤT THEO NGÀY HOÀN THÀNH) =====
            var startDate = now.Date.AddDays(-6);
            var dailyRevenueList = new List<decimal>();
            var dailyOrderCountList = new List<int>();
            var dailyLabels = new List<string>();

            for (int i = 0; i < 7; i++)
            {
                var curDate = startDate.AddDays(i);
                dailyLabels.Add(curDate.ToString("dd/MM"));

                var dayStart = curDate.Date;
                var dayEnd = curDate.Date.AddDays(1).AddTicks(-1);

                var rev = await _revenueService.GetRevenueAsync(ordersQuery, dayStart, dayEnd);
                var cnt = await _revenueService.GetCompletedOrderCountAsync(ordersQuery, dayStart, dayEnd);

                dailyRevenueList.Add(rev);
                dailyOrderCountList.Add(cnt);
            }

            ViewBag.ChartLabels = JsonSerializer.Serialize(dailyLabels);
            ViewBag.ChartRevenues = JsonSerializer.Serialize(dailyRevenueList);
            ViewBag.ChartOrders = JsonSerializer.Serialize(dailyOrderCountList);

            // ===== 3. PHÂN BỔ TRẠNG THÁI ĐƠN HÀNG (CÓ CHÚ THÍCH & TỶ LỆ) =====
            var statusDistribution = await _revenueService.GetOrderStatusDistributionAsync(ordersQuery);
            ViewBag.StatusDistribution = statusDistribution;
            ViewBag.StatusLabels = JsonSerializer.Serialize(statusDistribution.Select(s => s.DisplayName).ToList());
            ViewBag.StatusCounts = JsonSerializer.Serialize(statusDistribution.Select(s => s.Count).ToList());
            ViewBag.StatusColors = JsonSerializer.Serialize(statusDistribution.Select(s => s.ColorHex).ToList());

            // ===== 4. TOP 5 SẢN PHẨM BÁN CHẠY (CHỈ TỪ ĐƠN HOÀN THÀNH) =====
            var topProducts = await _revenueService.GetTopSellingProductsAsync(ordersQuery, 5);
            ViewBag.TopProductNames = JsonSerializer.Serialize(topProducts.Select(p => p.ProductName).ToList());
            ViewBag.TopProductSales = JsonSerializer.Serialize(topProducts.Select(p => p.TotalSold).ToList());
            ViewBag.TopProductRevenues = JsonSerializer.Serialize(topProducts.Select(p => p.TotalRevenue).ToList());

            // ===== 5. 5 ĐƠN HÀNG MỚI NHẤT =====
            ViewBag.LatestOrders = await ordersQuery
                .OrderByDescending(x => x.CreatedAt)
                .Take(5)
                .ToListAsync();

            // ===== 6. BÁO CÁO DOANH THU & DANH SÁCH HÓA ĐƠN THEO NGÀY (YÊU CẦU 4) =====
            DateTime targetDate = now.Date;
            if (!string.IsNullOrEmpty(reportDate) && DateTime.TryParse(reportDate, out var parsedReportDate))
            {
                targetDate = parsedReportDate.Date;
            }

            var dailyReport = await _revenueService.GetDailyRevenueReportAsync(ordersQuery, targetDate, statusGroup);
            ViewBag.DailyReport = dailyReport;
            ViewBag.SelectedReportDate = targetDate.ToString("yyyy-MM-dd");
            ViewBag.SelectedStatusGroup = statusGroup ?? "All";

            return View();
        }

        // ============================================================
        // API REAL-TIME ANALYTICS (AJAX REFRESH THEO THỜI GIAN THẬT)
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> GetRealtimeData(string range = "7days", string? fromDate = null, string? toDate = null)
        {
            var ordersQuery = await _userScope.ApplyOrderScopeAsync(_context.Orders.AsQueryable());
            var now = DateTime.Now;

            // 1. KPI Realtime
            var todayStart = now.Date;
            var todayEnd = now.Date.AddDays(1).AddTicks(-1);
            var monthStart = new DateTime(now.Year, now.Month, 1);
            var monthEnd = monthStart.AddMonths(1).AddTicks(-1);

            var todayRevenue = await _revenueService.GetRevenueAsync(ordersQuery, todayStart, todayEnd);
            var todayOrders = await _revenueService.GetCompletedOrderCountAsync(ordersQuery, todayStart, todayEnd);

            var monthRevenue = await _revenueService.GetRevenueAsync(ordersQuery, monthStart, monthEnd);
            var totalRevenue = await _revenueService.GetRevenueAsync(ordersQuery);
            var totalOrdersCount = await ordersQuery.CountAsync();

            var pendingOrdersCount = await ordersQuery
                .CountAsync(x => x.OrderStatus == "Pending" || x.OrderStatus == "Chờ xác nhận" || x.OrderStatus == "Chờ duyệt");

            // 2. Phân tích biểu đồ theo bộ lọc ngày hoàn thành (CompletedAt)
            var labels = new List<string>();
            var revenues = new List<decimal>();
            var orderCounts = new List<int>();

            if (range == "today")
            {
                // Thống kê theo 24 giờ trong ngày hoàn thành
                for (int h = 0; h < 24; h++)
                {
                    labels.Add($"{h:00}:00");
                    var hStart = todayStart.AddHours(h);
                    var hEnd = hStart.AddHours(1).AddTicks(-1);

                    var rev = await _revenueService.GetRevenueAsync(ordersQuery, hStart, hEnd);
                    var cnt = await _revenueService.GetCompletedOrderCountAsync(ordersQuery, hStart, hEnd);

                    revenues.Add(rev);
                    orderCounts.Add(cnt);
                }
            }
            else if (range == "30days")
            {
                var startDate = now.Date.AddDays(-29);
                for (int i = 0; i < 30; i++)
                {
                    var curDate = startDate.AddDays(i);
                    labels.Add(curDate.ToString("dd/MM"));

                    var dStart = curDate.Date;
                    var dEnd = curDate.Date.AddDays(1).AddTicks(-1);

                    var rev = await _revenueService.GetRevenueAsync(ordersQuery, dStart, dEnd);
                    var cnt = await _revenueService.GetCompletedOrderCountAsync(ordersQuery, dStart, dEnd);

                    revenues.Add(rev);
                    orderCounts.Add(cnt);
                }
            }
            else if (range == "custom" && !string.IsNullOrEmpty(fromDate) && !string.IsNullOrEmpty(toDate))
            {
                if (DateTime.TryParse(fromDate, out DateTime startD) && DateTime.TryParse(toDate, out DateTime endD))
                {
                    endD = endD.Date.AddDays(1).AddTicks(-1);
                    var totalDays = (int)(endD.Date - startD.Date).TotalDays + 1;
                    if (totalDays > 90) totalDays = 90; // Khống chế tối đa 90 ngày

                    for (int i = 0; i < totalDays; i++)
                    {
                        var curDate = startD.Date.AddDays(i);
                        labels.Add(curDate.ToString("dd/MM"));

                        var dStart = curDate.Date;
                        var dEnd = curDate.Date.AddDays(1).AddTicks(-1);

                        var rev = await _revenueService.GetRevenueAsync(ordersQuery, dStart, dEnd);
                        var cnt = await _revenueService.GetCompletedOrderCountAsync(ordersQuery, dStart, dEnd);

                        revenues.Add(rev);
                        orderCounts.Add(cnt);
                    }
                }
            }
            else
            {
                // Mặc định: 7 ngày qua
                var startDate = now.Date.AddDays(-6);
                for (int i = 0; i < 7; i++)
                {
                    var curDate = startDate.AddDays(i);
                    labels.Add(curDate.ToString("dd/MM"));

                    var dStart = curDate.Date;
                    var dEnd = curDate.Date.AddDays(1).AddTicks(-1);

                    var rev = await _revenueService.GetRevenueAsync(ordersQuery, dStart, dEnd);
                    var cnt = await _revenueService.GetCompletedOrderCountAsync(ordersQuery, dStart, dEnd);

                    revenues.Add(rev);
                    orderCounts.Add(cnt);
                }
            }

            // 3. Đơn hàng mới nhất
            var latestOrders = await ordersQuery
                .OrderByDescending(x => x.CreatedAt)
                .Take(5)
                .Select(o => new
                {
                    o.Id,
                    o.OrderCode,
                    o.CustomerName,
                    o.PhoneNumber,
                    FinalAmount = o.FinalAmount.ToString("N0") + "đ",
                    o.OrderStatus,
                    CreatedAt = o.CreatedAt.ToString("HH:mm dd/MM/yyyy"),
                    CompletedAt = o.CompletedAt.HasValue ? o.CompletedAt.Value.ToString("HH:mm dd/MM/yyyy") : "—"
                })
                .ToListAsync();

            return Json(new
            {
                success = true,
                timestamp = now.ToString("HH:mm:ss dd/MM/yyyy"),
                todayRevenue = todayRevenue.ToString("N0") + "đ",
                todayOrders = todayOrders,
                monthRevenue = monthRevenue.ToString("N0") + "đ",
                totalRevenue = totalRevenue.ToString("N0") + "đ",
                totalOrders = totalOrdersCount,
                pendingOrdersCount = pendingOrdersCount,
                labels = labels,
                revenues = revenues,
                orderCounts = orderCounts,
                latestOrders = latestOrders
            });
        }

        // ============================================================
        // API: BÁO CÁO CHI TIẾT HÓA ĐƠN TRONG NGÀY (YÊU CẦU 4)
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> GetDailyReportData(string? date, string? statusGroup)
        {
            var ordersQuery = await _userScope.ApplyOrderScopeAsync(_context.Orders.AsQueryable());
            DateTime targetDate = DateTime.Today;
            if (!string.IsNullOrEmpty(date) && DateTime.TryParse(date, out var parsedDate))
            {
                targetDate = parsedDate.Date;
            }

            var report = await _revenueService.GetDailyRevenueReportAsync(ordersQuery, targetDate, statusGroup);
            return Json(new { success = true, data = report });
        }
    }
}