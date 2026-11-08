using System.Collections.Generic;
using System.Linq;
using BeautyShop.Models;

namespace BeautyShop.Services
{
    /// <summary>
    /// Tập trung toàn bộ bộ lọc phạm vi dữ liệu (Data Scope) tại một nơi duy nhất (Bước 6).
    /// Mọi truy vấn (danh sách, tìm kiếm, báo cáo, API) đều đi qua các extension method này,
    /// không viết rải rác câu lệnh Where ở từng Controller.
    /// Lưu ý: Khi isAdmin = true (Quản trị viên hoặc Job nền hợp lệ), bộ lọc được bỏ qua để xem toàn hệ thống.
    /// </summary>
    public static class DataScopeExtensions
    {
        public static IQueryable<Inventory> WhereInStoreScope(
            this IQueryable<Inventory> query,
            bool isAdmin,
            IReadOnlyCollection<int> allowedStoreIds)
        {
            if (isAdmin) return query; // Admin xem toàn bộ tồn kho các miền
            return query.Where(i => allowedStoreIds.Contains(i.StoreId));
        }

        public static IQueryable<StockReceipt> WhereInStoreScope(
            this IQueryable<StockReceipt> query,
            bool isAdmin,
            IReadOnlyCollection<int> allowedStoreIds)
        {
            if (isAdmin) return query; // Admin xem toàn bộ phiếu nhập kho
            return query.Where(r => allowedStoreIds.Contains(r.StoreId));
        }

        /// <summary>
        /// Phiếu chuyển kho: Nhân viên thấy phiếu khi Kho đi (FromStoreId) HOẶC Kho đến (ToStoreId) nằm trong phạm vi của mình.
        /// </summary>
        public static IQueryable<StockTransfer> WhereInTransferScope(
            this IQueryable<StockTransfer> query,
            bool isAdmin,
            IReadOnlyCollection<int> allowedStoreIds)
        {
            if (isAdmin) return query; // Admin xem toàn bộ luân chuyển kho liên tỉnh
            return query.Where(t => allowedStoreIds.Contains(t.FromStoreId) || allowedStoreIds.Contains(t.ToStoreId));
        }

        /// <summary>
        /// Đơn hàng: Gắn với kho xuất hàng (StoreId, mặc định 1 là Kho tổng TP.HCM nếu đơn cũ chưa gán).
        /// Thu ngân / Nhân viên kho chỉ thấy đơn hàng thuộc phạm vi kho của mình.
        /// </summary>
        public static IQueryable<Order> WhereInOrderScope(
            this IQueryable<Order> query,
            bool isAdmin,
            IReadOnlyCollection<int> allowedStoreIds)
        {
            if (isAdmin) return query; // Admin xem mọi đơn hàng toàn quốc
            return query.Where(o => allowedStoreIds.Contains(o.StoreId ?? 1));
        }

        /// <summary>
        /// Chuyến giao hàng:
        /// - Admin: Thấy toàn bộ chuyến giao.
        /// - Nhân viên giao hàng (Shipper): Chỉ thấy chuyến giao được phân công cho chính mình (ShipperUserId == currentUserId).
        /// - Thu ngân / Điều phối: Thấy các chuyến xuất phát từ kho thuộc phạm vi của mình.
        /// </summary>
        public static IQueryable<DeliveryTrip> WhereInTripScope(
            this IQueryable<DeliveryTrip> query,
            bool isAdmin,
            string? currentRole,
            int? currentUserId,
            IReadOnlyCollection<int> allowedStoreIds)
        {
            if (isAdmin) return query;

            if (currentRole == AppRoles.Shipper)
            {
                if (!currentUserId.HasValue) return query.Where(t => false);
                return query.Where(t => t.ShipperUserId == currentUserId.Value);
            }

            return query.Where(t => allowedStoreIds.Contains(t.StoreId ?? 1));
        }

        /// <summary>
        /// Bảng đối soát cuối ngày & Báo cáo bán chạy/bán chậm:
        /// - Admin: Xem và phê duyệt đối soát của tất cả các kho.
        /// - Thu ngân: Chỉ xem và lập đối soát cho kho thuộc phạm vi của mình.
        /// </summary>
        public static IQueryable<DailyReconciliation> WhereInReconciliationScope(
            this IQueryable<DailyReconciliation> query,
            bool isAdmin,
            IReadOnlyCollection<int> allowedStoreIds)
        {
            if (isAdmin) return query;
            return query.Where(r => allowedStoreIds.Contains(r.StoreId ?? 1));
        }
    }
}
