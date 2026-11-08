using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BeautyShop.Models;

namespace BeautyShop.Services
{
    /// <summary>
    /// Dịch vụ xác định người dùng hiện tại, vai trò chức năng, bộ quyền (Permissions)
    /// và phạm vi dữ liệu kho (Data Scope) tập trung cho toàn hệ thống (Bước 6).
    /// </summary>
    public interface ICurrentUserScope
    {
        int? CurrentUserId { get; }
        string? CurrentUserName { get; }
        string? CurrentRole { get; }
        bool IsAuthenticated { get; }
        bool IsAdmin { get; }

        /// <summary>
        /// Cho phép thiết lập ngữ cảnh người dùng thủ công (phục vụ kiểm thử tự động xUnit hoặc job nền)
        /// </summary>
        void SetUserContext(int? userId);

        Task<int?> GetUserIdAsync();
        Task<string?> GetRoleNameAsync();
        Task<bool> IsAdminAsync();
        Task<User?> GetCurrentUserAsync();
        Task<HashSet<string>> GetPermissionsAsync();
        Task<bool> HasPermissionAsync(string permissionCode);

        /// <summary>
        /// Danh sách ID các kho nằm trong phạm vi của người dùng.
        /// Nếu là Admin: trả về tất cả các kho trong hệ thống (không cần lưu trong bảng UserWarehouseScope).
        /// </summary>
        Task<List<int>> GetAllowedStoreIdsAsync();
        Task<bool> HasAccessToStoreAsync(int storeId);

        // ===== BỘ LỌC PHẠM VI DỮ LIỆU TẬP TRUNG =====
        Task<IQueryable<Inventory>> ApplyInventoryScopeAsync(IQueryable<Inventory> query);
        Task<IQueryable<StockReceipt>> ApplyReceiptScopeAsync(IQueryable<StockReceipt> query);
        Task<IQueryable<StockTransfer>> ApplyTransferScopeAsync(IQueryable<StockTransfer> query);
        Task<IQueryable<Order>> ApplyOrderScopeAsync(IQueryable<Order> query);
        Task<IQueryable<DeliveryTrip>> ApplyTripScopeAsync(IQueryable<DeliveryTrip> query);
        Task<IQueryable<DailyReconciliation>> ApplyReconciliationScopeAsync(IQueryable<DailyReconciliation> query);

        // ===== KIỂM TRA TRUY CẬP BẢN GHI CỤ THỂ THEO ID =====
        Task<bool> CanViewTransferAsync(StockTransfer transfer);
        Task<bool> CanReceiveTransferAsync(StockTransfer transfer);
        Task<bool> CanCancelTransferAsync(StockTransfer transfer);
    }
}
