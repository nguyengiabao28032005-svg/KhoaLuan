using BeautyShop.Data;
using BeautyShop.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BeautyShop.Controllers
{
    public class VoucherController : Controller
    {
        private readonly ApplicationDbContext _context;

        public VoucherController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Voucher hoặc /Voucher/Index
        // Trang "Kho Voucher / Ưu Đãi" của Shop
        public async Task<IActionResult> Index(string? filter)
        {
            var now = DateTime.Now;
            var query = _context.Vouchers
                .Where(v => v.IsActive && now >= v.StartDate && now <= v.EndDate && (v.TotalUsageLimit == null || v.UsedCount < v.TotalUsageLimit))
                .AsQueryable();

            if (!string.IsNullOrEmpty(filter))
            {
                switch (filter.ToLower())
                {
                    case "percent":
                        query = query.Where(v => v.DiscountType == "Percent");
                        break;
                    case "fixed":
                        query = query.Where(v => v.DiscountType == "Fixed");
                        break;
                    case "min300":
                        query = query.Where(v => v.MinOrderValue <= 300000);
                        break;
                    case "min500":
                        query = query.Where(v => v.MinOrderValue >= 500000);
                        break;
                }
            }

            var rawVouchers = await query.OrderByDescending(v => v.Id).ToListAsync();
            var vouchers = rawVouchers
                .OrderBy(v => v.MinOrderValue)
                .ThenByDescending(v => v.DiscountValue)
                .ToList();

            // Lấy danh sách ID các voucher mà user hiện tại đã lưu trong ví (nếu đã đăng nhập)
            var userId = HttpContext.Session.GetInt32("UserId");
            var savedVoucherIds = new List<int>();
            if (userId.HasValue)
            {
                savedVoucherIds = await _context.UserVouchers
                    .Where(uv => uv.UserId == userId.Value && !uv.IsUsed)
                    .Select(uv => uv.VoucherId)
                    .ToListAsync();
            }

            ViewBag.SavedVoucherIds = savedVoucherIds;
            ViewBag.CurrentFilter = filter ?? "all";
            ViewBag.IsLoggedIn = userId.HasValue;

            return View(vouchers);
        }

        // POST: /Voucher/SaveVoucher
        // Khách hàng bấm [Lưu mã]
        [HttpPost]
        public async Task<IActionResult> SaveVoucher(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue)
            {
                return Json(new { success = false, requireLogin = true, message = "Vui lòng đăng nhập để lưu mã giảm giá vào tài khoản của bạn." });
            }

            var voucher = await _context.Vouchers.FindAsync(id);
            if (voucher == null || !voucher.IsActive)
            {
                return Json(new { success = false, message = "Mã giảm giá không tồn tại hoặc đã ngừng hoạt động." });
            }

            var now = DateTime.Now;
            if (now < voucher.StartDate || now > voucher.EndDate)
            {
                return Json(new { success = false, message = "Mã giảm giá đã hết hạn sử dụng." });
            }

            if (voucher.TotalUsageLimit.HasValue && voucher.UsedCount >= voucher.TotalUsageLimit.Value)
            {
                return Json(new { success = false, message = "Mã giảm giá đã hết lượt phát hành." });
            }

            // Kiểm tra xem khách đã lưu mã này chưa
            var existing = await _context.UserVouchers
                .FirstOrDefaultAsync(uv => uv.UserId == userId.Value && uv.VoucherId == id);

            if (existing != null)
            {
                if (existing.IsUsed)
                {
                    return Json(new { success = false, message = "Bạn đã sử dụng mã này trong một đơn hàng trước đó." });
                }
                return Json(new { success = true, alreadySaved = true, message = "Mã này đã có sẵn trong ví voucher của bạn!" });
            }

            var userVoucher = new UserVoucher
            {
                UserId = userId.Value,
                VoucherId = id,
                IsUsed = false,
                CreatedAt = DateTime.Now
            };

            _context.UserVouchers.Add(userVoucher);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = $"Đã lưu mã '{voucher.Code}' vào ví voucher của bạn thành công!" });
        }

        // GET: /Voucher/MyVouchers
        // Trang "Tài khoản → Voucher của tôi"
        public async Task<IActionResult> MyVouchers()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue)
            {
                return RedirectToAction("Login", "Account", new { returnUrl = "/Voucher/MyVouchers" });
            }

            var myVouchers = await _context.UserVouchers
                .Include(uv => uv.Voucher)
                .Where(uv => uv.UserId == userId.Value)
                .OrderByDescending(uv => uv.Id)
                .ToListAsync();

            return View(myVouchers);
        }

        // GET: /Voucher/GetAvailableVouchers
        // API cho Giỏ hàng và Thanh toán hiển thị danh sách voucher có thể áp dụng
        [HttpGet]
        public async Task<IActionResult> GetAvailableVouchers()
        {
            var now = DateTime.Now;
            var vouchers = await _context.Vouchers
                .Where(v => v.IsActive && now >= v.StartDate && now <= v.EndDate && (v.TotalUsageLimit == null || v.UsedCount < v.TotalUsageLimit))
                .Select(v => new
                {
                    v.Id,
                    v.Code,
                    v.Name,
                    v.DiscountType,
                    v.DiscountValue,
                    v.MinOrderValue,
                    v.MaxDiscountAmount,
                    EndDate = v.EndDate.ToString("dd/MM/yyyy")
                })
                .ToListAsync();

            return Json(new { success = true, data = vouchers });
        }
    }
}
