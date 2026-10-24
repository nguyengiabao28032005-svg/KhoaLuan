using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BeautyShop.Models
{
    /// <summary>
    /// Bảng đối soát tiền mặt cuối ngày (DailyReconciliation):
    /// Thu ngân tổng hợp tiền COD các chuyến trong ngày, nộp cho Chủ cửa hàng (Admin).
    /// Khóa sổ sau khi xác nhận, không cho chỉnh sửa.
    /// </summary>
    public class DailyReconciliation
    {
        public int Id { get; set; }

        public DateTime ReconciliationDate { get; set; } // Ngày chốt sổ

        [Required]
        [StringLength(50)]
        public string Code { get; set; } = string.Empty; // REC-yyyyMMdd-S{StoreId}

        public int? StoreId { get; set; } = 1; // Kho / Quỹ thu ngân thực hiện đối soát

        [ForeignKey("StoreId")]
        public Store? Store { get; set; }

        public int CashierUserId { get; set; }

        [ForeignKey("CashierUserId")]
        public User? CashierUser { get; set; }

        public int? AdminApprovedUserId { get; set; }

        [ForeignKey("AdminApprovedUserId")]
        public User? AdminApprovedUser { get; set; }

        public int TotalTripsSettled { get; set; } = 0;

        public int TotalOrdersDelivered { get; set; } = 0;

        public int TotalOrdersFailed { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalCodExpected { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalCodReceived { get; set; } = 0; // Tổng tiền thu ngân đã nhận từ các shipper

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalCashSubmitted { get; set; } = 0; // Số tiền thu ngân nộp cho Admin

        [Column(TypeName = "decimal(18,2)")]
        public decimal DifferenceAmount { get; set; } = 0; // TotalCashSubmitted - TotalCodReceived

        [StringLength(500)]
        public string? DifferenceReason { get; set; } // Ghi chú giải trình chênh lệch nếu có

        public string? TopSellingProductsJson { get; set; } // Thống kê sản phẩm bán chạy trong ngày

        public string? SlowSellingProductsJson { get; set; } // Thống kê sản phẩm bán chậm trong ngày

        [Required]
        [StringLength(30)]
        public string Status { get; set; } = "Draft"; // Draft (Chờ Admin duyệt), Approved (Đã khóa sổ)

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? ApprovedAt { get; set; }
    }
}
