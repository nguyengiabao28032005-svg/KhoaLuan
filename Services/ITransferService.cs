using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BeautyShop.Models;

namespace BeautyShop.Services
{
    public class DestinationStoreOption
    {
        public int StoreId { get; set; }
        public string StoreName { get; set; } = string.Empty;
        public string? ProvinceName { get; set; }
        public string? Region { get; set; }
        public string? StoreType { get; set; }
        public bool IsDirect { get; set; }
        public int? TransitStoreId { get; set; }
        public string? TransitStoreName { get; set; }
        public decimal? DistanceKm { get; set; }
        public decimal? EstimatedHours { get; set; }
        public string? RouteDescription { get; set; }
    }

    public class TransitWaitingBatchItem
    {
        public string BatchCode { get; set; } = string.Empty;
        public int LastTransferId { get; set; }
        public string LastTransferCode { get; set; } = string.Empty;
        public int OriginStoreId { get; set; }
        public string OriginStoreName { get; set; } = string.Empty;
        public int CurrentHubStoreId { get; set; }
        public string CurrentHubStoreName { get; set; } = string.Empty;
        public int FinalDestinationStoreId { get; set; }
        public string FinalDestinationStoreName { get; set; } = string.Empty;
        public DateTime ArrivedAtHubDate { get; set; }
        public List<StockTransferLine> Items { get; set; } = new();
        public int TotalQuantity { get; set; }
    }

    public interface ITransferService
    {
        Task<List<DestinationStoreOption>> GetValidDestinationsAsync(int fromStoreId);

        Task<(bool Success, string Message, StockTransfer? Transfer)> CreateTransferAsync(
            int fromStoreId,
            int toStoreId,
            int finalDestinationStoreId,
            string? batchCode,
            List<(int VariantId, int Quantity)> items,
            int? userId,
            string? userName,
            string? note);

        Task<(bool Success, string Message, bool IsAtFinalDestination)> ReceiveTransferAsync(
            int transferId,
            int? userId,
            string? userName,
            string? note);

        Task<(bool Success, string Message)> CancelTransferAsync(
            int transferId,
            int? userId,
            string? userName,
            string? reason);

        Task<List<StockTransfer>> GetBatchHistoryAsync(string batchCode);

        Task<List<TransitWaitingBatchItem>> GetTransitWaitingBatchesAsync(int hubStoreId = 5);

        Task<(bool Success, string Message)> ToggleRouteStatusAsync(int routeId);
    }
}
