using System;

namespace BeautyShop.Services
{
    /// <summary>
    /// Cấu hình và quy tắc nghiệp vụ cho Chức năng Giao hàng Tối ưu (Bước 5):
    /// - Giờ chốt ca mặc định: 10:00 (cấu hình được).
    ///   Lý do nghiệp vụ: Cần thời gian từ khi đặt hàng để nhân viên kho xác nhận đơn, xuất kho,
    ///   kiểm đếm và đóng gói bao bì hoàn chỉnh trước khi shipper nhận hàng đi ca Chiều.
    /// - Số đơn tối đa mỗi chuyến (N): mặc định 8 (cấu hình được).
    /// - Số lần giao lại tối đa: mặc định 2 (vượt quá thì chuyển sang Chờ xử lý / Trả hàng).
    /// </summary>
    public static class DeliveryConfig
    {
        public static TimeSpan CutoffTime { get; set; } = new TimeSpan(10, 0, 0); // 10:00 sáng
        public static int MaxOrdersPerTrip { get; set; } = 8; // N mặc định = 8 đơn/chuyến
        public static int MaxFailedAttempts { get; set; } = 2; // Tối đa 2 lần giao lại thất bại

        /// <summary>
        /// Tính toán Ngày giao dự kiến và Ca giao mặc định theo thời gian đặt hàng:
        /// - Nếu đặt trước 10:00 -> Giao ca Chiều (13:00 - 17:00) cùng ngày.
        /// - Nếu đặt từ 10:00 trở đi -> Giao ca Sáng (08:00 - 12:00) ngày hôm sau.
        /// </summary>
        public static (DateTime ExpectedDate, string Shift, string ShiftDisplayName, string Reason) GetDefaultDeliverySchedule(DateTime orderTime)
        {
            if (orderTime.TimeOfDay < CutoffTime)
            {
                return (
                    orderTime.Date,
                    "Afternoon",
                    "Ca Chiều (13:00 - 17:00)",
                    $"Đặt trước giờ chốt {CutoffTime:hh\\:mm}: Kho kịp xuất và đóng gói đơn để giao ngay trong ca Chiều hôm nay."
                );
            }
            else
            {
                return (
                    orderTime.Date.AddDays(1),
                    "Morning",
                    "Ca Sáng (08:00 - 12:00)",
                    $"Đặt sau giờ chốt {CutoffTime:hh\\:mm}: Đơn được xử lý đóng gói vào cuối ngày và giao vào ca Sáng ngày mai."
                );
            }
        }

        public static string GetShiftDisplayName(string? shift)
        {
            return shift switch
            {
                "Morning" => "Ca Sáng (08:00 - 12:00)",
                "Afternoon" => "Ca Chiều (13:00 - 17:00)",
                "Evening" => "Ca Tối (18:00 - 21:00)",
                _ => "Ca Sáng (08:00 - 12:00)"
            };
        }
    }
}
