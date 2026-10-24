using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BeautyShop.Data;
using BeautyShop.Models;
using BeautyShop.Services;
using BeautyShop.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BeautyShop.Areas.Admin.Controllers
{
    [Area("Admin")]
    [RequirePermission(AppPermissions.UserManage)]
    public class EmployeesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentUserScope _userScope;

        public EmployeesController(ApplicationDbContext context, ICurrentUserScope userScope)
        {
            _context = context;
            _userScope = userScope;
        }

        private async Task LoadFormLookupsAsync()
        {
            ViewBag.Roles = await _context.Roles
                .Where(r => r.Name != AppRoles.Customer && r.Name != AppRoles.Employee)
                .OrderBy(r => r.Id)
                .ToListAsync();
            ViewBag.Stores = await _context.Stores
                .Where(s => s.IsActive)
                .OrderBy(s => s.Id)
                .ToListAsync();
        }

        // ===== DANH SÁCH NHÂN VIÊN & QUẢN TRỊ VIÊN =====
        public async Task<IActionResult> Index(string keyword, string? roleFilter)
        {
            var query = _context.Users
                .Include(x => x.Role)
                .Include(x => x.WarehouseScopes)
                    .ThenInclude(ws => ws.Store)
                .Where(x => x.Role != null && x.Role.Name != AppRoles.Customer)
                .AsQueryable();

            if (!string.IsNullOrEmpty(keyword))
            {
                query = query.Where(x =>
                    x.FullName.Contains(keyword) ||
                    x.Email.Contains(keyword) ||
                    (x.PhoneNumber != null && x.PhoneNumber.Contains(keyword)));
            }

            if (!string.IsNullOrEmpty(roleFilter))
            {
                query = query.Where(x => x.Role != null && x.Role.Name == roleFilter);
            }

            ViewBag.Keyword = keyword;
            ViewBag.RoleFilter = roleFilter;
            await LoadFormLookupsAsync();
            return View(await query.OrderBy(x => x.RoleId).ThenByDescending(x => x.CreatedAt).ToListAsync());
        }

        // ===== TẠO NHÂN VIÊN MỚI =====
        public async Task<IActionResult> Create()
        {
            await LoadFormLookupsAsync();
            return View(new AdminUserViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> Create(AdminUserViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Password))
            {
                ModelState.AddModelError("Password", "Vui lòng nhập mật khẩu cho tài khoản mới.");
            }

            if (!ModelState.IsValid)
            {
                await LoadFormLookupsAsync();
                return View(model);
            }

            if (await _context.Users.AnyAsync(u => u.Email == model.Email))
            {
                ModelState.AddModelError("Email", "Email này đã được sử dụng.");
                await LoadFormLookupsAsync();
                return View(model);
            }

            var targetRole = await _context.Roles.FindAsync(model.RoleId);
            if (targetRole == null)
            {
                ModelState.AddModelError("RoleId", "Vai trò không hợp lệ.");
                await LoadFormLookupsAsync();
                return View(model);
            }

            var user = new User
            {
                FullName = model.FullName.Trim(),
                Email = model.Email.Trim(),
                PhoneNumber = model.PhoneNumber?.Trim(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password),
                RoleId = model.RoleId,
                Position = model.Position ?? AppRoles.GetDisplayName(targetRole.Name),
                IsActive = true,
                SecurityStamp = Guid.NewGuid().ToString("N"),
                CreatedAt = DateTime.Now
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            int? adminId = await _userScope.GetUserIdAsync();

            // Gán phạm vi kho (UserWarehouseScope) nếu không phải Admin (Admin bỏ qua bộ lọc, không lưu 6 dòng)
            if (targetRole.Name != AppRoles.Admin && model.SelectedStoreIds != null && model.SelectedStoreIds.Any())
            {
                foreach (var sId in model.SelectedStoreIds.Distinct())
                {
                    _context.UserWarehouseScopes.Add(new UserWarehouseScope
                    {
                        UserId = user.Id,
                        StoreId = sId,
                        AssignedByUserId = adminId,
                        AssignedAt = DateTime.Now
                    });
                }
            }

            // Ghi nhật ký AuditLog
            _context.AuditLogs.Add(new AuditLog
            {
                PerformedByUserId = adminId,
                Action = "CreateUser",
                TargetUserId = user.Id,
                OldValue = null,
                NewValue = $"Role={targetRole.Name}, Stores=[{string.Join(",", model.SelectedStoreIds ?? new List<int>())}]",
                Note = $"Tạo nhân viên mới '{user.FullName}' ({user.Email}) với vai trò {targetRole.Name}",
                CreatedAt = DateTime.Now
            });

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã tạo tài khoản nhân viên \"{user.FullName}\" ({AppRoles.GetDisplayName(targetRole.Name)}) thành công!";
            return RedirectToAction(nameof(Index));
        }

        // ===== SỬA THÔNG TIN NHÂN VIÊN, VAI TRÒ & PHẠM VI KHO =====
        public async Task<IActionResult> Edit(int id)
        {
            var user = await _context.Users
                .Include(u => u.Role)
                .Include(u => u.WarehouseScopes)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null || user.Role?.Name == AppRoles.Customer)
                return RedirectToAction(nameof(Index));

            var vm = new AdminUserViewModel
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                RoleId = user.RoleId,
                Position = user.Position,
                IsActive = user.IsActive,
                SelectedStoreIds = user.WarehouseScopes.Select(ws => ws.StoreId).ToList()
            };

            await LoadFormLookupsAsync();
            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(AdminUserViewModel model)
        {
            var user = await _context.Users
                .Include(u => u.Role)
                .Include(u => u.WarehouseScopes)
                .FirstOrDefaultAsync(u => u.Id == model.Id);

            if (user == null) return NotFound();

            if (await _context.Users.AnyAsync(u => u.Email == model.Email && u.Id != model.Id))
            {
                ModelState.AddModelError("Email", "Email này đã được sử dụng bởi tài khoản khác.");
                await LoadFormLookupsAsync();
                return View(model);
            }

            var newRole = await _context.Roles.FindAsync(model.RoleId);
            if (newRole == null)
            {
                ModelState.AddModelError("RoleId", "Vai trò được chọn không tồn tại.");
                await LoadFormLookupsAsync();
                return View(model);
            }

            string oldRoleName = user.Role?.Name ?? "";
            var oldStoreIds = user.WarehouseScopes.Select(ws => ws.StoreId).OrderBy(x => x).ToList();

            // CHỐNG TỰ KHÓA (ANTI-SELF-LOCKOUT): Không cho phép tước quyền Admin hoặc khóa tài khoản của Admin hoạt động cuối cùng
            if (oldRoleName == AppRoles.Admin && (newRole.Name != AppRoles.Admin || !model.IsActive))
            {
                int otherActiveAdmins = await _context.Users
                    .Include(u => u.Role)
                    .CountAsync(u => u.Id != user.Id && u.IsActive && u.Role != null && u.Role.Name == AppRoles.Admin);

                if (otherActiveAdmins == 0)
                {
                    TempData["ErrorMessage"] = "Chặn thao tác: Không thể hạ vai trò hoặc vô hiệu hóa Quản trị viên (Admin) đang hoạt động cuối cùng của hệ thống!";
                    await LoadFormLookupsAsync();
                    return View(model);
                }
            }

            user.FullName = model.FullName.Trim();
            user.Email = model.Email.Trim();
            user.PhoneNumber = model.PhoneNumber?.Trim();
            user.IsActive = model.IsActive;
            user.RoleId = model.RoleId;
            user.Position = string.IsNullOrWhiteSpace(model.Position)
                ? AppRoles.GetDisplayName(newRole.Name)
                : model.Position.Trim();

            if (!string.IsNullOrWhiteSpace(model.Password))
            {
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password);
            }

            // Làm mới SecurityStamp để quyền mới / phạm vi kho mới có hiệu lực tức thì
            user.SecurityStamp = Guid.NewGuid().ToString("N");

            // Cập nhật phạm vi kho (UserWarehouseScope)
            _context.UserWarehouseScopes.RemoveRange(user.WarehouseScopes);

            int? adminId = await _userScope.GetUserIdAsync();
            var newStoreIds = new List<int>();

            if (newRole.Name != AppRoles.Admin && model.SelectedStoreIds != null && model.SelectedStoreIds.Any())
            {
                newStoreIds = model.SelectedStoreIds.Distinct().OrderBy(x => x).ToList();
                foreach (var sId in newStoreIds)
                {
                    _context.UserWarehouseScopes.Add(new UserWarehouseScope
                    {
                        UserId = user.Id,
                        StoreId = sId,
                        AssignedByUserId = adminId,
                        AssignedAt = DateTime.Now
                    });
                }
            }

            // Ghi AuditLog khi thay đổi vai trò hoặc phạm vi kho
            _context.AuditLogs.Add(new AuditLog
            {
                PerformedByUserId = adminId,
                Action = oldRoleName != newRole.Name ? "ChangeUserRole" : "UpdateUserScope",
                TargetUserId = user.Id,
                OldValue = $"Role={oldRoleName}, Active={user.IsActive}, Stores=[{string.Join(",", oldStoreIds)}]",
                NewValue = $"Role={newRole.Name}, Active={model.IsActive}, Stores=[{string.Join(",", newStoreIds)}]",
                Note = $"Cập nhật vai trò/phạm vi kho cho '{user.FullName}' ({user.Email})",
                CreatedAt = DateTime.Now
            });

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã cập nhật vai trò và phạm vi kho cho \"{user.FullName}\" thành công! Quyền mới đã có hiệu lực ngay lập tức.";
            return RedirectToAction(nameof(Index));
        }

        // ===== KHÓA / MỞ TÀI KHOẢN =====
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var user = await _context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == id);
            if (user == null) return RedirectToAction(nameof(Index));

            // Chặn tự khóa Admin cuối cùng
            if (user.IsActive && user.Role?.Name == AppRoles.Admin)
            {
                int otherActiveAdmins = await _context.Users
                    .Include(u => u.Role)
                    .CountAsync(u => u.Id != user.Id && u.IsActive && u.Role != null && u.Role.Name == AppRoles.Admin);

                if (otherActiveAdmins == 0)
                {
                    TempData["ErrorMessage"] = "Chặn thao tác: Không thể khóa Quản trị viên (Admin) đang hoạt động cuối cùng của hệ thống!";
                    return RedirectToAction(nameof(Index));
                }
            }

            bool oldStatus = user.IsActive;
            user.IsActive = !user.IsActive;
            user.SecurityStamp = Guid.NewGuid().ToString("N");

            int? adminId = await _userScope.GetUserIdAsync();
            _context.AuditLogs.Add(new AuditLog
            {
                PerformedByUserId = adminId,
                Action = user.IsActive ? "UnlockUser" : "LockUser",
                TargetUserId = user.Id,
                OldValue = $"IsActive={oldStatus}",
                NewValue = $"IsActive={user.IsActive}",
                Note = $"{(user.IsActive ? "Mở khóa" : "Khóa")} tài khoản '{user.FullName}' ({user.Email})",
                CreatedAt = DateTime.Now
            });

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = user.IsActive
                ? $"Đã mở khóa tài khoản \"{user.FullName}\"."
                : $"Đã khóa tài khoản \"{user.FullName}\". Phiên làm việc của tài khoản này đã bị vô hiệu hóa ngay lập tức.";

            return RedirectToAction(nameof(Index));
        }

        // ===== XEM CHI TIẾT NHÂN VIÊN =====
        public async Task<IActionResult> Detail(int id)
        {
            var user = await _context.Users
                .Include(x => x.Role)
                    .ThenInclude(r => r!.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
                .Include(x => x.WarehouseScopes)
                    .ThenInclude(ws => ws.Store)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (user == null || user.Role?.Name == AppRoles.Customer)
                return RedirectToAction(nameof(Index));

            ViewBag.TotalOrdersProcessed = await _context.Orders.CountAsync();
            ViewBag.AuditLogs = await _context.AuditLogs
                .Include(a => a.PerformedByUser)
                .Where(a => a.TargetUserId == id)
                .OrderByDescending(a => a.CreatedAt)
                .Take(20)
                .ToListAsync();

            return View(user);
        }
    }
}
