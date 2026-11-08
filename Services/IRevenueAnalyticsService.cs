using BeautyShop.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BeautyShop.Services
{
    public class TopProductRevenueDto
    {
        public int VariantId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? Sku { get; set; }
        public string? ColorName { get; set; }
        public string? Volume { get; set; }
        public int TotalSold { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal TotalCost { get; set; }
        public decimal GrossProfit => TotalRevenue - TotalCost;
    }

    public class OrderStatusCountDto
    {
        public string Status { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal Percentage { get; set; }
        public string ColorHex { get; set; } = "#64748b";
        public string BadgeClass { get; set; } = "bg-secondary";
    }

    /// <summary>
    /// DTO chi tiết hóa đơn/đơn hàng trong ngày phục vụ báo cáo kiểm soát doanh thu (Yêu cầu 4)
    /// </summary>
    public class DailyOrderInvoiceDto
    {
        public int OrderId { get; set; }
        public string OrderCode { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string ShippingAddress { get; set; } = string.Empty;
        public string OrderStatus { get; set; } = string.Empty;
        public string StatusDisplayName { get; set; } = string.Empty;
        public string StatusBadgeClass { get; set; } = "bg-secondary";
        public DateTime? StatusUpdatedAt { get; set; }

        public decimal TotalAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal DeliveryFee { get; set; }
        public decimal FinalAmount { get; set; }

        // Các phần tiền chia nhóm chi tiết
        public decimal DepositAmount { get; set; } = 0;
        public DateTime? DepositDate { get; set; }

        public decimal PaymentAmount { get; set; } = 0;
        public DateTime? PaymentDate { get; set; }

        public decimal MoneyReceivedAmount { get; set; } = 0;
        public DateTime? MoneyReceivedDate { get; set; }

        public decimal RevenueRecognized { get; set; } = 0; // Chỉ tính vào doanh thu nếu đơn Completed và tiền nhận đúng ngày

        public string DeliveryType { get; set; } = "Internal";
        public string? DeliveryTypeLabel { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string StatusGroup { get; set; } = "Other"; // "Completed", "Deposit", "Processing", "Cancelled"
    }

    /// <summary>
    /// Báo cáo thống kê doanh thu theo ngày và kiểm soát đối chiếu hóa đơn (Yêu cầu 4)
    /// </summary>
    public class DailyRevenueReportDto
    {
        public DateTime ReportDate { get; set; }
        public int TotalOrderCount { get; set; }

        // Tổng tiền chia nhóm
        public decimal TotalGrossAmount { get; set; } // Tổng tiền danh sách hóa đơn
        public decimal TotalCompletedRevenue { get; set; } // Tổng doanh thu thực nhận (chỉ đơn Completed)
        public decimal TotalDepositCollected { get; set; } // Tổng tiền cọc đã thu
        public decimal TotalCancelledAmount { get; set; } // Tổng tiền đơn hủy (thể hiện để khớp tổng tiền)
        public decimal TotalPendingAmount { get; set; } // Tổng tiền đang chờ thu

        // Đếm theo nhóm
        public int CompletedCount { get; set; }
        public int DepositCount { get; set; }
        public int ProcessingCount { get; set; }
        public int CancelledCount { get; set; }

        public List<DailyOrderInvoiceDto> Invoices { get; set; } = new();
    }

    public interface IRevenueAnalyticsService
    {
        /// <summary>
        /// Bộ lọc chuẩn duy nhất: Chỉ đơn có trạng thái "Completed" và CompletedAt != null mới được tính doanh thu.
        /// Loại trừ hoàn toàn: Pending, Confirmed, Processing, Shipping, Delivering, Failed, Cancelled.
        /// </summary>
        IQueryable<Order> GetCompletedRevenueOrders(IQueryable<Order> query);

        /// <summary>
        /// Tính tổng doanh thu theo ngày hoàn thành (CompletedAt).
        /// </summary>
        Task<decimal> GetRevenueAsync(IQueryable<Order> query, DateTime? fromDate = null, DateTime? toDate = null);

        /// <summary>
        /// Đếm số đơn hoàn thành theo ngày hoàn thành (CompletedAt).
        /// </summary>
        Task<int> GetCompletedOrderCountAsync(IQueryable<Order> query, DateTime? fromDate = null, DateTime? toDate = null);

        /// <summary>
        /// Tính Giá vốn hàng bán (COGS) của các đơn hoàn thành theo ngày hoàn thành.
        /// </summary>
        Task<decimal> GetCogsAsync(IQueryable<Order> query, DateTime? fromDate = null, DateTime? toDate = null);

        /// <summary>
        /// Tính Lợi nhuận gộp (Gross Profit = Revenue - COGS) của các đơn hoàn thành.
        /// </summary>
        Task<decimal> GetGrossProfitAsync(IQueryable<Order> query, DateTime? fromDate = null, DateTime? toDate = null);

        /// <summary>
        /// Top sản phẩm mang lại doanh thu cao nhất (chỉ tính từ các đơn hoàn thành).
        /// </summary>
        Task<List<TopProductRevenueDto>> GetTopSellingProductsAsync(IQueryable<Order> query, int topN = 5, DateTime? fromDate = null, DateTime? toDate = null);

        /// <summary>
        /// Phân bổ chi tiết số lượng và phần trăm từng trạng thái đơn hàng.
        /// </summary>
        Task<List<OrderStatusCountDto>> GetOrderStatusDistributionAsync(IQueryable<Order> query);

        /// <summary>
        /// Lấy báo cáo chi tiết hóa đơn trong ngày phục vụ kiểm tra và đối chiếu doanh thu (Yêu cầu 4)
        /// - Hiển thị tất cả theo ngày rồi mới lọc theo tình trạng
        /// - Thể hiện đơn hủy để khớp tổng tiền
        /// - Chia thành các nhóm: đặt cọc, thanh toán, hoàn thành, hủy
        /// - Tính ngày nhận tiền (tiền về hôm sau tính vào ngày nhận tiền)
        /// </summary>
        Task<DailyRevenueReportDto> GetDailyRevenueReportAsync(IQueryable<Order> query, DateTime targetDate, string? statusGroupFilter = null);

        /// <summary>
        /// Chuyển đơn sang hoàn thành: Gán OrderStatus = "Completed" và lưu CompletedAt (bảo toàn nếu đã có sẵn).
        /// </summary>
        void MarkOrderAsCompleted(Order order, DateTime? completionTime = null);
    }
}
