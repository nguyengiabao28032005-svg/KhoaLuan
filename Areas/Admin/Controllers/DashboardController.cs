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

        public DashboardController(ApplicationDbContext context, ICurrentUserScope userScope)
        {
            _context = context;
            _userScope = userScope;
        }

        public async Task<IActionResult> Index()
        {
            var now = DateTime.Now;
            var validStatuses = new[] { "Confirmed", "Shipping", "Completed", "Processing", "Đã xác nhận", "Đang giao hàng", "Giao thành công" };
            var cancelledStatuses = new[] { "Cancelled", "Hủy bỏ đơn", "Đã hủy" };

            // Bộ lọc phạm vi dữ liệu tập trung theo kho (Admin thấy toàn bộ, Thu ngân chỉ thấy kho của mình)
            var ordersQuery = await _userScope.ApplyOrderScopeAsync(_context.Orders.AsQueryable());

            // ===== 1. THỐNG KÊ KPI KHỞI TẠO =====
            ViewBag.TotalProducts = await _context.Products.CountAsync();
            ViewBag.TotalUsers = await _context.Users.CountAsync();
            ViewBag.TotalOrders = await ordersQuery.CountAsync();

            // Tổng doanh thu tích lũy (bỏ qua đơn hủy)
            var nonCancelledOrders = await ordersQuery
                .Where(x => !cancelledStatuses.Contains(x.OrderStatus))
                .ToListAsync();

            ViewBag.TotalRevenue = nonCancelledOrders.Sum(x => x.FinalAmount);

            // Doanh thu hôm nay
            var todayStart = now.Date;
            var todayEnd = now.Date.AddDays(1).AddTicks(-1);
            ViewBag.TodayRevenue = nonCancelledOrders
                .Where(x => x.CreatedAt >= todayStart && x.CreatedAt <= todayEnd)
                .Sum(x => x.FinalAmount);

            ViewBag.TodayOrders = await ordersQuery
                .CountAsync(x => x.CreatedAt >= todayStart && x.CreatedAt <= todayEnd);

            // Doanh thu tháng này
            ViewBag.OrdersThisMonth = await ordersQuery
                .CountAsync(x => x.CreatedAt.Month == now.Month && x.CreatedAt.Year == now.Year);

            ViewBag.RevenueThisMonth = nonCancelledOrders
                .Where(x => x.CreatedAt.Month == now.Month && x.CreatedAt.Year == now.Year)
                .Sum(x => x.FinalAmount);

            // Đơn hàng chờ xử lý
            ViewBag.PendingOrdersCount = await ordersQuery
                .CountAsync(x => x.OrderStatus == "Pending" || x.OrderStatus == "Chờ xác nhận" || x.OrderStatus == "Chờ duyệt");

            // ===== 2. BIỂU ĐỒ MẶC ĐỊNH (7 NGÀY GẦN NHẤT) =====
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

                var rev = nonCancelledOrders
                    .Where(o => o.CreatedAt >= dayStart && o.CreatedAt <= dayEnd)
                    .Sum(x => x.FinalAmount);

                var cnt = await ordersQuery
                    .CountAsync(o => o.CreatedAt >= dayStart && o.CreatedAt <= dayEnd);

                dailyRevenueList.Add(rev);
                dailyOrderCountList.Add(cnt);
            }

            ViewBag.ChartLabels = JsonSerializer.Serialize(dailyLabels);
            ViewBag.ChartRevenues = JsonSerializer.Serialize(dailyRevenueList);
            ViewBag.ChartOrders = JsonSerializer.Serialize(dailyOrderCountList);

            // ===== 3. PHÂN BỔ TRẠNG THÁI ĐƠN HÀNG =====
            var statusGroups = await ordersQuery
                .GroupBy(o => o.OrderStatus)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            ViewBag.StatusLabels = JsonSerializer.Serialize(statusGroups.Select(s => s.Status).ToList());
            ViewBag.StatusCounts = JsonSerializer.Serialize(statusGroups.Select(s => s.Count).ToList());

            // ===== 4. TOP 5 SẢN PHẨM BÁN CHẠY (THEO PHẠM VI KHO) =====
            var scopedOrderIds = await ordersQuery.Select(o => o.Id).ToListAsync();
            var topProducts = _context.OrderDetails
                .Include(od => od.Variant)
                    .ThenInclude(v => v != null ? v.Product : null)
                .Where(od => scopedOrderIds.Contains(od.OrderId))
                .AsEnumerable()
                .GroupBy(od => od.VariantId)
                .Select(g => new
                {
                    ProductName = g.First().Variant != null && g.First().Variant!.Product != null
                        ? g.First().Variant!.Product!.Name : "Sản phẩm khác",
                    TotalSold = g.Sum(od => od.Quantity),
                    TotalRevenue = g.Sum(od => od.Quantity * od.UnitPrice)
                })
                .OrderByDescending(x => x.TotalRevenue)
                .Take(5)
                .ToList();

            ViewBag.TopProductNames = JsonSerializer.Serialize(topProducts.Select(p => p.ProductName).ToList());
            ViewBag.TopProductSales = JsonSerializer.Serialize(topProducts.Select(p => p.TotalSold).ToList());
            ViewBag.TopProductRevenues = JsonSerializer.Serialize(topProducts.Select(p => p.TotalRevenue).ToList());

            // ===== 5. 5 ĐƠN HÀNG MỚI NHẤT =====
            ViewBag.LatestOrders = await ordersQuery
                .OrderByDescending(x => x.CreatedAt)
                .Take(5)
                .ToListAsync();

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
            var cancelledStatuses = new[] { "Cancelled", "Hủy bỏ đơn", "Đã hủy" };

            // 1. KPI Realtime
            var todayStart = now.Date;
            var todayEnd = now.Date.AddDays(1).AddTicks(-1);

            var todayRevenue = _context.Orders
                .Where(x => x.CreatedAt >= todayStart && x.CreatedAt <= todayEnd && !cancelledStatuses.Contains(x.OrderStatus))
                .Select(x => x.FinalAmount)
                .AsEnumerable()
                .Sum();
 
            var todayOrders = _context.Orders
                .Count(x => x.CreatedAt >= todayStart && x.CreatedAt <= todayEnd);
 
            var monthRevenue = _context.Orders
                .Where(x => x.CreatedAt.Month == now.Month && x.CreatedAt.Year == now.Year && !cancelledStatuses.Contains(x.OrderStatus))
                .Select(x => x.FinalAmount)
                .AsEnumerable()
                .Sum();
 
            var totalRevenue = _context.Orders
                .Where(x => !cancelledStatuses.Contains(x.OrderStatus))
                .Select(x => x.FinalAmount)
                .AsEnumerable()
                .Sum();

            var pendingOrdersCount = _context.Orders
                .Count(x => x.OrderStatus == "Pending" || x.OrderStatus == "Chờ xác nhận" || x.OrderStatus == "Chờ duyệt");

            // 2. Phân tích biểu đồ theo bộ lọc
            var labels = new List<string>();
            var revenues = new List<decimal>();
            var orderCounts = new List<int>();

            if (range == "today")
            {
                // Thống kê theo 24 giờ trong ngày
                for (int h = 0; h < 24; h++)
                {
                    labels.Add($"{h:00}:00");
                    var hStart = todayStart.AddHours(h);
                    var hEnd = hStart.AddHours(1).AddTicks(-1);

                    var rev = _context.Orders
                        .Where(o => o.CreatedAt >= hStart && o.CreatedAt <= hEnd && !cancelledStatuses.Contains(o.OrderStatus))
                        .Select(o => o.FinalAmount)
                        .AsEnumerable()
                        .Sum();

                    var cnt = _context.Orders
                        .Count(o => o.CreatedAt >= hStart && o.CreatedAt <= hEnd);

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

                    var rev = _context.Orders
                        .Where(o => o.CreatedAt >= dStart && o.CreatedAt <= dEnd && !cancelledStatuses.Contains(o.OrderStatus))
                        .Select(o => o.FinalAmount)
                        .AsEnumerable()
                        .Sum();

                    var cnt = _context.Orders
                        .Count(o => o.CreatedAt >= dStart && o.CreatedAt <= dEnd);

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

                        var rev = _context.Orders
                            .Where(o => o.CreatedAt >= dStart && o.CreatedAt <= dEnd && !cancelledStatuses.Contains(o.OrderStatus))
                            .Select(o => o.FinalAmount)
                            .AsEnumerable()
                            .Sum();

                        var cnt = _context.Orders
                            .Count(o => o.CreatedAt >= dStart && o.CreatedAt <= dEnd);

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

                    var rev = _context.Orders
                        .Where(o => o.CreatedAt >= dStart && o.CreatedAt <= dEnd && !cancelledStatuses.Contains(o.OrderStatus))
                        .Select(o => o.FinalAmount)
                        .AsEnumerable()
                        .Sum();

                    var cnt = _context.Orders
                        .Count(o => o.CreatedAt >= dStart && o.CreatedAt <= dEnd);

                    revenues.Add(rev);
                    orderCounts.Add(cnt);
                }
            }

            // 3. Đơn hàng mới nhất
            var latestOrders = _context.Orders
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
                    CreatedAt = o.CreatedAt.ToString("HH:mm dd/MM/yyyy")
                })
                .ToList();

            return Json(new
            {
                success = true,
                timestamp = now.ToString("HH:mm:ss dd/MM/yyyy"),
                todayRevenue = todayRevenue.ToString("N0") + "đ",
                todayOrders = todayOrders,
                monthRevenue = monthRevenue.ToString("N0") + "đ",
                totalRevenue = totalRevenue.ToString("N0") + "đ",
                pendingOrdersCount = pendingOrdersCount,
                labels = labels,
                revenues = revenues,
                orderCounts = orderCounts,
                latestOrders = latestOrders
            });
        }
    }
}