using BeautyShop.Data;
using BeautyShop.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BeautyShop.Controllers
{
    public class ReviewController : Controller
    {
        private readonly ApplicationDbContext _context;

    public ReviewController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Create(int productId)
        {
            var userId =
                HttpContext.Session.GetInt32("UserId");

            if (userId == null)
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            bool purchased =
                _context.OrderDetails
                .Include(x => x.Order)
                .Include(x => x.Variant)
                .Any(x =>
                    x.Order.UserId == userId &&
                    x.Order.OrderStatus == "Completed" &&
                    x.Variant.ProductId == productId);

            if (!purchased)
            {
                TempData["Error"] =
                    "Bạn chưa mua sản phẩm này.";

                return RedirectToAction(
                    "Detail",
                    "Product",
                    new { id = productId });
            }

            bool reviewed =
                _context.Reviews.Any(x =>
                    x.UserId == userId &&
                    x.ProductId == productId);

            if (reviewed)
            {
                TempData["Error"] =
                    "Bạn đã đánh giá sản phẩm này.";

                return RedirectToAction(
                    "Detail",
                    "Product",
                    new { id = productId });
            }

            var product =
                _context.Products
                .FirstOrDefault(x => x.Id == productId);

            ViewBag.Product = product;

            return View();
        }

        [HttpPost]
        public IActionResult Create(
      int productId,
      int rating,
      string comment)
        {
            var userId =
                HttpContext.Session.GetInt32("UserId");

            if (userId == null)
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            Review review = new Review
            {
                ProductId = productId,
                UserId = userId.Value,
                Rating = rating,
                Comment = comment,
                IsApproved = false,
                IsHidden = false
            };

            _context.Reviews.Add(review);
            _context.SaveChanges();

            TempData["Success"] =
                "Đánh giá đã được gửi thành công.";

            return RedirectToAction(
                "Detail",
                "Product",
                new { id = productId });
        }
    
    }

}
