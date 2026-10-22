using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BeautyShop.Data;
using BeautyShop.Models;
using BeautyShop.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BeautyShop.Areas.Admin.Controllers
{
    [Area("Admin")]
    [RequirePermission(AppPermissions.RoleManage)]
    public class RoleManagementController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentUserScope _userScope;

        public RoleManagementController(ApplicationDbContext context, ICurrentUserScope userScope)
        {
            _context = context;
            _userScope = userScope;
        }

        // GET: /Admin/RoleManagement/Matrix
        public async Task<IActionResult> Matrix()
        {
            var roles = await _context.Roles
                .Include(r => r.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
                .Where(r => r.Name != AppRoles.Customer && r.Name != AppRoles.Employee)
                .OrderBy(r => r.Id)
                .ToListAsync();

            var permissions = await _context.Permissions
                .OrderBy(p => p.Id)
                .ToListAsync();

            var usersWithScopes = await _context.Users
                .Include(u => u.Role)
                .Include(u => u.WarehouseScopes)
                    .ThenInclude(ws => ws.Store)
                .Where(u => u.Role != null && u.Role.Name != AppRoles.Customer)
                .OrderBy(u => u.RoleId)
                .ThenBy(u => u.Id)
                .ToListAsync();

            ViewBag.Roles = roles;
            ViewBag.Permissions = permissions;
            ViewBag.StaffUsers = usersWithScopes;

            return View();
        }

        // POST: /Admin/RoleManagement/UpdateRolePermissions
        [HttpPost]
        public async Task<IActionResult> UpdateRolePermissions(int roleId, List<int>? permissionIds)
        {
            var role = await _context.Roles
                .Include(r => r.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
                .FirstOrDefaultAsync(r => r.Id == roleId);

            if (role == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy vai trò cần cập nhật.";
                return RedirectToAction(nameof(Matrix));
            }

            if (role.Name == AppRoles.Admin)
            {
                TempData["ErrorMessage"] = "Chặn tự khóa (Anti-Self-Lockout): Vai trò Quản trị viên (Admin) luôn giữ toàn bộ quyền hệ thống, không được phép gỡ quyền của Admin.";
                return RedirectToAction(nameof(Matrix));
            }

            permissionIds ??= new List<int>();
            var oldCodes = role.RolePermissions
                .Where(rp => rp.Permission != null)
                .Select(rp => rp.Permission!.Code)
                .OrderBy(c => c)
                .ToList();

            _context.RolePermissions.RemoveRange(role.RolePermissions);

            foreach (var permId in permissionIds.Distinct())
            {
                _context.RolePermissions.Add(new RolePermission
                {
                    RoleId = role.Id,
                    PermissionId = permId
                });
            }

            // Đổi SecurityStamp cho tất cả user thuộc vai trò này để làm mới quyền tức thì
            var affectedUsers = await _context.Users.Where(u => u.RoleId == role.Id).ToListAsync();
            foreach (var u in affectedUsers)
            {
                u.SecurityStamp = Guid.NewGuid().ToString("N");
            }

            var newCodes = await _context.Permissions
                .Where(p => permissionIds.Contains(p.Id))
                .Select(p => p.Code)
                .OrderBy(c => c)
                .ToListAsync();

            int? adminId = await _userScope.GetUserIdAsync();
            _context.AuditLogs.Add(new AuditLog
            {
                PerformedByUserId = adminId,
                Action = "UpdateRolePermissions",
                TargetUserId = null,
                OldValue = $"Role={role.Name}: [{string.Join(", ", oldCodes)}]",
                NewValue = $"Role={role.Name}: [{string.Join(", ", newCodes)}]",
                Note = $"Cập nhật ma trận quyền cho vai trò {role.DisplayName ?? role.Name} ({affectedUsers.Count} nhân viên được làm mới quyền tức thì)",
                CreatedAt = DateTime.Now
            });

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã cập nhật bộ quyền chức năng cho vai trò \"{role.DisplayName ?? role.Name}\". Quyền mới đã có hiệu lực tức thì cho {affectedUsers.Count} tài khoản.";
            return RedirectToAction(nameof(Matrix));
        }

        // GET: /Admin/RoleManagement/AuditLogs
        public async Task<IActionResult> AuditLogs(string? actionFilter)
        {
            var query = _context.AuditLogs
                .Include(a => a.PerformedByUser)
                .Include(a => a.TargetUser)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(actionFilter))
            {
                query = query.Where(a => a.Action == actionFilter);
            }

            var logs = await query
                .OrderByDescending(a => a.CreatedAt)
                .Take(100)
                .ToListAsync();

            ViewBag.ActionFilter = actionFilter;
            return View(logs);
        }
    }
}
