using BeautyShop.Data;
using BeautyShop.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BeautyShop.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class VouchersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public VouchersController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Admin/Vouchers
        public async Task<IActionResult> Index(string? keyword, string? status)
        {
            var query = _context.Vouchers
                .Include(v => v.UserVouchers)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                string kw = keyword.Trim().ToLower();
                query = query.Where(v => v.Code.ToLower().Contains(kw) || v.Name.ToLower().Contains(kw));
            }

            var now = DateTime.Now;
            if (!string.IsNullOrEmpty(status))
            {
                switch (status.ToLower())
                {
                    case "active":
                        query = query.Where(v => v.IsActive && now >= v.StartDate && now <= v.EndDate && (v.TotalUsageLimit == null || v.UsedCount < v.TotalUsageLimit));
                        break;
                    case "upcoming":
                        query = query.Where(v => v.IsActive && now < v.StartDate);
                        break;
                    case "expired":
                        query = query.Where(v => now > v.EndDate);
                        break;
                    case "depleted":
                        query = query.Where(v => v.TotalUsageLimit != null && v.UsedCount >= v.TotalUsageLimit);
                        break;
                    case "inactive":
                        query = query.Where(v => !v.IsActive);
                        break;
                }
            }

            var vouchers = await query.OrderByDescending(x => x.Id).ToListAsync();

            ViewBag.Keyword = keyword;
            ViewBag.Status = status;

            // Stats summary for top KPI cards
            var allVouchers = await _context.Vouchers.ToListAsync();
            ViewBag.TotalVouchers = allVouchers.Count;
            ViewBag.ActiveVouchers = allVouchers.Count(v => v.IsActive && now >= v.StartDate && now <= v.EndDate && (v.TotalUsageLimit == null || v.UsedCount < v.TotalUsageLimit));
            ViewBag.TotalUsedCount = allVouchers.Sum(v => v.UsedCount);
            ViewBag.DepletedOrExpired = allVouchers.Count(v => now > v.EndDate || (v.TotalUsageLimit != null && v.UsedCount >= v.TotalUsageLimit));

            return View(vouchers);
        }

        // GET: Admin/Vouchers/Create
        public IActionResult Create()
        {
            var defaultVoucher = new Voucher
            {
                StartDate = DateTime.Now,
                EndDate = DateTime.Now.AddDays(30),
                DiscountType = "Percent",
                DiscountValue = 10,
                MinOrderValue = 300000,
                MaxDiscountAmount = 100000,
                TotalUsageLimit = 100,
                MaxUsesPerUser = 1,
                Scope = "All",
                IsActive = true
            };
            return View(defaultVoucher);
        }

        // POST: Admin/Vouchers/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Voucher model)
        {
            if (string.IsNullOrWhiteSpace(model.Code))
            {
                ModelState.AddModelError("Code", "Vui lòng nhập mã voucher.");
            }
            else
            {
                model.Code = model.Code.Trim().ToUpper();
                if (await _context.Vouchers.AnyAsync(v => v.Code == model.Code))
                {
                    ModelState.AddModelError("Code", $"Mã voucher '{model.Code}' đã tồn tại trong hệ thống. Vui lòng chọn mã khác.");
                }
            }

            if (string.IsNullOrWhiteSpace(model.Name))
            {
                model.Name = model.Code;
            }

            if (model.EndDate < model.StartDate)
            {
                ModelState.AddModelError("EndDate", "Ngày kết thúc phải lớn hơn hoặc bằng ngày bắt đầu.");
            }

            if (model.DiscountType == "Percent" && (model.DiscountValue <= 0 || model.DiscountValue > 100))
            {
                ModelState.AddModelError("DiscountValue", "Giảm theo phần trăm phải nằm trong khoảng từ 1% đến 100%.");
            }

            if (model.DiscountType == "Fixed" && model.DiscountValue <= 0)
            {
                ModelState.AddModelError("DiscountValue", "Số tiền giảm phải lớn hơn 0đ.");
            }

            if (!ModelState.IsValid)
                return View(model);

            _context.Vouchers.Add(model);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã tạo thành công voucher '{model.Code}'!";
            return RedirectToAction(nameof(Index));
        }

        // GET: Admin/Vouchers/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var voucher = await _context.Vouchers.FindAsync(id);
            if (voucher == null)
                return NotFound();

            return View(voucher);
        }

        // POST: Admin/Vouchers/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Voucher model)
        {
            if (string.IsNullOrWhiteSpace(model.Code))
            {
                ModelState.AddModelError("Code", "Vui lòng nhập mã voucher.");
            }
            else
            {
                model.Code = model.Code.Trim().ToUpper();
                if (await _context.Vouchers.AnyAsync(v => v.Code == model.Code && v.Id != model.Id))
                {
                    ModelState.AddModelError("Code", $"Mã voucher '{model.Code}' đã được sử dụng cho chương trình khác.");
                }
            }

            if (string.IsNullOrWhiteSpace(model.Name))
            {
                model.Name = model.Code;
            }

            if (model.EndDate < model.StartDate)
            {
                ModelState.AddModelError("EndDate", "Ngày kết thúc phải lớn hơn hoặc bằng ngày bắt đầu.");
            }

            if (model.DiscountType == "Percent" && (model.DiscountValue <= 0 || model.DiscountValue > 100))
            {
                ModelState.AddModelError("DiscountValue", "Giảm theo phần trăm phải nằm trong khoảng từ 1% đến 100%.");
            }

            if (model.DiscountType == "Fixed" && model.DiscountValue <= 0)
            {
                ModelState.AddModelError("DiscountValue", "Số tiền giảm phải lớn hơn 0đ.");
            }

            if (!ModelState.IsValid)
                return View(model);

            try
            {
                _context.Vouchers.Update(model);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Đã cập nhật thành công voucher '{model.Code}'!";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.Vouchers.AnyAsync(e => e.Id == model.Id))
                    return NotFound();
                else
                    throw;
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Admin/Vouchers/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var voucher = await _context.Vouchers
                .Include(v => v.UserVouchers!)
                    .ThenInclude(uv => uv.User)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (voucher == null)
                return NotFound();

            return View(voucher);
        }

        // POST/GET: Admin/Vouchers/ToggleStatus/5
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var voucher = await _context.Vouchers.FindAsync(id);
            if (voucher == null)
                return RedirectToAction(nameof(Index));

            voucher.IsActive = !voucher.IsActive;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = voucher.IsActive
                ? $"Đã kích hoạt lại mã '{voucher.Code}'."
                : $"Đã tắt mã '{voucher.Code}'.";

            return RedirectToAction(nameof(Index));
        }

        // POST: Admin/Vouchers/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var voucher = await _context.Vouchers
                .Include(v => v.UserVouchers)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (voucher != null)
            {
                if (voucher.UserVouchers != null && voucher.UserVouchers.Any())
                {
                    _context.UserVouchers.RemoveRange(voucher.UserVouchers);
                }

                _context.Vouchers.Remove(voucher);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Đã xóa voucher '{voucher.Code}'.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}