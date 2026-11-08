using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BeautyShop.Models
{
    /// <summary>
    /// Điểm dừng giao hàng trong chuyến (DeliveryTripStop):
    /// Lưu lịch sử chi tiết từng lần giao hàng của đơn.
    /// Có cờ IsActive và Unique Filtered Index để đảm bảo 1 đơn chỉ có tối đa 1 điểm dừng đang hoạt động.
    /// </summary>
    public class DeliveryTripStop
    {
        public int Id { get; set; }

        public int DeliveryTripId { get; set; }

        [ForeignKey("DeliveryTripId")]
        public DeliveryTrip? DeliveryTrip { get; set; }

        public int OrderId { get; set; }

        [ForeignKey("OrderId")]
        public Order? Order { get; set; }

        public int StopOrder { get; set; } = 1; // Thứ tự điểm dừng trong chuyến (1, 2, 3...)

        [Required]
        [StringLength(30)]
        public string StopStatus { get; set; } = "Pending"; // Pending, Delivering, Success, Failed, Removed

        [Column(TypeName = "decimal(18,2)")]
        public decimal CodExpectedAmount { get; set; } = 0; // Đơn không COD có giá trị là 0

        [Column(TypeName = "decimal(18,2)")]
        public decimal CodCollectedAmount { get; set; } = 0; // Số tiền thực thu khi giao thành công

        [StringLength(100)]
        public string? FailureReason { get; set; } // Bắt buộc khi StopStatus == "Failed" (Khách hẹn lại, Không nghe máy, Sai địa chỉ...)

        [StringLength(255)]
        public string? Notes { get; set; }

        public bool IsActive { get; set; } = true; // Ràng buộc: chỉ 1 Stop có IsActive = true trên 1 đơn hàng tại 1 thời điểm

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? CompletedAt { get; set; }
    }
}
