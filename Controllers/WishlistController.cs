using BeautyShop.Data;
using BeautyShop.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BeautyShop.Controllers
{
    public class WishlistController : Controller
    {
        private readonly ApplicationDbContext _context;


    public WishlistController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            var userId =
                HttpContext.Session.GetInt32("UserId");

            if (userId == null)
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            var wishlist = _context.Wishlists
                .Include(x => x.WishlistItems)
                .ThenInclude(x => x.Product)
                .FirstOrDefault(x => x.UserId == userId);

            return View(wishlist);
        }

        public IActionResult Add(int productId)
        {
            var userId =
                HttpContext.Session.GetInt32("UserId");

            if (userId == null)
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            var wishlist = _context.Wishlists
                .Include(x => x.WishlistItems)
                .FirstOrDefault(x => x.UserId == userId);

            if (wishlist == null)
            {
                wishlist = new Wishlist
                {
                    UserId = userId.Value
                };

                _context.Wishlists.Add(wishlist);
                _context.SaveChanges();
            }

            bool exists =
                wishlist.WishlistItems?
                .Any(x => x.ProductId == productId)
                ?? false;

            if (!exists)
            {
                _context.WishlistItems.Add(
                    new WishlistItem
                    {
                        WishlistId = wishlist.Id,
                        ProductId = productId
                    });

                _context.SaveChanges();
            }
            // --- BẮT ĐẦU ĐOẠN LƯU VẾT "ADDWISHLIST" ---
            _context.UserBehaviors.Add(new BeautyShop.Models.UserBehavior
            {
                UserId = userId.Value,
                ProductId = productId,
                ActionType = "AddWishlist",
                CreatedAt = DateTime.Now
            });
            _context.SaveChanges();
            // --- KẾT THÚC ĐOẠN LƯU VẾT ---

            return Redirect(
                Request.Headers["Referer"].ToString());
        }

        public IActionResult Remove(int id)
        {
            var item =
                _context.WishlistItems.Find(id);

            if (item != null)
            {
                _context.WishlistItems.Remove(item);
                _context.SaveChanges();
            }

            return RedirectToAction(nameof(Index));
        }
    }


}
