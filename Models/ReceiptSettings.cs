namespace BeautyShop.Models
{
    public class ReceiptSettings
    {
        /// <summary>
        /// Ngưỡng tồn kho thấp để tô màu nổi bật trên bảng chọn sản phẩm (mặc định 10)
        /// </summary>
        public int LowStockThreshold { get; set; } = 10;

        /// <summary>
        /// Giới hạn số ngày lùi tối đa cho phép khi lập phiếu nhập (mặc định 30 ngày)
        /// </summary>
        public int MaxPastDaysAllowed { get; set; } = 30;

        /// <summary>
        /// Ngưỡng chênh lệch đơn giá nhập so với lần gần nhất để cảnh báo (mặc định 20%)
        /// </summary>
        public decimal PriceDiscrepancyThresholdPercent { get; set; } = 20.0m;

        /// <summary>
        /// Ngưỡng biên lợi nhuận tối thiểu để cảnh báo sau khi nhập (mặc định 10%)
        /// </summary>
        public decimal MinimumProfitMarginPercent { get; set; } = 10.0m;
    }
}
