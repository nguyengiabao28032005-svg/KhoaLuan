using BeautyShop.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace BeautyShop.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class EmployeeDashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public EmployeeDashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        private bool IsStaff()
        {
            var role = HttpContext.Session.GetString("Role");
            return role == "Employee" || role == "Admin";
        }

        public IActionResult Index()
        {
            if (!IsStaff())
                return RedirectToAction("Index", "Home", new { area = "" });

            var now = DateTime.Now;
            var todayStart = now.Date;
            var todayEnd = now.Date.AddDays(1).AddTicks(-1);

            var employeeName = HttpContext.Session.GetString("FullName") ?? "Nhân viên";

            // ===== KPI NHÂN VIÊN =====
            // Đơn chờ xử lý
            var pendingStatuses = new[] { "Pending", "Chờ xác nhận", "Chờ duyệt" };
            ViewBag.PendingOrders = _context.Orders
                .Count(o => pendingStatuses.Contains(o.OrderStatus));

            // Đơn hôm nay
            ViewBag.TodayOrders = _context.Orders
                .Count(o => o.CreatedAt >= todayStart && o.CreatedAt <= todayEnd);

            // Đơn đã hoàn thành hôm nay
            var completedStatuses = new[] { "Completed", "Giao thành công" };
            ViewBag.CompletedToday = _context.Orders
                .Count(o => o.CreatedAt >= todayStart && o.CreatedAt <= todayEnd
                         && completedStatuses.Contains(o.OrderStatus));

            // Sản phẩm sắp hết hàng (tồn kho <= 5)
            var lowStockItems = _context.Inventories
                .Include(i => i.Variant)
                    .ThenInclude(v => v.Product)
                .Where(i => i.StockQuantity <= 5 && i.StockQuantity > 0)
                .OrderBy(i => i.StockQuantity)
                .Take(10)
                .ToList();
            ViewBag.LowStockItems = lowStockItems;
            ViewBag.LowStockCount = _context.Inventories.Count(i => i.StockQuantity <= 5 && i.StockQuantity > 0);

            // Sản phẩm hết hàng hoàn toàn
            ViewBag.OutOfStockCount = _context.Inventories.Count(i => i.StockQuantity <= 0);

            // 10 đơn hàng mới nhất cần xử lý
            ViewBag.LatestPendingOrders = _context.Orders
                .Where(o => pendingStatuses.Contains(o.OrderStatus))
                .OrderByDescending(o => o.CreatedAt)
                .Take(10)
                .ToList();

            // 5 đơn hàng mới nhất (tất cả trạng thái)
            ViewBag.LatestOrders = _context.Orders
                .OrderByDescending(o => o.CreatedAt)
                .Take(5)
                .ToList();

            // Đánh giá chờ duyệt
            ViewBag.PendingReviews = _context.Reviews.Count(r => !r.IsApproved);

            ViewBag.EmployeeName = employeeName;

            return View();
        }
    }
}
