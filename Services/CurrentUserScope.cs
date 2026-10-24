using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using BeautyShop.Data;
using BeautyShop.Models;

namespace BeautyShop.Services
{
    public class CurrentUserScope : ICurrentUserScope
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor? _httpContextAccessor;
        private int? _manualUserId;

        public CurrentUserScope(ApplicationDbContext context, IHttpContextAccessor? httpContextAccessor = null)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        public void SetUserContext(int? userId)
        {
            _manualUserId = userId;
        }

        public int? CurrentUserId
        {
            get
            {
                if (_manualUserId.HasValue) return _manualUserId.Value;

                var httpContext = _httpContextAccessor?.HttpContext;
                if (httpContext == null) return null;

                var claimId = httpContext.User?.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!string.IsNullOrEmpty(claimId) && int.TryParse(claimId, out int parsedId))
                {
                    return parsedId;
                }

                try
                {
                    return httpContext.Session?.GetInt32("UserId");
                }
                catch
                {
                    return null;
                }
            }
        }

        public string? CurrentUserName
        {
            get
            {
                var user = GetCurrentUserAsync().GetAwaiter().GetResult();
                return user?.FullName;
            }
        }

        public string? CurrentRole
        {
            get
            {
                var user = GetCurrentUserAsync().GetAwaiter().GetResult();
                if (user == null || !user.IsActive) return null;
                return user.Role?.Name;
            }
        }

        public bool IsAuthenticated
        {
            get
            {
                var user = GetCurrentUserAsync().GetAwaiter().GetResult();
                return user != null && user.IsActive;
            }
        }

        public bool IsAdmin => CurrentRole == AppRoles.Admin;

        public Task<int?> GetUserIdAsync() => Task.FromResult(CurrentUserId);

        public async Task<string?> GetRoleNameAsync()
        {
            var user = await GetCurrentUserAsync();
            if (user == null || !user.IsActive) return null;
            return user.Role?.Name;
        }

        public async Task<bool> IsAdminAsync()
        {
            var role = await GetRoleNameAsync();
            return role == AppRoles.Admin;
        }

        public async Task<User?> GetCurrentUserAsync()
        {
            int? uid = CurrentUserId;
            if (!uid.HasValue) return null;

            return await _context.Users
                .AsNoTracking()
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == uid.Value);
        }

        /// <summary>
        /// Đọc trực tiếp danh sách quyền từ CSDL theo Role hiện tại của người dùng.
        /// Đảm bảo khi Admin thay đổi vai trò hoặc ma trận quyền, quyền mới có hiệu lực ngay lập tức ở request tiếp theo.
        /// </summary>
        public async Task<HashSet<string>> GetPermissionsAsync()
        {
            var user = await GetCurrentUserAsync();
            if (user == null || !user.IsActive || user.Role == null)
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            if (user.Role.Name == AppRoles.Admin)
            {
                return new HashSet<string>(AppPermissions.All, StringComparer.OrdinalIgnoreCase);
            }

            var perms = await _context.RolePermissions
                .AsNoTracking()
                .Where(rp => rp.RoleId == user.RoleId && rp.Permission != null)
                .Select(rp => rp.Permission!.Code)
                .ToListAsync();

            return new HashSet<string>(perms, StringComparer.OrdinalIgnoreCase);
        }

        public async Task<bool> HasPermissionAsync(string permissionCode)
        {
            if (string.IsNullOrWhiteSpace(permissionCode)) return false;
            var perms = await GetPermissionsAsync();
            return perms.Contains(permissionCode);
        }

        /// <summary>
        /// Lấy danh sách StoreId trong phạm vi của người dùng.
        /// - Admin: Thấy tất cả các kho (không lưu dòng dư thừa trong UserWarehouseScopes).
        /// - Nhân viên khác: Đọc trực tiếp từ bảng UserWarehouseScopes.
        /// </summary>
        public async Task<List<int>> GetAllowedStoreIdsAsync()
        {
            var user = await GetCurrentUserAsync();
            if (user == null || !user.IsActive || user.Role == null)
            {
                return new List<int>();
            }

            if (user.Role.Name == AppRoles.Admin)
            {
                return await _context.Stores
                    .AsNoTracking()
                    .Select(s => s.Id)
                    .ToListAsync();
            }

            return await _context.UserWarehouseScopes
                .AsNoTracking()
                .Where(s => s.UserId == user.Id)
                .Select(s => s.StoreId)
                .Distinct()
                .ToListAsync();
        }

        public async Task<bool> HasAccessToStoreAsync(int storeId)
        {
            if (!IsAuthenticated) return false;
            if (IsAdmin) return true;

            var allowed = await GetAllowedStoreIdsAsync();
            return allowed.Contains(storeId);
        }

        public async Task<IQueryable<Inventory>> ApplyInventoryScopeAsync(IQueryable<Inventory> query)
        {
            if (!IsAuthenticated) return query.Where(i => false);
            var allowed = await GetAllowedStoreIdsAsync();
            return query.WhereInStoreScope(IsAdmin, allowed);
        }

        public async Task<IQueryable<StockReceipt>> ApplyReceiptScopeAsync(IQueryable<StockReceipt> query)
        {
            if (!IsAuthenticated) return query.Where(r => false);
            var allowed = await GetAllowedStoreIdsAsync();
            return query.WhereInStoreScope(IsAdmin, allowed);
        }

        public async Task<IQueryable<StockTransfer>> ApplyTransferScopeAsync(IQueryable<StockTransfer> query)
        {
            if (!IsAuthenticated) return query.Where(t => false);
            var allowed = await GetAllowedStoreIdsAsync();
            return query.WhereInTransferScope(IsAdmin, allowed);
        }

        public async Task<IQueryable<Order>> ApplyOrderScopeAsync(IQueryable<Order> query)
        {
            if (!IsAuthenticated) return query.Where(o => false);
            var allowed = await GetAllowedStoreIdsAsync();
            return query.WhereInOrderScope(IsAdmin, allowed);
        }

        public async Task<IQueryable<DeliveryTrip>> ApplyTripScopeAsync(IQueryable<DeliveryTrip> query)
        {
            if (!IsAuthenticated) return query.Where(t => false);
            var allowed = await GetAllowedStoreIdsAsync();
            return query.WhereInTripScope(IsAdmin, CurrentRole, CurrentUserId, allowed);
        }

        public async Task<IQueryable<DailyReconciliation>> ApplyReconciliationScopeAsync(IQueryable<DailyReconciliation> query)
        {
            if (!IsAuthenticated) return query.Where(r => false);
            var allowed = await GetAllowedStoreIdsAsync();
            return query.WhereInReconciliationScope(IsAdmin, allowed);
        }

        public async Task<bool> CanViewTransferAsync(StockTransfer transfer)
        {
            if (!IsAuthenticated || transfer == null) return false;
            if (IsAdmin) return true;

            var allowed = await GetAllowedStoreIdsAsync();
            return allowed.Contains(transfer.FromStoreId) || allowed.Contains(transfer.ToStoreId);
        }

        /// <summary>
        /// Xác nhận nhận phiếu chuyển kho: CHỈ cho phép người có phạm vi tại Kho đến (ToStoreId) hoặc Admin.
        /// </summary>
        public async Task<bool> CanReceiveTransferAsync(StockTransfer transfer)
        {
            if (!IsAuthenticated || transfer == null) return false;
            if (IsAdmin) return true;

            var allowed = await GetAllowedStoreIdsAsync();
            return allowed.Contains(transfer.ToStoreId);
        }

        /// <summary>
        /// Hủy phiếu chuyển kho: CHỈ cho phép người có phạm vi tại Kho đi (FromStoreId) hoặc Admin.
        /// </summary>
        public async Task<bool> CanCancelTransferAsync(StockTransfer transfer)
        {
            if (!IsAuthenticated || transfer == null) return false;
            if (IsAdmin) return true;

            var allowed = await GetAllowedStoreIdsAsync();
            return allowed.Contains(transfer.FromStoreId);
        }
    }
}
