using BeautyShop.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BeautyShop.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class ReviewsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReviewsController(ApplicationDbContext context)
        {
            _context = context;
        }

        private bool IsStaff()
        {
            var role = HttpContext.Session.GetString("Role");
            return role == "Admin" || role == "Employee";
        }

        public IActionResult Index()
        {
            if (!IsStaff()) return RedirectToAction("Index", "Home", new { area = "" });

            var reviews = _context.Reviews
                .Include(x => x.User)
                .Include(x => x.Product)
                .OrderByDescending(x => x.CreatedAt)
                .ToList();

            return View(reviews);
        }

        public IActionResult ToggleApprove(int id)
        {
            if (!IsStaff()) return RedirectToAction("Index", "Home", new { area = "" });

            var review = _context.Reviews
                .FirstOrDefault(x => x.Id == id);

            if (review == null)
                return RedirectToAction(nameof(Index));

            review.IsApproved = !review.IsApproved;

            _context.SaveChanges();

            return RedirectToAction(nameof(Index));
        }

        public IActionResult Detail(int id)
        {
            if (!IsStaff()) return RedirectToAction("Index", "Home", new { area = "" });

            var review = _context.Reviews
                .Include(x => x.User)
                .Include(x => x.Product)
                .FirstOrDefault(x => x.Id == id);

            if (review == null)
                return RedirectToAction(nameof(Index));

            return View(review);
        }
    }
}