using BeautyShop.Data;
using BeautyShop.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BeautyShop.Services
{
    public class RevenueAnalyticsService : IRevenueAnalyticsService
    {
        private readonly ApplicationDbContext _context;

        public RevenueAnalyticsService(ApplicationDbContext context)
        {
            _context = context;
        }

        public IQueryable<Order> GetCompletedRevenueOrders(IQueryable<Order> query)
        {
            // Định nghĩa chuẩn duy nhất cho "Đơn tính doanh thu":
            // Bắt buộc OrderStatus == "Completed" và (MoneyReceivedDate != null hoặc CompletedAt != null).
            // Loại trừ hoàn toàn: Pending, Confirmed, Processing, Shipping, Delivering, Failed, Cancelled.
            return query.Where(o => o.OrderStatus == "Completed" && (o.MoneyReceivedDate != null || o.CompletedAt != null));
        }

        public async Task<decimal> GetRevenueAsync(IQueryable<Order> query, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var completedQuery = GetCompletedRevenueOrders(query);

            if (fromDate.HasValue)
            {
                completedQuery = completedQuery.Where(o => (o.MoneyReceivedDate ?? o.CompletedAt) >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                completedQuery = completedQuery.Where(o => (o.MoneyReceivedDate ?? o.CompletedAt) <= toDate.Value);
            }

            var amounts = await completedQuery.Select(o => o.FinalAmount).ToListAsync();
            return amounts.Sum();
        }

        public async Task<int> GetCompletedOrderCountAsync(IQueryable<Order> query, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var completedQuery = GetCompletedRevenueOrders(query);

            if (fromDate.HasValue)
            {
                completedQuery = completedQuery.Where(o => (o.MoneyReceivedDate ?? o.CompletedAt) >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                completedQuery = completedQuery.Where(o => (o.MoneyReceivedDate ?? o.CompletedAt) <= toDate.Value);
            }

            return await completedQuery.CountAsync();
        }

        public async Task<decimal> GetCogsAsync(IQueryable<Order> query, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var completedOrders = GetCompletedRevenueOrders(query);

            if (fromDate.HasValue)
            {
                completedOrders = completedOrders.Where(o => o.CompletedAt >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                completedOrders = completedOrders.Where(o => o.CompletedAt <= toDate.Value);
            }

            var orderIds = await completedOrders.Select(o => o.Id).ToListAsync();
            if (!orderIds.Any()) return 0m;

            var details = await _context.OrderDetails
                .Include(d => d.Variant)
                .Where(d => orderIds.Contains(d.OrderId))
                .ToListAsync();

            decimal totalCogs = details.Sum(d =>
            {
                decimal unitCost = d.CostPrice > 0 ? d.CostPrice : (d.Variant?.OriginalPrice ?? 0m);
                return d.Quantity * unitCost;
            });

            return totalCogs;
        }

        public async Task<decimal> GetGrossProfitAsync(IQueryable<Order> query, DateTime? fromDate = null, DateTime? toDate = null)
        {
            decimal revenue = await GetRevenueAsync(query, fromDate, toDate);
            decimal cogs = await GetCogsAsync(query, fromDate, toDate);
            return revenue - cogs;
        }

        public async Task<List<TopProductRevenueDto>> GetTopSellingProductsAsync(IQueryable<Order> query, int topN = 5, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var completedOrders = GetCompletedRevenueOrders(query);

            if (fromDate.HasValue)
            {
                completedOrders = completedOrders.Where(o => o.CompletedAt >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                completedOrders = completedOrders.Where(o => o.CompletedAt <= toDate.Value);
            }

            var orderIds = await completedOrders.Select(o => o.Id).ToListAsync();
            if (!orderIds.Any()) return new List<TopProductRevenueDto>();

            var details = await _context.OrderDetails
                .Include(d => d.Variant)
                    .ThenInclude(v => v != null ? v.Product : null)
                .Where(d => orderIds.Contains(d.OrderId))
                .ToListAsync();

            var topProducts = details
                .GroupBy(d => d.VariantId)
                .Select(g =>
                {
                    var first = g.First();
                    var v = first.Variant;
                    var prodName = v?.Product?.Name ?? $"Sản phẩm #{g.Key}";
                    int totalQty = g.Sum(x => x.Quantity);
                    decimal totalRev = g.Sum(x => x.Quantity * x.UnitPrice);
                    decimal totalCost = g.Sum(x =>
                    {
                        decimal cost = x.CostPrice > 0 ? x.CostPrice : (v?.OriginalPrice ?? 0m);
                        return x.Quantity * cost;
                    });

                    return new TopProductRevenueDto
                    {
                        VariantId = g.Key,
                        ProductName = prodName,
                        Sku = v?.SKU,
                        ColorName = v?.ColorName,
                        Volume = v?.Volume,
                        TotalSold = totalQty,
                        TotalRevenue = totalRev,
                        TotalCost = totalCost
                    };
                })
                .OrderByDescending(x => x.TotalRevenue)
                .Take(topN)
                .ToList();

            return topProducts;
        }

        public async Task<List<OrderStatusCountDto>> GetOrderStatusDistributionAsync(IQueryable<Order> query)
        {
            var list = await query
                .GroupBy(o => o.OrderStatus)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            int totalOrders = list.Sum(x => x.Count);

            var result = new List<OrderStatusCountDto>();
            foreach (var item in list)
            {
                string displayName = item.Status;
                string colorHex = "#64748b";
                string badgeClass = "bg-secondary";

                switch (item.Status)
                {
                    case "Pending":
                    case "Chờ duyệt":
                    case "Chờ xác nhận":
                        displayName = "Chờ duyệt";
                        colorHex = "#f59e0b"; // Orange
                        badgeClass = "bg-warning text-dark";
                        break;
                    case "Confirmed":
                    case "Đã xác nhận":
                        displayName = "Đã xác nhận";
                        colorHex = "#3b82f6"; // Blue
                        badgeClass = "bg-primary";
                        break;
                    case "Processing":
                    case "Đóng gói":
                        displayName = "Đang xử lý / Đóng gói";
                        colorHex = "#8b5cf6"; // Purple
                        badgeClass = "bg-info text-dark";
                        break;
                    case "Shipping":
                    case "Delivering":
                    case "Đang giao":
                    case "Đang giao hàng":
                        displayName = "Đang giao hàng";
                        colorHex = "#06b6d4"; // Cyan
                        badgeClass = "bg-info";
                        break;
                    case "Completed":
                    case "Giao thành công":
                        displayName = "Giao thành công";
                        colorHex = "#10b981"; // Green
                        badgeClass = "bg-success";
                        break;
                    case "Failed":
                    case "Giao thất bại":
                    case "PendingResolution":
                        displayName = "Giao thất bại";
                        colorHex = "#f43f5e"; // Rose
                        badgeClass = "bg-danger";
                        break;
                    case "Cancelled":
                    case "Hủy bỏ đơn":
                    case "Đã hủy":
                        displayName = "Đã hủy";
                        colorHex = "#ef4444"; // Red
                        badgeClass = "bg-secondary";
                        break;
                    default:
                        displayName = item.Status ?? "Khác";
                        break;
                }

                decimal percentage = totalOrders > 0 ? Math.Round((decimal)item.Count * 100m / totalOrders, 1) : 0m;

                result.Add(new OrderStatusCountDto
                {
                    Status = item.Status,
                    DisplayName = displayName,
                    Count = item.Count,
                    Percentage = percentage,
                    ColorHex = colorHex,
                    BadgeClass = badgeClass
                });
            }

            return result.OrderByDescending(r => r.Count).ToList();
        }

        public void MarkOrderAsCompleted(Order order, DateTime? completionTime = null)
        {
            order.OrderStatus = "Completed";
            // Bảo toàn CompletedAt nếu đã có từ trước (không bị tính lại khi chỉnh sửa)
            if (!order.CompletedAt.HasValue)
            {
                order.CompletedAt = completionTime ?? DateTime.Now;
            }
            if (!order.MoneyReceivedDate.HasValue)
            {
                order.MoneyReceivedDate = order.CompletedAt;
            }
            order.StatusUpdatedAt = DateTime.Now;
            order.UpdatedAt = DateTime.Now;
        }

        public async Task<DailyRevenueReportDto> GetDailyRevenueReportAsync(IQueryable<Order> query, DateTime targetDate, string? statusGroupFilter = null)
        {
            var date = targetDate.Date;
            var nextDate = date.AddDays(1);

            // Các đơn phát sinh hoặc có biến động tiền trong ngày được chọn
            var allOrders = await query
                .Where(o =>
                    (o.CreatedAt >= date && o.CreatedAt < nextDate) ||
                    (o.MoneyReceivedDate != null && o.MoneyReceivedDate >= date && o.MoneyReceivedDate < nextDate) ||
                    (o.CompletedAt != null && o.CompletedAt >= date && o.CompletedAt < nextDate) ||
                    (o.StatusUpdatedAt != null && o.StatusUpdatedAt >= date && o.StatusUpdatedAt < nextDate) ||
                    (o.DepositDate != null && o.DepositDate >= date && o.DepositDate < nextDate))
                .OrderByDescending(o => o.MoneyReceivedDate ?? o.CompletedAt ?? o.StatusUpdatedAt ?? o.CreatedAt)
                .ToListAsync();

            var report = new DailyRevenueReportDto
            {
                ReportDate = date
            };

            var invoiceList = new List<DailyOrderInvoiceDto>();

            foreach (var o in allOrders)
            {
                string statusGroup = "Processing";
                string badgeClass = "bg-warning text-dark";
                string displayStatus = o.OrderStatus;

                if (o.OrderStatus == "Completed")
                {
                    statusGroup = "Completed";
                    badgeClass = "bg-success text-white";
                    displayStatus = "Đã hoàn thành";
                }
                else if (o.OrderStatus == "Cancelled")
                {
                    statusGroup = "Cancelled";
                    badgeClass = "bg-danger text-white";
                    displayStatus = "Đã hủy đơn";
                }
                else if (o.DepositAmount > 0)
                {
                    statusGroup = "Deposit";
                    badgeClass = "bg-primary text-white";
                    displayStatus = "Đã đặt cọc";
                }
                else
                {
                    statusGroup = "Processing";
                    badgeClass = "bg-warning text-dark";
                    displayStatus = o.OrderStatus == "Shipping" ? "Đang giao hàng" : "Đang xử lý";
                }

                // Kiểm tra xem tiền nhận đúng ngày này không
                var moneyDate = o.MoneyReceivedDate ?? o.CompletedAt;
                bool isMoneyReceivedToday = moneyDate.HasValue && moneyDate.Value.Date == date;

                // CHỈ CỘNG DOANH THU TỪ ĐƠN ĐÃ HOÀN THÀNH:
                // Nếu đặt hôm nay nhưng tiền về hôm sau thì tính vào ngày nhận tiền!
                decimal revenue = 0;
                if (o.OrderStatus == "Completed" && isMoneyReceivedToday)
                {
                    revenue = o.FinalAmount;
                }

                var inv = new DailyOrderInvoiceDto
                {
                    OrderId = o.Id,
                    OrderCode = o.OrderCode,
                    CustomerName = o.CustomerName,
                    PhoneNumber = o.PhoneNumber,
                    ShippingAddress = o.ShippingAddress,
                    OrderStatus = o.OrderStatus,
                    StatusDisplayName = displayStatus,
                    StatusBadgeClass = badgeClass,
                    StatusUpdatedAt = o.StatusUpdatedAt ?? o.UpdatedAt,
                    TotalAmount = o.TotalAmount,
                    DiscountAmount = o.DiscountAmount,
                    DeliveryFee = o.DeliveryFee,
                    FinalAmount = o.FinalAmount,
                    DepositAmount = o.DepositAmount,
                    DepositDate = o.DepositDate,
                    PaymentAmount = o.PaymentAmount > 0 ? o.PaymentAmount : (o.OrderStatus == "Completed" ? o.FinalAmount : 0),
                    PaymentDate = o.PaymentDate ?? (o.OrderStatus == "Completed" ? moneyDate : null),
                    MoneyReceivedDate = moneyDate,
                    MoneyReceivedAmount = (o.OrderStatus == "Completed") ? o.FinalAmount : 0,
                    RevenueRecognized = revenue,
                    DeliveryType = o.DeliveryType ?? (o.ShippingAddress.Contains("Hồ Chí Minh") ? "Internal" : "ThirdParty"),
                    DeliveryTypeLabel = (o.DeliveryType == "ThirdParty" || (!string.IsNullOrEmpty(o.ShippingAddress) && !o.ShippingAddress.Contains("Hồ Chí Minh"))) ? "Chuyển phát GHN (Tỉnh)" : "Nội bộ TP.HCM",
                    PaymentMethod = o.PaymentMethod,
                    StatusGroup = statusGroup
                };

                invoiceList.Add(inv);

                // Thống kê đối soát chia nhóm:
                report.TotalGrossAmount += o.FinalAmount;
                if (o.OrderStatus == "Completed" && isMoneyReceivedToday)
                {
                    report.TotalCompletedRevenue += o.FinalAmount;
                    report.CompletedCount++;
                }
                else if (o.OrderStatus == "Cancelled")
                {
                    report.TotalCancelledAmount += o.FinalAmount;
                    report.CancelledCount++;
                }
                else if (o.DepositAmount > 0)
                {
                    report.TotalDepositCollected += o.DepositAmount;
                    report.DepositCount++;
                }
                else
                {
                    report.TotalPendingAmount += o.FinalAmount;
                    report.ProcessingCount++;
                }
            }

            report.TotalOrderCount = invoiceList.Count;

            // Lọc theo nhóm tình trạng nếu có yêu cầu
            if (!string.IsNullOrWhiteSpace(statusGroupFilter) && statusGroupFilter != "All")
            {
                invoiceList = invoiceList.Where(i => i.StatusGroup.Equals(statusGroupFilter, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            report.Invoices = invoiceList;
            return report;
        }
    }
}
