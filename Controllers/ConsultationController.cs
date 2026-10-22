using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeautyShop.Data;
using BeautyShop.Models;
using BeautyShop.ViewModels;

namespace BeautyShop.Controllers
{
    public class ConsultationController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ConsultationController(ApplicationDbContext context)
        {
            _context = context;
        }

        private int? CurrentUserId => HttpContext.Session.GetInt32("UserId");

        // ===== 1. ĐẶT LỊCH TƯ VẤN (GET) =====
        [HttpGet]
        public IActionResult Book()
        {
            if (CurrentUserId == null)
            {
                TempData["ErrorMessage"] = "Vui lòng đăng nhập để đặt lịch tư vấn.";
                return RedirectToAction("Login", "Account");
            }

            var model = new BookConsultationViewModel
            {
                AppointmentDate = DateTime.Today.AddDays(1),
                StartTime = "09:00",
                EndTime = "09:45",
                ConsultationType = "Tư vấn chăm sóc da"
            };

            return View(model);
        }

        // ===== 1. ĐẶT LỊCH TƯ VẤN (POST) =====
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Book(BookConsultationViewModel model)
        {
            if (CurrentUserId == null)
            {
                TempData["ErrorMessage"] = "Vui lòng đăng nhập để đặt lịch tư vấn.";
                return RedirectToAction("Login", "Account");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // 1. Kiểm tra ngày không được ở quá khứ
            if (model.AppointmentDate.Date < DateTime.Today)
            {
                ModelState.AddModelError("AppointmentDate", "Ngày hẹn không được ở trong quá khứ.");
                return View(model);
            }

            // 2. Chuyển đổi giờ bắt đầu và giờ kết thúc
            if (!TimeSpan.TryParse(model.StartTime, out TimeSpan startTime))
            {
                ModelState.AddModelError("StartTime", "Giờ bắt đầu không đúng định dạng (HH:mm).");
                return View(model);
            }

            if (!TimeSpan.TryParse(model.EndTime, out TimeSpan endTime))
            {
                ModelState.AddModelError("EndTime", "Giờ kết thúc không đúng định dạng (HH:mm).");
                return View(model);
            }

            // 3. Giờ kết thúc phải sau giờ bắt đầu
            if (endTime <= startTime)
            {
                ModelState.AddModelError("EndTime", "Giờ kết thúc phải sau giờ bắt đầu.");
                return View(model);
            }

            // 4. Nếu chọn ngày hôm nay thì giờ bắt đầu không được trong quá khứ
            if (model.AppointmentDate.Date == DateTime.Today && startTime <= DateTime.Now.TimeOfDay)
            {
                ModelState.AddModelError("StartTime", "Không thể chọn khung giờ đã qua trong ngày hôm nay.");
                return View(model);
            }

            // 5. Kiểm tra lịch trùng của chính khách hàng này
            bool hasUserOverlap = _context.Consultations
                .AsEnumerable() // <--- Thêm dòng này để xử lý so sánh ngày giờ trên bộ nhớ RAM
                .Any(c => c.UserId == CurrentUserId.Value &&
                          c.Status != "Đã hủy" &&
                          c.AppointmentDate.Date == model.AppointmentDate.Date &&
                          c.StartTime < endTime && c.EndTime > startTime);

            if (hasUserOverlap)
            {
                ModelState.AddModelError("", "Bạn đã có một lịch hẹn khác bị trùng vào khung giờ này. Vui lòng chọn ngày hoặc giờ khác.");
                return View(model);
            }

            // 6. Tạo bản ghi mới
            var consultation = new Consultation
            {
                UserId = CurrentUserId.Value,
                ConsultationType = model.ConsultationType,
                AppointmentDate = model.AppointmentDate.Date,
                StartTime = startTime,
                EndTime = endTime,
                Status = "Chờ xác nhận",
                CustomerNote = model.CustomerNote,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            _context.Consultations.Add(consultation);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đặt lịch tư vấn thành công! Đội ngũ chuyên viên BeautyShop sẽ sớm xác nhận lịch hẹn của bạn.";
            return RedirectToAction(nameof(MyConsultations));
        }

        // ===== 2. QUẢN LÝ LỊCH TƯ VẤN CỦA TÔI =====
        [HttpGet]
        public async Task<IActionResult> MyConsultations(string? status)
        {
            if (CurrentUserId == null)
            {
                TempData["ErrorMessage"] = "Vui lòng đăng nhập để xem lịch tư vấn.";
                return RedirectToAction("Login", "Account");
            }

            var query = _context.Consultations
                .Include(c => c.Employee)
                .Include(c => c.Review)
                .Include(c => c.ConsultationProducts!)
                    .ThenInclude(cp => cp.Product)
                .Where(c => c.UserId == CurrentUserId.Value)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(c => c.Status == status);
            }

            var list = query
                 .AsEnumerable() // <--- Thêm dòng này để C# xử lý phần sắp xếp ngày giờ
                 .OrderByDescending(c => c.AppointmentDate)
                 .ThenByDescending(c => c.StartTime)
                 .ToList(); // Đổi thành .ToList() vì đã ở dạng IEnumerable

            ViewBag.CurrentStatus = status;
            return View(list);
        }

        // ===== 3. CHI TIẾT LỊCH TƯ VẤN =====
        [HttpGet]
        public async Task<IActionResult> Detail(int id)
        {
            if (CurrentUserId == null)
            {
                TempData["ErrorMessage"] = "Vui lòng đăng nhập để xem chi tiết.";
                return RedirectToAction("Login", "Account");
            }

            var consultation = await _context.Consultations
                .Include(c => c.User)
                .Include(c => c.Employee)
                .Include(c => c.Review)
                .Include(c => c.ConsultationProducts!)
                    .ThenInclude(cp => cp.Product)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (consultation == null || consultation.UserId != CurrentUserId.Value)
            {
                TempData["ErrorMessage"] = "Không tìm thấy lịch tư vấn hoặc bạn không có quyền truy cập.";
                return RedirectToAction(nameof(MyConsultations));
            }

            return View(consultation);
        }

        // ===== 4. HỦY LỊCH TƯ VẤN =====
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id, string? reason)
        {
            if (CurrentUserId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var consultation = await _context.Consultations.FirstOrDefaultAsync(c => c.Id == id);

            if (consultation == null || consultation.UserId != CurrentUserId.Value)
            {
                TempData["ErrorMessage"] = "Không tìm thấy lịch hẹn hoặc bạn không có quyền thao tác.";
                return RedirectToAction(nameof(MyConsultations));
            }

            // Chỉ cho phép hủy khi đang ở các trạng thái chưa diễn ra
            var cancellableStatuses = new[] { "Chờ xác nhận", "Đã xác nhận", "Đã phân công" };
            if (!cancellableStatuses.Contains(consultation.Status))
            {
                TempData["ErrorMessage"] = $"Không thể hủy lịch hẹn đang ở trạng thái \"{consultation.Status}\".";
                return RedirectToAction(nameof(Detail), new { id });
            }

            consultation.Status = "Đã hủy";
            consultation.UpdatedAt = DateTime.Now;
            if (!string.IsNullOrWhiteSpace(reason))
            {
                consultation.CustomerNote = string.IsNullOrEmpty(consultation.CustomerNote)
                    ? $"[Lý do hủy]: {reason}"
                    : $"{consultation.CustomerNote}\n[Lý do hủy]: {reason}";
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã hủy lịch hẹn tư vấn thành công.";
            return RedirectToAction(nameof(MyConsultations));
        }

        // ===== 5. ĐÁNH GIÁ BUỔI TƯ VẤN =====
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Review(ConsultationReviewViewModel model)
        {
            if (CurrentUserId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var consultation = await _context.Consultations
                .Include(c => c.Review)
                .FirstOrDefaultAsync(c => c.Id == model.ConsultationId);

            if (consultation == null || consultation.UserId != CurrentUserId.Value)
            {
                TempData["ErrorMessage"] = "Không tìm thấy lịch tư vấn hoặc bạn không có quyền đánh giá.";
                return RedirectToAction(nameof(MyConsultations));
            }

            // Kiểm tra trạng thái: chỉ cho phép đánh giá khi "Đã hoàn thành"
            if (consultation.Status != "Đã hoàn thành")
            {
                TempData["ErrorMessage"] = "Chỉ có thể đánh giá sau khi buổi tư vấn đã hoàn thành.";
                return RedirectToAction(nameof(Detail), new { id = model.ConsultationId });
            }

            // Kiểm tra đã đánh giá trước đó chưa
            if (consultation.Review != null || await _context.ConsultationReviews.AnyAsync(r => r.ConsultationId == model.ConsultationId))
            {
                TempData["ErrorMessage"] = "Bạn đã gửi đánh giá cho buổi tư vấn này rồi, không thể đánh giá lại.";
                return RedirectToAction(nameof(Detail), new { id = model.ConsultationId });
            }

            if (model.Rating < 1 || model.Rating > 5)
            {
                TempData["ErrorMessage"] = "Số sao đánh giá phải từ 1 đến 5.";
                return RedirectToAction(nameof(Detail), new { id = model.ConsultationId });
            }

            var review = new ConsultationReview
            {
                ConsultationId = model.ConsultationId,
                UserId = CurrentUserId.Value,
                Rating = model.Rating,
                Comment = model.Comment,
                CreatedAt = DateTime.Now
            };

            _context.ConsultationReviews.Add(review);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Cảm ơn bạn đã gửi đánh giá cho buổi tư vấn!";
            return RedirectToAction(nameof(Detail), new { id = model.ConsultationId });
        }
    }
}
