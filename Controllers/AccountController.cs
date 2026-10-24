using BeautyShop.Data;
using BeautyShop.Models;
using BeautyShop.ViewModels;
using Microsoft.AspNetCore.Mvc;
using BCrypt.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using BeautyShop.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace BeautyShop.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AccountController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Login()
        {
            return View();
        }

        [HttpGet]
        public IActionResult AccessDenied(string? permission = null, string? returnUrl = null)
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            ViewBag.RequiredPermission = permission;
            ViewBag.RequiredPermissionName = !string.IsNullOrWhiteSpace(permission)
                ? AppPermissions.GetVietnameseName(permission)
                : "Quyền truy cập chức năng";
            ViewBag.ReturnUrl = returnUrl;
            return View("~/Views/Account/AccessDenied.cshtml");
        }

        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            if (_context.Users.Any(x => x.Email == model.Email))
            {
                ModelState.AddModelError("", "Email đã tồn tại");
                return View(model);
            }

            var customerRole = _context.Roles.FirstOrDefault(x => x.Name == "Customer");

            if (customerRole == null)
            {
                customerRole = new Role { Name = "Customer" };
                _context.Roles.Add(customerRole);
                _context.SaveChanges();
            }

            User user = new User
            {
                FullName = model.FullName,
                Email = model.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password),
                RoleId = customerRole.Id,
                CreatedAt = DateTime.Now,
                IsActive = true
            };

            _context.Users.Add(user);
            _context.SaveChanges();

            return RedirectToAction("Login");
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = _context.Users
                .Include(x => x.Role)
                    .ThenInclude(r => r!.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
                .Include(x => x.WarehouseScopes)
                .FirstOrDefault(x => x.Email == model.Email);

            if (user == null)
            {
                ModelState.AddModelError("", "Email không tồn tại");
                return View(model);
            }

            // Kiểm tra tài khoản bị khóa
            if (!user.IsActive)
            {
                ModelState.AddModelError("", "Tài khoản đã bị vô hiệu hóa. Vui lòng liên hệ quản trị viên.");
                return View(model);
            }

            bool checkPassword = BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash);

            if (!checkPassword)
            {
                ModelState.AddModelError("", "Sai mật khẩu");
                return View(model);
            }

            // 1. Ghi nhận dữ liệu đăng nhập vào Session (tương thích ngược cho Khách hàng & Giỏ hàng)
            HttpContext.Session.SetInt32("UserId", user.Id);
            HttpContext.Session.SetString("FullName", user.FullName);
            HttpContext.Session.SetString("Role", user.Role!.Name);

            // 2. Ghi nhận Cookie Authentication + Claims (Roles, Permissions, SecurityStamp, StoreScope)
            if (HttpContext.RequestServices?.GetService(typeof(IAuthenticationService)) != null)
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Name, user.FullName),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.Role, user.Role.Name),
                    new Claim("SecurityStamp", user.SecurityStamp ?? "")
                };

                if (user.Role.Name == AppRoles.Admin)
                {
                    foreach (var permCode in AppPermissions.All)
                    {
                        claims.Add(new Claim("Permission", permCode));
                    }
                }
                else if (user.Role.RolePermissions != null)
                {
                    foreach (var rp in user.Role.RolePermissions.Where(x => x.Permission != null))
                    {
                        claims.Add(new Claim("Permission", rp.Permission!.Code));
                    }
                }

                foreach (var ws in user.WarehouseScopes)
                {
                    claims.Add(new Claim("StoreScope", ws.StoreId.ToString()));
                }

                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var principal = new ClaimsPrincipal(identity);
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
            }

            // LOGIC KHÔI PHỤC GIỎ HÀNG TỪ DATABASE KHI ĐĂNG NHẬP THÀNH CÔNG
            var dbCart = _context.Carts
                .Include(c => c.CartItems!)
                    .ThenInclude(ci => ci.Variant)
                        .ThenInclude(v => v!.Product)
                .FirstOrDefault(c => c.UserId == user.Id);

            var sessionCart = new List<CartItemViewModel>();

            if (dbCart != null && dbCart.CartItems != null && dbCart.CartItems.Any())
            {
                foreach (var item in dbCart.CartItems)
                {
                    if (item.Variant != null && item.Variant.Product != null)
                    {
                        sessionCart.Add(new CartItemViewModel
                        {
                            ProductId = item.Variant.Product.Id,
                            VariantId = item.VariantId,
                            ProductName = item.Variant.Product.Name,
                            ImageUrl = item.Variant.Product.ThumbnailUrl ?? "",
                            Price = item.Variant.RetailPrice ?? 0,
                            Quantity = item.Quantity
                        });
                    }
                }
            }

            // Đồng bộ dữ liệu giỏ hàng tìm thấy vào lại Session "CART"
            HttpContext.Session.SetObject("CART", sessionCart);

            if (user.Role.Name == AppRoles.Admin)
            {
                return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
            }

            if (user.Role.Name == AppRoles.Warehouse)
            {
                return RedirectToAction("Index", "Inventory", new { area = "Admin" });
            }

            if (user.Role.Name == AppRoles.Cashier)
            {
                return RedirectToAction("Index", "Orders", new { area = "Admin" });
            }

            if (user.Role.Name == AppRoles.Shipper)
            {
                return RedirectToAction("MyTrips", "Delivery", new { area = "Admin" });
            }

            if (user.Role.Name == AppRoles.Employee)
            {
                return RedirectToAction("Index", "EmployeeDashboard", new { area = "Admin" });
            }

            return RedirectToAction("Index", "Home");
        }

        public async Task<IActionResult> Logout()
        {
            HttpContext.Session.Clear();
            if (HttpContext.RequestServices?.GetService(typeof(IAuthenticationService)) != null)
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
            return RedirectToAction("Login");
        }

        public IActionResult Profile()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return RedirectToAction("Login");

            var user = _context.Users.FirstOrDefault(x => x.Id == userId);
            return View(user);
        }

        public IActionResult EditProfile()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return RedirectToAction("Login");

            var user = _context.Users.FirstOrDefault(x => x.Id == userId);
            if (user == null)
                return RedirectToAction("Profile");

            var model = new EditProfileViewModel
            {
                FullName = user.FullName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber
            };

            return View(model);
        }

        [HttpPost]
        public IActionResult EditProfile(EditProfileViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return RedirectToAction("Login");

            var user = _context.Users.FirstOrDefault(x => x.Id == userId);
            if (user == null)
                return RedirectToAction("Profile");

            user.FullName = model.FullName;
            user.Email = model.Email;
            user.PhoneNumber = model.PhoneNumber;

            _context.SaveChanges();

            HttpContext.Session.SetString("FullName", user.FullName);
            return RedirectToAction("Profile");
        }

        public IActionResult ChangePassword()
        {
            return View();
        }

        [HttpPost]
        public IActionResult ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return RedirectToAction("Login");

            var user = _context.Users.FirstOrDefault(x => x.Id == userId);
            if (user == null)
                return RedirectToAction("Login");

            bool checkPassword = BCrypt.Net.BCrypt.Verify(model.CurrentPassword, user.PasswordHash);

            if (!checkPassword)
            {
                ModelState.AddModelError("", "Mật khẩu hiện tại không đúng");
                return View(model);
            }

            if (model.NewPassword != model.ConfirmPassword)
            {
                ModelState.AddModelError("", "Xác nhận mật khẩu không khớp");
                return View(model);
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
            _context.SaveChanges();

            ViewBag.Success = true;
            return View();
        }

        // =============================================
        // TÍNH NĂNG QUÊN MẬT KHẨU (FORGOT & RESET PASSWORD)
        // =============================================
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        public IActionResult ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = _context.Users.FirstOrDefault(x => x.Email == model.Email);
            if (user == null)
            {
                ModelState.AddModelError("", "Email này chưa được đăng ký trong hệ thống.");
                return View(model);
            }

            string token = Guid.NewGuid().ToString("N");
            HttpContext.Session.SetString("ResetEmail", user.Email);
            HttpContext.Session.SetString("ResetToken", token);

            return RedirectToAction("ResetPassword", new { email = user.Email, token = token });
        }

        public IActionResult ResetPassword(string email, string token)
        {
            var savedEmail = HttpContext.Session.GetString("ResetEmail");
            var savedToken = HttpContext.Session.GetString("ResetToken");

            if (string.IsNullOrEmpty(savedEmail) || string.IsNullOrEmpty(savedToken) || savedEmail != email || savedToken != token)
            {
                TempData["ErrorMessage"] = "Yêu cầu khôi phục mật khẩu không hợp lệ hoặc đã hết hạn.";
                return RedirectToAction("ForgotPassword");
            }

            var model = new ResetPasswordViewModel
            {
                Email = email,
                Token = token
            };

            return View(model);
        }

        [HttpPost]
        public IActionResult ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var savedEmail = HttpContext.Session.GetString("ResetEmail");
            var savedToken = HttpContext.Session.GetString("ResetToken");

            if (string.IsNullOrEmpty(savedEmail) || string.IsNullOrEmpty(savedToken) || savedEmail != model.Email || savedToken != model.Token)
            {
                ModelState.AddModelError("", "Yêu cầu đặt lại mật khẩu đã hết hạn. Vui lòng thử lại.");
                return View(model);
            }

            var user = _context.Users.FirstOrDefault(x => x.Email == model.Email);
            if (user == null)
            {
                ModelState.AddModelError("", "Không tìm thấy thông tin tài khoản.");
                return View(model);
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
            _context.SaveChanges();

            HttpContext.Session.Remove("ResetEmail");
            HttpContext.Session.Remove("ResetToken");

            TempData["SuccessMessage"] = "Đặt lại mật khẩu thành công! Vui lòng đăng nhập bằng mật khẩu mới.";
            return RedirectToAction("Login");
        }

        // =============================================
        // TRANG ĐƠN HÀNG CỦA TÔI
        // =============================================
        public IActionResult MyOrders()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return RedirectToAction("Login");

            var orders = _context.Orders
                .Include(x => x.OrderDetails!)
                    .ThenInclude(x => x.Variant!)
                        .ThenInclude(x => x.Product)
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.CreatedAt)
                .ToList();

            return View(orders);
        }

        public IActionResult MyOrderDetail(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return RedirectToAction("Login");

            var order = _context.Orders
                .Include(x => x.OrderDetails!)
                    .ThenInclude(x => x.Variant!)
                        .ThenInclude(x => x.Product)
                .FirstOrDefault(x => x.Id == id && x.UserId == userId);

            if (order == null)
                return RedirectToAction("MyOrders");

            return View(order);
        }
    }
}