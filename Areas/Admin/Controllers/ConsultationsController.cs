using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeautyShop.Data;
using BeautyShop.Models;
using BeautyShop.ViewModels;

namespace BeautyShop.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class ConsultationsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ConsultationsController(ApplicationDbContext context)
        {
            _context = context;
        }

        private bool IsStaff => HttpContext.Session.GetString("Role") == "Admin" || HttpContext.Session.GetString("Role") == "Employee";
        private bool IsAdmin => HttpContext.Session.GetString("Role") == "Admin";
        private int? CurrentUserId => HttpContext.Session.GetInt32("UserId");

        // ===== 1. DANH SÁCH TẤT CẢ LỊCH TƯ VẤN (ADMIN & STAFF) =====
        [HttpGet]
        public async Task<IActionResult> Index(string? keyword, string? status, string? consultationType, DateTime? date)
        {
            if (!IsStaff) return RedirectToAction("Login", "Account", new { area = "" });

            var query = _context.Consultations
                .Include(c => c.User)
                .Include(c => c.Employee)
                .Include(c => c.Review)
                .Include(c => c.ConsultationProducts!)
                    .ThenInclude(cp => cp.Product)
                .AsQueryable();

            // Tìm kiếm theo từ khóa: tên khách, SĐT, email, tên nhân viên, mã lịch
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = keyword.Trim();
                query = query.Where(c =>
                    (c.User != null && (c.User.FullName.Contains(kw) || c.User.Email.Contains(kw) || (c.User.PhoneNumber != null && c.User.PhoneNumber.Contains(kw)))) ||
                    (c.Employee != null && c.Employee.FullName.Contains(kw)) ||
                    c.Id.ToString() == kw ||
                    c.ConsultationType.Contains(kw));
            }

            // Lọc theo trạng thái
            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(c => c.Status == status);
            }

            // Lọc theo loại tư vấn
            if (!string.IsNullOrEmpty(consultationType))
            {
                query = query.Where(c => c.ConsultationType == consultationType);
            }

            // Lọc theo ngày
            if (date.HasValue)
            {
                query = query.Where(c => c.AppointmentDate.Date == date.Value.Date);
            }

            var list = query
    .AsEnumerable() // <--- Chèn thêm dòng này để xử lý sắp xếp TimeSpan trên RAM
    .OrderByDescending(c => c.AppointmentDate)
    .ThenByDescending(c => c.StartTime)
    .ToList(); // Đổi thành .ToList()

            // Danh sách nhân viên để phân công (chỉ User có Role == "Employee" và IsActive)
            ViewBag.Employees = await _context.Users
                .Include(u => u.Role)
                .Where(u => u.Role != null && u.Role.Name == "Employee" && u.IsActive)
                .OrderBy(u => u.FullName)
                .ToListAsync();

            // Thống kê nhanh
            ViewBag.TotalCount = await _context.Consultations.CountAsync();
            ViewBag.PendingCount = await _context.Consultations.CountAsync(c => c.Status == "Chờ xác nhận");
            ViewBag.AssignedCount = await _context.Consultations.CountAsync(c => c.Status == "Đã phân công");
            ViewBag.InProgressCount = await _context.Consultations.CountAsync(c => c.Status == "Đang tư vấn");
            ViewBag.CompletedCount = await _context.Consultations.CountAsync(c => c.Status == "Đã hoàn thành");
            ViewBag.CancelledCount = await _context.Consultations.CountAsync(c => c.Status == "Đã hủy");

            ViewBag.Keyword = keyword;
            ViewBag.CurrentStatus = status;
            ViewBag.CurrentType = consultationType;
            ViewBag.CurrentDate = date?.ToString("yyyy-MM-dd");
            ViewBag.IsAdmin = IsAdmin;

            return View(list);
        }

        // ===== 2. LỊCH TƯ VẤN CỦA TÔI (DÀNH CHO NHÂN VIÊN) =====
        [HttpGet]
        public async Task<IActionResult> MyAssigned(string? keyword, string? status, DateTime? date)
        {
            if (!IsStaff) return RedirectToAction("Login", "Account", new { area = "" });

            var query = _context.Consultations
                .Include(c => c.User)
                .Include(c => c.Employee)
                .Include(c => c.Review)
                .Include(c => c.ConsultationProducts!)
                    .ThenInclude(cp => cp.Product)
                .Where(c => c.EmployeeId == (CurrentUserId ?? 0))
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = keyword.Trim();
                query = query.Where(c =>
                    (c.User != null && (c.User.FullName.Contains(kw) || c.User.Email.Contains(kw) || (c.User.PhoneNumber != null && c.User.PhoneNumber.Contains(kw)))) ||
                    c.ConsultationType.Contains(kw));
            }

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(c => c.Status == status);
            }

            if (date.HasValue)
            {
                query = query.Where(c => c.AppointmentDate.Date == date.Value.Date);
            }

            var list = query
                .AsEnumerable()
                .OrderByDescending(c => c.AppointmentDate)
                .ThenByDescending(c => c.StartTime)
                .ToList();

            ViewBag.Keyword = keyword;
            ViewBag.CurrentStatus = status;
            ViewBag.CurrentDate = date?.ToString("yyyy-MM-dd");

            return View(list);
        }

        // ===== 3. CHI TIẾT LỊCH TƯ VẤN =====
        [HttpGet]
        public async Task<IActionResult> Detail(int id)
        {
            if (!IsStaff) return RedirectToAction("Login", "Account", new { area = "" });

            var consultation = await _context.Consultations
                .Include(c => c.User)
                .Include(c => c.Employee)
                .Include(c => c.Review)
                .Include(c => c.ConsultationProducts!)
                    .ThenInclude(cp => cp.Product)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (consultation == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy lịch tư vấn.";
                return RedirectToAction(nameof(Index));
            }

            // Nếu là Employee, chỉ xem được lịch của mình
            if (!IsAdmin && consultation.EmployeeId != (CurrentUserId ?? 0))
            {
                TempData["ErrorMessage"] = "Bạn chỉ có thể xem lịch tư vấn được phân công cho chính bạn.";
                return RedirectToAction(nameof(MyAssigned));
            }

            // Danh sách nhân viên để Admin phân công
            ViewBag.Employees = await _context.Users
                .Include(u => u.Role)
                .Where(u => u.Role != null && u.Role.Name == "Employee" && u.IsActive)
                .OrderBy(u => u.FullName)
                .ToListAsync();

            ViewBag.IsAdmin = IsAdmin;
            return View(consultation);
        }

        // ===== 4. PHÂN CÔNG NHÂN VIÊN (CHỈ ADMIN) =====
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignEmployee(int consultationId, int employeeId)
        {
            if (!IsAdmin)
            {
                TempData["ErrorMessage"] = "Chỉ quản trị viên mới có quyền phân công nhân viên.";
                return RedirectToAction(nameof(Index));
            }

            var consultation = await _context.Consultations.FindAsync(consultationId);
            if (consultation == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy lịch tư vấn.";
                return RedirectToAction(nameof(Index));
            }

            // 1. Kiểm tra nhân viên có tồn tại và có Role == "Employee" không
            var employee = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == employeeId && u.IsActive);

            if (employee == null || employee.Role?.Name != "Employee")
            {
                TempData["ErrorMessage"] = "Người dùng được chọn không phải là nhân viên tư vấn hoặc tài khoản đã bị khóa.";
                return RedirectToAction(nameof(Detail), new { id = consultationId });
            }

            // 2. Kiểm tra trùng lịch: nhân viên này đã có lịch hẹn khác vào cùng khung giờ hay không
            bool isOverlapping = await _context.Consultations.AnyAsync(c =>
                c.Id != consultationId &&
                c.EmployeeId == employeeId &&
                c.Status != "Đã hủy" &&
                c.AppointmentDate.Date == consultation.AppointmentDate.Date &&
                c.StartTime < consultation.EndTime &&
                c.EndTime > consultation.StartTime);

            if (isOverlapping)
            {
                TempData["ErrorMessage"] = $"Nhân viên {employee.FullName} đã có lịch hẹn khác trùng vào khung giờ ({consultation.StartTime:hh\\:mm} - {consultation.EndTime:hh\\:mm}) ngày {consultation.AppointmentDate:dd/MM/yyyy}. Vui lòng chọn nhân viên khác hoặc đổi lịch.";
                return RedirectToAction(nameof(Detail), new { id = consultationId });
            }

            // 3. Phân công và cập nhật trạng thái
            consultation.EmployeeId = employeeId;
            consultation.Status = "Đã phân công";
            consultation.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã phân công nhân viên \"{employee.FullName}\" phụ trách lịch tư vấn thành công!";
            return RedirectToAction(nameof(Detail), new { id = consultationId });
        }

        // ===== 5. CẬP NHẬT TRẠNG THÁI (ADMIN & EMPLOYEE) =====
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int consultationId, string newStatus)
        {
            if (!IsStaff) return RedirectToAction("Login", "Account", new { area = "" });

            var consultation = await _context.Consultations.FindAsync(consultationId);
            if (consultation == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy lịch tư vấn.";
                return RedirectToAction(nameof(Index));
            }

            // Employee chỉ cập nhật được lịch của mình
            if (!IsAdmin && consultation.EmployeeId != (CurrentUserId ?? 0))
            {
                TempData["ErrorMessage"] = "Bạn không có quyền thay đổi trạng thái lịch tư vấn này.";
                return RedirectToAction(nameof(MyAssigned));
            }

            var validStatuses = new[] { "Chờ xác nhận", "Đã xác nhận", "Đã phân công", "Đang tư vấn", "Đã hoàn thành", "Đã hủy" };
            if (!validStatuses.Contains(newStatus))
            {
                TempData["ErrorMessage"] = "Trạng thái không hợp lệ.";
                return RedirectToAction(nameof(Detail), new { id = consultationId });
            }

            consultation.Status = newStatus;
            consultation.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã cập nhật trạng thái thành \"{newStatus}\".";
            return RedirectToAction(nameof(Detail), new { id = consultationId });
        }

        // ===== 6. CẬP NHẬT KẾT QUẢ TƯ VẤN & SẢN PHẨM ĐỀ XUẤT (GET) =====
        [HttpGet]
        public async Task<IActionResult> UpdateResult(int id)
        {
            if (!IsStaff) return RedirectToAction("Login", "Account", new { area = "" });

            var consultation = await _context.Consultations
                .Include(c => c.User)
                .Include(c => c.Employee)
                .Include(c => c.ConsultationProducts!)
                    .ThenInclude(cp => cp.Product)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (consultation == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy lịch tư vấn.";
                return RedirectToAction(nameof(Index));
            }

            if (!IsAdmin && consultation.EmployeeId != (CurrentUserId ?? 0))
            {
                TempData["ErrorMessage"] = "Bạn chỉ có thể cập nhật kết quả lịch tư vấn được phân công cho bạn.";
                return RedirectToAction(nameof(MyAssigned));
            }

            var model = new ConsultationResultViewModel
            {
                ConsultationId = consultation.Id,
                Status = consultation.Status == "Chờ xác nhận" || consultation.Status == "Đã phân công" ? "Đang tư vấn" : consultation.Status,
                EmployeeNote = consultation.EmployeeNote,
                ConsultationResult = consultation.ConsultationResult ?? string.Empty,
                SelectedProductIds = consultation.ConsultationProducts?.Select(cp => cp.ProductId).ToList() ?? new List<int>(),
                ProductNotes = consultation.ConsultationProducts?.ToDictionary(cp => cp.ProductId, cp => cp.Note ?? "") ?? new Dictionary<int, string>()
            };

            // Danh sách toàn bộ sản phẩm đang hoạt động để nhân viên đề xuất
            ViewBag.AvailableProducts = await _context.Products
                .Include(p => p.Brand)
                .Include(p => p.Category)
                .Include(p => p.ProductVariants)
                .Where(p => p.IsActive)
                .OrderBy(p => p.Name)
                .ToListAsync();

            ViewBag.Consultation = consultation;
            return View(model);
        }

        // ===== 6. CẬP NHẬT KẾT QUẢ TƯ VẤN & SẢN PHẨM ĐỀ XUẤT (POST) =====
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateResult(ConsultationResultViewModel model)
        {
            if (!IsStaff) return RedirectToAction("Login", "Account", new { area = "" });

            var consultation = await _context.Consultations
                .Include(c => c.ConsultationProducts)
                .FirstOrDefaultAsync(c => c.Id == model.ConsultationId);

            if (consultation == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy lịch tư vấn.";
                return RedirectToAction(nameof(Index));
            }

            if (!IsAdmin && consultation.EmployeeId != (CurrentUserId ?? 0))
            {
                TempData["ErrorMessage"] = "Bạn không có quyền cập nhật lịch tư vấn này.";
                return RedirectToAction(nameof(MyAssigned));
            }

            if (string.IsNullOrWhiteSpace(model.ConsultationResult))
            {
                ModelState.AddModelError("ConsultationResult", "Vui lòng nhập kết quả và nội dung tư vấn.");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.AvailableProducts = await _context.Products
                    .Include(p => p.Brand)
                    .Include(p => p.Category)
                    .Include(p => p.ProductVariants)
                    .Where(p => p.IsActive)
                    .OrderBy(p => p.Name)
                    .ToListAsync();
                ViewBag.Consultation = await _context.Consultations
                    .Include(c => c.User)
                    .Include(c => c.Employee)
                    .FirstOrDefaultAsync(c => c.Id == model.ConsultationId);
                return View(model);
            }

            // 1. Cập nhật thông tin chính
            consultation.Status = model.Status;
            consultation.EmployeeNote = model.EmployeeNote;
            consultation.ConsultationResult = model.ConsultationResult;
            consultation.UpdatedAt = DateTime.Now;

            // 2. Cập nhật sản phẩm đề xuất
            if (consultation.ConsultationProducts != null && consultation.ConsultationProducts.Any())
            {
                _context.ConsultationProducts.RemoveRange(consultation.ConsultationProducts);
            }

            if (model.SelectedProductIds != null && model.SelectedProductIds.Any())
            {
                foreach (var prodId in model.SelectedProductIds.Distinct())
                {
                    string? note = null;
                    if (model.ProductNotes != null && model.ProductNotes.TryGetValue(prodId, out var pNote))
                    {
                        note = pNote;
                    }

                    _context.ConsultationProducts!.Add(new ConsultationProduct
                    {
                        ConsultationId = consultation.Id,
                        ProductId = prodId,
                        Note = note
                    });
                }
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Cập nhật kết quả tư vấn và sản phẩm đề xuất thành công!";
            return RedirectToAction(nameof(Detail), new { id = consultation.Id });
        }
    }
}
