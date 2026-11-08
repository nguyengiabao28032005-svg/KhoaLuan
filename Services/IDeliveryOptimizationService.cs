using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BeautyShop.Models;

namespace BeautyShop.Services
{
    public interface IDeliveryOptimizationService
    {
        // 1. Phân vùng & Nhận diện
        Task<DeliveryZone?> DetectZoneAsync(string shippingAddress);
        Task<bool> AssignZoneToOrderAsync(int orderId, int zoneId);

        // 2. Lấy danh sách đơn chờ giao
        Task<List<Order>> GetPendingDeliveryOrdersAsync(DateTime tripDate, string shift, string? clusterCode = null);

        // 3. Gom chuyến tự động & Lập chuyến thủ công
        Task<(bool Success, string Message, List<DeliveryTrip> CreatedTrips)> AutoBatchTripsAsync(
            DateTime tripDate, 
            string shift, 
            int? maxOrdersPerTrip = null, 
            int? createdByUserId = null);

        Task<(bool Success, string Message, DeliveryTrip? Trip)> CreateManualTripAsync(
            DateTime tripDate, 
            string shift, 
            string clusterCode, 
            int? shipperUserId, 
            List<int> orderIds, 
            int? createdByUserId = null);

        Task<(bool Success, string Message)> UpdateTripStopsOrderAsync(int tripId, List<int> orderedStopIds);
        Task<(bool Success, string Message)> AssignShipperAsync(int tripId, int shipperUserId);
        
        /// <summary>
        /// Nhân viên kho phân công nhân viên giao hàng, ghi tên người bàn giao và giờ nhận hàng (Yêu cầu 3)
        /// </summary>
        Task<(bool Success, string Message)> AssignShipperWithHandoverAsync(
            int tripId, 
            int shipperUserId, 
            string handoverStaffName, 
            DateTime departureTime);

        Task<(bool Success, string Message)> StartTripAsync(int tripId, int shipperUserId);
        Task<(bool Success, string Message)> RemoveOrderFromTripAsync(int tripId, int orderId, string reason);
        Task<(bool Success, string Message)> CancelOrderAndRemoveFromTripAsync(int orderId, string reason);

        // 4. Shipper cập nhật kết quả giao
        Task<(bool Success, string Message)> UpdateStopStatusAsync(
            int stopId, 
            string newStatus, 
            decimal? codCollected, 
            string? failureReason, 
            string? notes, 
            int currentUserId);

        // 5. Đối soát tiền COD chuyến (Thu ngân nhận từ Shipper)
        Task<(bool Success, string Message)> SettleTripCodAsync(
            int tripId, 
            int cashierUserId, 
            decimal receivedAmount, 
            string? notes);

        // 5b. Luồng bàn giao và nhận tiền bên chuyển phát thứ ba (GHN, Đi tỉnh - Yêu cầu 3)
        Task<(bool Success, string Message)> HandoverToThirdPartyCarrierAsync(
            int orderId, 
            string carrierStaffName, 
            string ghnOrderCode, 
            DateTime handoverAt);

        Task<(bool Success, string Message)> ConfirmCarrierPaymentReceivedAsync(
            int orderId, 
            decimal amount, 
            DateTime receivedDate, 
            string paymentMethod, 
            string? note, 
            int userId, 
            string userName);

        // 6. Đối soát cuối ngày (Thu ngân chốt sổ nộp Admin)
        Task<(bool Success, string Message, DailyReconciliation? Reconciliation)> CreateDailyReconciliationAsync(
            DateTime date, 
            int cashierUserId, 
            decimal totalCashSubmitted, 
            string? differenceReason);

        Task<(bool Success, string Message)> ApproveDailyReconciliationAsync(int reconciliationId, int adminUserId);
    }
}
