using BeautyShop.Data;
using BeautyShop.Helpers;
using BeautyShop.Models;
using BeautyShop.Services;
using BeautyShop.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BeautyShop.Controllers
{
    public class CheckoutController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;

        private readonly IDeliveryOptimizationService _deliveryService;

        public CheckoutController(ApplicationDbContext context, IEmailService emailService, IDeliveryOptimizationService deliveryService)
        {
            _context = context;
            _emailService = emailService;
            _deliveryService = deliveryService;
        }

        public IActionResult Index()
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var cart = HttpContext.Session.GetObject<List<CartItemViewModel>>("CART") ?? new List<CartItemViewModel>();

            if (!cart.Any())
            {
                return RedirectToAction("Index", "Cart");
            }

            // Chặn thanh toán nếu trong giỏ có sản phẩm chưa thiết lập giá bán
            var getVariantIds = cart.Select(c => c.VariantId).Distinct().ToList();
            var hasUnpricedInGet = _context.ProductVariants
                .Any(v => getVariantIds.Contains(v.Id) && (v.RetailPrice == null || v.RetailPrice <= 0));
            if (hasUnpricedInGet || cart.Any(c => c.Price <= 0))
            {
                TempData["ErrorMessage"] = "Giỏ hàng có sản phẩm chưa được thiết lập giá bán. Vui lòng loại bỏ trước khi thanh toán.";
                return RedirectToAction("Index", "Cart");
            }

            decimal cartTotal = cart.Sum(x => x.Total);
            ViewBag.CartTotal = cartTotal;

            int? currentUserId = HttpContext.Session.GetInt32("UserId");
            var now = DateTime.Now;
            if (currentUserId.HasValue)
            {
                ViewBag.MyVouchers = _context.UserVouchers
                    .Include(uv => uv.Voucher)
                    .Where(uv => uv.UserId == currentUserId.Value && !uv.IsUsed && uv.Voucher != null && uv.Voucher.IsActive && now >= uv.Voucher.StartDate && now <= uv.Voucher.EndDate && (uv.Voucher.TotalUsageLimit == null || uv.Voucher.UsedCount < uv.Voucher.TotalUsageLimit))
                    .Select(uv => uv.Voucher!)
                    .ToList();
            }

            // Tính toán ca giao dự kiến theo quy tắc giờ chốt 10:00 (Bước 5)
            var schedule = DeliveryConfig.GetDefaultDeliverySchedule(DateTime.Now);
            ViewBag.DefaultExpectedDate = schedule.ExpectedDate;
            ViewBag.DefaultShift = schedule.Shift;
            ViewBag.DefaultShiftName = schedule.ShiftDisplayName;
            ViewBag.DeliveryReason = schedule.Reason;
            ViewBag.DeliveryZones = _context.DeliveryZones.Where(z => z.IsActive).OrderBy(z => z.SortOrder).ToList();

            return View(new CheckoutViewModel());
        }

        [HttpPost]
        public IActionResult Index(CheckoutViewModel model, string? OrderCode, string? VoucherCode)
        {
            if (!ModelState.IsValid)
            {
                var currentCart = HttpContext.Session.GetObject<List<CartItemViewModel>>("CART") ?? new List<CartItemViewModel>();
                ViewBag.CartTotal = currentCart.Sum(x => x.Total);
                return View(model);
            }

            var cart = HttpContext.Session.GetObject<List<CartItemViewModel>>("CART") ?? new List<CartItemViewModel>();

            if (!cart.Any())
            {
                return RedirectToAction("Index", "Cart");
            }

            // Kiểm tra nghiêm ngặt ở bước tạo đơn: Chặn biến thể chưa thiết lập giá bán
            var postVariantIds = cart.Select(c => c.VariantId).Distinct().ToList();
            var hasUnpricedInPost = _context.ProductVariants
                .Any(v => postVariantIds.Contains(v.Id) && (v.RetailPrice == null || v.RetailPrice <= 0));
            if (hasUnpricedInPost || cart.Any(c => c.Price <= 0))
            {
                TempData["ErrorMessage"] = "Đơn hàng chứa sản phẩm chưa được thiết lập giá bán hợp lệ nên không thể hoàn tất đặt hàng.";
                return RedirectToAction("Index", "Cart");
            }

            int? userId = HttpContext.Session.GetInt32("UserId");
            decimal total = cart.Sum(x => x.Total);

            decimal deliveryFee = 30000;
            if (int.TryParse(Request.Form["ShippingFee"], out int parsedFee) && parsedFee > 0)
                deliveryFee = parsedFee;

            int toDistrictId = 0;
            int.TryParse(Request.Form["ToDistrictId"], out toDistrictId);
            string toWardCode = Request.Form["ToWardCode"].ToString() ?? "";

            string finalOrderCode = !string.IsNullOrEmpty(OrderCode)
                ? OrderCode
                : "ORD" + DateTime.Now.ToString("yyyyMMddHHmmss");

            decimal discountAmount = 0;
            if (!string.IsNullOrEmpty(VoucherCode))
            {
                var cleanCode = VoucherCode.Trim().ToUpper();
                var voucher = _context.Vouchers.FirstOrDefault(v => v.Code == cleanCode && v.IsActive);
                if (voucher != null && DateTime.Now >= voucher.StartDate && DateTime.Now <= voucher.EndDate)
                {
                    if (voucher.TotalUsageLimit == null || voucher.UsedCount < voucher.TotalUsageLimit)
                    {
                        if (total >= voucher.MinOrderValue)
                        {
                            if (voucher.DiscountType == "Fixed") discountAmount = voucher.DiscountValue;
                            else if (voucher.DiscountType == "Percent") 
                            {
                                discountAmount = total * (voucher.DiscountValue / 100m);
                                if (voucher.MaxDiscountAmount.HasValue && voucher.MaxDiscountAmount.Value > 0 && discountAmount > voucher.MaxDiscountAmount.Value)
                                    discountAmount = voucher.MaxDiscountAmount.Value;
                            }

                            if (discountAmount > total) discountAmount = total;

                            voucher.UsedCount += 1;

                            // Cập nhật UserVoucher của khách hàng nếu đã đăng nhập
                            if (userId.HasValue)
                            {
                                var uv = _context.UserVouchers.FirstOrDefault(x => x.UserId == userId.Value && x.VoucherId == voucher.Id && !x.IsUsed);
                                if (uv != null)
                                {
                                    uv.IsUsed = true;
                                    uv.UsedAt = DateTime.Now;
                                }
                                else
                                {
                                    _context.UserVouchers.Add(new UserVoucher
                                    {
                                        UserId = userId.Value,
                                        VoucherId = voucher.Id,
                                        IsUsed = true,
                                        UsedAt = DateTime.Now,
                                        CreatedAt = DateTime.Now
                                    });
                                }
                            }
                        }
                    }
                }
            }

            // ===== QUY TẮC KHUNG THỜI GIAN VÀ KHU VỰC GIAO HÀNG (BƯỚC 5) =====
            DateTime? expectedDeliveryDate = null;
            if (DateTime.TryParse(Request.Form["ExpectedDeliveryDate"], out DateTime parsedDate))
            {
                expectedDeliveryDate = parsedDate.Date;
            }
            string? deliveryShift = Request.Form["DeliveryShift"].ToString();

            // Nếu khách không chọn -> Tự động gán theo quy tắc giờ chốt 10:00
            if (!expectedDeliveryDate.HasValue || string.IsNullOrWhiteSpace(deliveryShift))
            {
                var def = DeliveryConfig.GetDefaultDeliverySchedule(DateTime.Now);
                expectedDeliveryDate = def.ExpectedDate;
                deliveryShift = def.Shift;
            }

            // Tự động nhận diện khu vực nếu chưa gán
            int? deliveryZoneId = null;
            if (int.TryParse(Request.Form["DeliveryZoneId"], out int parsedZoneId) && parsedZoneId > 0)
            {
                deliveryZoneId = parsedZoneId;
            }
            else
            {
                var detected = _deliveryService.DetectZoneAsync(model.ShippingAddress).GetAwaiter().GetResult();
                if (detected != null) deliveryZoneId = detected.Id;
            }

            // 1. Tạo và lưu thông tin Đơn hàng (Gán StoreId theo địa chỉ giao để lọc phạm vi kho ở Bước 6)
            int assignedStoreId = RoleAndScopeSeeder.DetermineStoreIdFromAddress(model.ShippingAddress);
            Order order = new Order
            {
                OrderCode = finalOrderCode,
                UserId = userId,
                StoreId = assignedStoreId,
                CustomerName = model.CustomerName,
                PhoneNumber = model.PhoneNumber,
                ShippingAddress = model.ShippingAddress,
                TotalAmount = total,
                DeliveryFee = deliveryFee,
                DiscountAmount = discountAmount,
                FinalAmount = Math.Max(0, total + deliveryFee - discountAmount),
                OrderStatus = "Pending",
                PaymentMethod = model.PaymentMethod,
                PaymentStatus = model.PaymentMethod == "ChuyenKhoan" ? "Awaiting Payment" : "Unpaid",
                ToDistrictId = toDistrictId,
                ToWardCode = toWardCode,
                DeliveryZoneId = deliveryZoneId,
                ExpectedDeliveryDate = expectedDeliveryDate,
                DeliveryShift = deliveryShift,
                DeliveryStatus = "Ready",
                FailedDeliveryAttempts = 0,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            _context.Orders.Add(order);
            _context.SaveChanges();

            // 2. Lưu Chi tiết đơn hàng
            foreach (var item in cart)
            {
                OrderDetail detail = new OrderDetail
                {
                    OrderId = order.Id,
                    VariantId = item.VariantId,
                    Quantity = item.Quantity,
                    UnitPrice = item.Price
                };

                _context.OrderDetails.Add(detail);
            }
            _context.SaveChanges();

            // 3. Xóa sạch giỏ hàng dưới Database
            if (userId.HasValue)
            {
                var dbCart = _context.Carts.FirstOrDefault(c => c.UserId == userId.Value);
                if (dbCart != null)
                {
                    var dbCartItems = _context.CartItems.Where(ci => ci.CartId == dbCart.Id).ToList();
                    if (dbCartItems.Any())
                    {
                        _context.CartItems.RemoveRange(dbCartItems);
                        _context.SaveChanges();
                    }
                }
            }

            // 4. Xóa sạch giỏ hàng trên Session
            HttpContext.Session.Remove("CART");

            // 5. GỬI EMAIL XÁC NHẬN ĐẶT HÀNG TỰ ĐỘNG KHÔNG LÀM CHẬM RESPONSE
            var userObj = userId.HasValue ? _context.Users.FirstOrDefault(u => u.Id == userId.Value) : null;
            string recipientEmail = userObj?.Email ?? "";
            if (!string.IsNullOrEmpty(recipientEmail))
            {
                var fullOrder = _context.Orders
                    .Include(o => o.OrderDetails!)
                        .ThenInclude(d => d.Variant!)
                            .ThenInclude(v => v.Product)
                    .FirstOrDefault(o => o.Id == order.Id);

                if (fullOrder != null)
                {
                    _emailService.SendOrderConfirmationEmailAsync(fullOrder, recipientEmail);
                }
            }

            return RedirectToAction("Success", new { id = order.Id });
        }

        public IActionResult Success(int id)
        {
            var order = _context.Orders
                .Include(x => x.OrderDetails!)
                    .ThenInclude(x => x.Variant!)
                        .ThenInclude(x => x.Product)
                .FirstOrDefault(x => x.Id == id);

            if (order == null)
                return RedirectToAction("Index", "Home");

            return View(order);
        }

        // POST: /Checkout/ApplyVoucher
        [HttpPost]
        public IActionResult ApplyVoucher(string code, decimal total)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return Json(new { success = false, message = "Vui lòng nhập mã giảm giá." });
            }

            string cleanCode = code.Trim().ToUpper();
            var voucher = _context.Vouchers.FirstOrDefault(v => v.Code == cleanCode);

            if (voucher == null || !voucher.IsActive)
            {
                return Json(new { success = false, message = "❌ Mã giảm giá không tồn tại hoặc đã ngừng hoạt động." });
            }

            var now = DateTime.Now;
            if (now < voucher.StartDate)
            {
                return Json(new { success = false, message = $"❌ Mã giảm giá chưa đến ngày áp dụng (bắt đầu từ {voucher.StartDate:dd/MM/yyyy HH:mm})." });
            }

            if (now > voucher.EndDate)
            {
                return Json(new { success = false, message = "❌ Voucher đã hết hạn sử dụng." });
            }

            if (voucher.TotalUsageLimit.HasValue && voucher.UsedCount >= voucher.TotalUsageLimit.Value)
            {
                return Json(new { success = false, message = "❌ Voucher đã hết lượt sử dụng." });
            }

            if (total < voucher.MinOrderValue)
            {
                return Json(new { success = false, message = $"❌ Đơn hàng chưa đạt giá trị tối thiểu {voucher.MinOrderValue:N0}đ." });
            }

            int? userId = HttpContext.Session.GetInt32("UserId");
            if (userId.HasValue)
            {
                int usedByUser = _context.UserVouchers.Count(uv => uv.UserId == userId.Value && uv.VoucherId == voucher.Id && uv.IsUsed);
                if (usedByUser >= voucher.MaxUsesPerUser)
                {
                    return Json(new { success = false, message = $"❌ Bạn đã sử dụng hết số lần cho phép của mã này (Tối đa {voucher.MaxUsesPerUser} lần/khách)." });
                }
            }

            decimal discountAmount = 0;
            if (voucher.DiscountType == "Fixed")
            {
                discountAmount = voucher.DiscountValue;
                if (discountAmount > total) discountAmount = total;
            }
            else if (voucher.DiscountType == "Percent")
            {
                discountAmount = total * (voucher.DiscountValue / 100m);
                if (voucher.MaxDiscountAmount.HasValue && voucher.MaxDiscountAmount.Value > 0 && discountAmount > voucher.MaxDiscountAmount.Value)
                {
                    discountAmount = voucher.MaxDiscountAmount.Value;
                }
            }

            decimal finalTotal = total - discountAmount;
            if (finalTotal < 0) finalTotal = 0;

            return Json(new
            {
                success = true,
                code = voucher.Code,
                discount = discountAmount,
                discountFormatted = discountAmount.ToString("N0") + "đ",
                finalTotal = finalTotal,
                message = $"Áp dụng mã {voucher.Code} thành công! Đã giảm {discountAmount:N0}đ."
            });
        }

        // GET: /Checkout/GetAvailableVouchers
        [HttpGet]
        public IActionResult GetAvailableVouchers(decimal total)
        {
            var now = DateTime.Now;
            int? userId = HttpContext.Session.GetInt32("UserId");

            var vouchers = _context.Vouchers
                .Where(v => v.IsActive && v.StartDate <= now && v.EndDate >= now)
                .OrderByDescending(v => v.DiscountValue)
                .ToList();

            var result = vouchers.Select(v =>
            {
                bool isUsedByUser = false;
                if (userId.HasValue)
                {
                    int usedCount = _context.UserVouchers.Count(uv => uv.UserId == userId.Value && uv.VoucherId == v.Id && uv.IsUsed);
                    isUsedByUser = (usedCount >= v.MaxUsesPerUser);
                }

                bool isExhausted = v.TotalUsageLimit.HasValue && v.UsedCount >= v.TotalUsageLimit.Value;
                bool isEligible = total >= v.MinOrderValue && !isUsedByUser && !isExhausted;

                decimal calculatedDiscount = 0;
                if (v.DiscountType == "Fixed")
                {
                    calculatedDiscount = v.DiscountValue;
                }
                else
                {
                    calculatedDiscount = total * (v.DiscountValue / 100m);
                    if (v.MaxDiscountAmount.HasValue && calculatedDiscount > v.MaxDiscountAmount.Value)
                        calculatedDiscount = v.MaxDiscountAmount.Value;
                }

                return new
                {
                    id = v.Id,
                    code = v.Code,
                    name = v.Name,
                    discountType = v.DiscountType,
                    discountValue = v.DiscountValue,
                    minOrderValue = v.MinOrderValue,
                    maxDiscountAmount = v.MaxDiscountAmount,
                    endDate = v.EndDate.ToString("dd/MM/yyyy"),
                    isEligible = isEligible,
                    isUsedByUser = isUsedByUser,
                    isExhausted = isExhausted,
                    calculatedDiscount = calculatedDiscount,
                    reason = !isEligible ? (isUsedByUser ? "Đã dùng hết lượt" : (isExhausted ? "Hết lượt dùng" : $"Chưa đủ {v.MinOrderValue:N0}đ")) : ""
                };
            }).ToList();

            return Json(result);
        }
    }
}