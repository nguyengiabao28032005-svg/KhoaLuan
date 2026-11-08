using BeautyShop.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BeautyShop.Controllers
{
    public class OrderController : Controller
    {
        private readonly ApplicationDbContext _context;

        public OrderController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return RedirectToAction("Login", "Account");

            var orders = _context.Orders
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.CreatedAt)
                .ToList();

            return View(orders);
        }

        public IActionResult Detail(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return RedirectToAction("Login", "Account");

            var order = _context.Orders
                .Include(x => x.OrderDetails)
                .ThenInclude(x => x.Variant)
                .ThenInclude(x => x.Product)
                .FirstOrDefault(x => x.Id == id && x.UserId == userId);

            if (order == null)
                return NotFound();

            return View(order);
        }

        // =============================================
        // HUY DON HANG - Chi cho phep khi don chua duoc Admin duyet
        // (OrderStatus = "Pending" hoac "Cho xac nhan")
        // =============================================
        [HttpPost]
        public IActionResult Cancel(int id, string? reason)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return RedirectToAction("Login", "Account");

            var order = _context.Orders
                .FirstOrDefault(x => x.Id == id && x.UserId == userId);

            if (order == null)
            {
                TempData["ErrorMessage"] = "Khong tim thay don hang.";
                return RedirectToAction("Index");
            }

            // Chi cho phep huy khi don dang o trang thai cho xu ly
            var cancellableStatuses = new[] { "Pending", "Cho xac nhan" };
            if (!cancellableStatuses.Contains(order.OrderStatus))
            {
                TempData["ErrorMessage"] = "Don hang nay khong the huy vi da duoc xu ly hoac dang giao.";
                return RedirectToAction("Detail", new { id = id });
            }

            // Cap nhat trang thai sang "Da huy"
            order.OrderStatus = "Da huy";
            order.UpdatedAt = DateTime.Now;

            // Luu ly do huy vao CancelReason neu model co truong nay
            // Neu chua co truong nay thi bo qua
            _context.SaveChanges();

            TempData["SuccessMessage"] = "Don hang #" + order.OrderCode + " da duoc huy thanh cong.";
            return RedirectToAction("Index");
        }
    }
}
