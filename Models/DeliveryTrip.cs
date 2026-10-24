using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BeautyShop.Models
{
    /// <summary>
    /// Chuyến giao hàng (DeliveryTrip):
    /// Gom các đơn hàng cùng ngày, cùng ca, cùng cụm lân cận.
    /// Quản lý vòng đời chuyến và đối soát tiền COD bàn giao cho thu ngân.
    /// </summary>
    public class DeliveryTrip
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string TripCode { get; set; } = string.Empty; // TRIP-yyyyMMdd-xxxx

        public DateTime TripDate { get; set; } // Ngày giao

        [Required]
        [StringLength(30)]
        public string Shift { get; set; } = "Morning"; // Morning (Sáng), Afternoon (Chiều), Evening (Tối)

        [Required]
        [StringLength(100)]
        public string ClusterCode { get; set; } = string.Empty; // Cụm khu vực chính (hoặc cụm ghép kề nhau)

        public int? StoreId { get; set; } = 1; // Kho xuất phát của chuyến giao (phục vụ lọc phạm vi cho thu ngân/nhân viên kho)

        [ForeignKey("StoreId")]
        public Store? Store { get; set; }

        public int? ShipperUserId { get; set; }

        [ForeignKey("ShipperUserId")]
        public User? ShipperUser { get; set; }

        [Required]
        [StringLength(30)]
        public string Status { get; set; } = "Draft"; // Draft, Assigned, InTransit, Completed, Cancelled

        public int TotalOrders { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalCodExpected { get; set; } = 0; // Đơn không COD có CodExpected = 0

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalCodCollected { get; set; } = 0; // Thực thu khi giao thành công

        // Bàn giao tiền COD chuyến cho thu ngân
        public int? CashierUserId { get; set; }

        [ForeignKey("CashierUserId")]
        public User? CashierUser { get; set; }

        [StringLength(30)]
        public string SettlementStatus { get; set; } = "Unsettled"; // Unsettled (Chưa nộp), Settled (Đã nộp cho thu ngân)

        public DateTime? SettledAt { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? SettlementReceivedAmount { get; set; } // Số tiền thu ngân thực nhận

        [Column(TypeName = "decimal(18,2)")]
        public decimal? SettlementDifference { get; set; } // Chênh lệch (Received - Collected)

        [StringLength(255)]
        public string? SettlementNotes { get; set; }

        public int? CreatedByUserId { get; set; }

        [ForeignKey("CreatedByUserId")]
        public User? CreatedByUser { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? CompletedAt { get; set; }

        public ICollection<DeliveryTripStop> Stops { get; set; } = new List<DeliveryTripStop>();
    }
}
