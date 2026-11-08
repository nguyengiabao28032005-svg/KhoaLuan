using BeautyShop.Data;
using BeautyShop.Helpers;
using BeautyShop.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using BeautyShop.Models;

namespace BeautyShop.Controllers
{
    public class CartController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CartController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
            {
                TempData["Message"] = "Vui lòng đăng nhập để thêm sản phẩm vào giỏ hàng";
                return RedirectToAction("Login", "Account");
            }

            var cart = HttpContext.Session.GetObject<List<CartItemViewModel>>("CART") ?? new List<CartItemViewModel>();
            return View(cart);
        }

        public IActionResult AddToCart(int productId, int quantity = 1, int? variantId = null)
        {
            // 1. Kiểm tra trạng thái đăng nhập để lấy UserId
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers["Accept"].ToString().Contains("application/json"))
                {
                    return Json(new { success = false, requireLogin = true, message = "Vui lòng đăng nhập để thêm sản phẩm vào giỏ hàng." });
                }
                TempData["Message"] = "Vui lòng đăng nhập để thêm sản phẩm vào giỏ hàng";
                return RedirectToAction("Login", "Account");
            }

            var cart = HttpContext.Session.GetObject<List<CartItemViewModel>>("CART") ?? new List<CartItemViewModel>();

            var product = _context.Products
                .Include(x => x.ProductVariants)
                .FirstOrDefault(x => x.Id == productId);

            if (product == null)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers["Accept"].ToString().Contains("application/json"))
                    return Json(new { success = false, message = "Không tìm thấy sản phẩm." });
                return RedirectToAction("Index", "Product");
            }

            // Ưu tiên lấy biến thể theo variantId được gửi từ form, fallback sang biến thể đầu tiên
            var variant = (variantId.HasValue && variantId.Value > 0)
                ? product.ProductVariants!.FirstOrDefault(v => v.Id == variantId.Value)
                : null;
            variant ??= product.ProductVariants!.FirstOrDefault();

            if (variant == null)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers["Accept"].ToString().Contains("application/json"))
                    return Json(new { success = false, message = "Sản phẩm hiện chưa có phân loại bán." });
                return RedirectToAction("Index", "Product");
            }

            // Chặn mua nếu biến thể chưa được thiết lập giá bán
            if (!variant.HasPrice)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers["Accept"].ToString().Contains("application/json"))
                    return Json(new { success = false, message = "Sản phẩm này chưa được thiết lập giá bán, hiện chưa thể mua." });
                TempData["ErrorMessage"] = "Sản phẩm này chưa được thiết lập giá bán, hiện chưa thể mua.";
                return RedirectToAction("Detail", "Product", new { id = productId });
            }

            // Tìm item trong giỏ theo cả productId VÀ variantId để hỗ trợ nhiều biến thể cùng sản phẩm
            var item = cart.FirstOrDefault(x => x.ProductId == productId && x.VariantId == variant.Id);

            if (item != null)
            {
                item.Quantity += quantity;
            }
            else
            {
                cart.Add(new CartItemViewModel
                {
                    ProductId = product.Id,
                    VariantId = variant.Id,
                    ProductName = product.Name,
                    ImageUrl = !string.IsNullOrEmpty(variant.ImageUrl) ? variant.ImageUrl : (product.ThumbnailUrl ?? ""),
                    Price = variant.RetailPrice ?? 0,
                    Quantity = quantity
                });
            }

            // Cập nhật lại giỏ hàng trong Session để UI hiển thị ngay lập tức
            HttpContext.Session.SetObject("CART", cart);

            // =========================================================
            // ĐỒNG BỘ TỰ ĐỘNG XUỐNG DATABASE
            // =========================================================
            // Lấy hoặc tạo mới giỏ hàng dưới DB cho User này nếu chưa có
            var dbCart = _context.Carts.FirstOrDefault(c => c.UserId == userId);
            if (dbCart == null)
            {
                dbCart = new Cart
                {
                    UserId = userId,
                    CreatedAt = DateTime.Now
                };
                _context.Carts.Add(dbCart);
                _context.SaveChanges();
            }

            // Kiểm tra xem biến thể sản phẩm này đã nằm trong giỏ hàng DB chưa
            var dbCartItem = _context.CartItems
                .FirstOrDefault(ci => ci.CartId == dbCart.Id && ci.VariantId == variant.Id);

            if (dbCartItem != null)
            {
                dbCartItem.Quantity += quantity; // Cộng dồn số lượng
            }
            else
            {
                _context.CartItems.Add(new CartItem
                {
                    CartId = dbCart.Id,
                    VariantId = variant.Id,
                    Quantity = quantity
                });
            }
            _context.SaveChanges();
            // =========================================================

            // --- BẮT ĐẦU ĐOẠN LƯU VẾT "ADDTOCART" ---
            if (userId.HasValue)
            {
                _context.UserBehaviors.Add(new BeautyShop.Models.UserBehavior
                {
                    UserId = userId.Value,
                    ProductId = productId,
                    ActionType = "AddToCart",
                    CreatedAt = DateTime.Now
                });
                _context.SaveChanges();
            }
            // --- KẾT THÚC ĐOẠN LƯU VẾT ---
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers["Accept"].ToString().Contains("application/json"))
            {
                int totalQty = cart.Sum(x => x.Quantity);
                return Json(new
                {
                    success = true,
                    message = $"Đã thêm {quantity} sản phẩm \"{product.Name}\" vào giỏ hàng!",
                    totalQuantity = totalQty,
                    itemCount = cart.Count,
                    productName = product.Name,
                    price = variant.RetailPrice
                });
            }

            return RedirectToAction("Index");
        }

        public IActionResult Remove(int productId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var cart = HttpContext.Session.GetObject<List<CartItemViewModel>>("CART") ?? new List<CartItemViewModel>();
            var item = cart.FirstOrDefault(x => x.ProductId == productId);

            if (item != null)
            {
                // 1. Xóa khỏi Session phục vụ UI nhanh
                cart.Remove(item);
                HttpContext.Session.SetObject("CART", cart);

                // =========================================================
                // ĐỒNG BỘ XÓA KHỎI DATABASE
                // =========================================================
                if (userId != null)
                {
                    var dbCart = _context.Carts.FirstOrDefault(c => c.UserId == userId);
                    if (dbCart != null)
                    {
                        var dbCartItem = _context.CartItems
                            .FirstOrDefault(ci => ci.CartId == dbCart.Id && ci.VariantId == item.VariantId);

                        if (dbCartItem != null)
                        {
                            _context.CartItems.Remove(dbCartItem);
                            _context.SaveChanges();
                        }
                    }
                }
                // =========================================================
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult UpdateQuantity(int productId, int quantity)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var cart = HttpContext.Session.GetObject<List<CartItemViewModel>>("CART") ?? new List<CartItemViewModel>();
            var item = cart.FirstOrDefault(x => x.ProductId == productId);

            if (item == null || quantity < 1)
            {
                return Json(new { success = false, message = "Sản phẩm không tồn tại hoặc số lượng không hợp lệ." });
            }

            // 1. Cập nhật số lượng mới trong Session phục vụ UI nhanh
            item.Quantity = quantity;
            HttpContext.Session.SetObject("CART", cart);

            // =========================================================
            // ĐỒNG BỘ CẬP NHẬT SỐ LƯỢNG MỚI XUỐNG DATABASE
            // =========================================================
            if (userId != null)
            {
                var dbCart = _context.Carts.FirstOrDefault(c => c.UserId == userId);
                if (dbCart != null)
                {
                    var dbCartItem = _context.CartItems
                        .FirstOrDefault(ci => ci.CartId == dbCart.Id && ci.VariantId == item.VariantId);

                    if (dbCartItem != null)
                    {
                        dbCartItem.Quantity = quantity; // Ghi đè số lượng mới từ AJAX gửi lên
                        _context.SaveChanges();
                    }
                }
            }
            // =========================================================

            // Tính toán dữ liệu mới để trả về cho kết quả xử lý AJAX thành công
            decimal itemTotal = item.Total;
            decimal cartTotal = cart.Sum(x => x.Total);
            int totalQuantity = cart.Sum(x => x.Quantity);

            return Json(new
            {
                success = true,
                itemTotal = itemTotal,
                cartTotal = cartTotal,
                totalQuantity = totalQuantity
            });
        }

        public IActionResult AddCombo(string productIds)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                TempData["Message"] = "Vui lòng đăng nhập để mua combo";
                return RedirectToAction("Login", "Account");
            }

            if (string.IsNullOrEmpty(productIds))
            {
                return RedirectToAction("Index", "Home");
            }

            var ids = productIds.Split(',')
                .Select(id => int.TryParse(id, out var parsed) ? parsed : 0)
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            var cart = HttpContext.Session.GetObject<List<CartItemViewModel>>("CART") ?? new List<CartItemViewModel>();

            var dbCart = _context.Carts.FirstOrDefault(c => c.UserId == userId);
            if (dbCart == null)
            {
                dbCart = new Cart
                {
                    UserId = userId,
                    CreatedAt = DateTime.Now
                };
                _context.Carts.Add(dbCart);
                _context.SaveChanges();
            }

            int discountPct = ids.Count == 2 ? 8 : (ids.Count == 3 ? 12 : (ids.Count >= 4 ? 15 : 0));

            foreach (var productId in ids)
            {
                var product = _context.Products
                    .Include(x => x.ProductVariants)
                    .FirstOrDefault(x => x.Id == productId);

                if (product == null) continue;

                var variant = product.ProductVariants!.FirstOrDefault();
                if (variant == null || !variant.HasPrice) continue;

                decimal basePrice = variant.RetailPrice ?? 0;
                decimal comboItemPrice = basePrice;
                if (discountPct > 0)
                {
                    comboItemPrice = Math.Round((basePrice * (1 - discountPct / 100m)) / 1000m) * 1000m;
                }

                var item = cart.FirstOrDefault(x => x.ProductId == productId);

                if (item != null)
                {
                    item.Quantity += 1;
                    item.Price = comboItemPrice; // Áp dụng giá combo
                }
                else
                {
                    cart.Add(new CartItemViewModel
                    {
                        ProductId = product.Id,
                        VariantId = variant.Id,
                        ProductName = product.Name,
                        ImageUrl = product.ThumbnailUrl ?? "",
                        Price = comboItemPrice,
                        Quantity = 1
                    });
                }

                var dbCartItem = _context.CartItems
                    .FirstOrDefault(ci => ci.CartId == dbCart.Id && ci.VariantId == variant.Id);

                if (dbCartItem != null)
                {
                    dbCartItem.Quantity += 1;
                }
                else
                {
                    _context.CartItems.Add(new CartItem
                    {
                        CartId = dbCart.Id,
                        VariantId = variant.Id,
                        Quantity = 1
                    });
                }

                _context.UserBehaviors.Add(new BeautyShop.Models.UserBehavior
                {
                    UserId = userId.Value,
                    ProductId = productId,
                    ActionType = "AddToCart",
                    CreatedAt = DateTime.Now
                });
            }

            HttpContext.Session.SetObject("CART", cart);
            _context.SaveChanges();

            return RedirectToAction("Index");
        }
    }
}