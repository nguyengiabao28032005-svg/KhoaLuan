using System;
using System.ComponentModel.DataAnnotations;

namespace BeautyShop.Models
{
    /// <summary>
    /// Thực thể Khu vực giao hàng (DeliveryZone).
    /// Tên gọi chuẩn: "Tên khu vực", không gọi là Quận/Huyện do cấu trúc hành chính thực tế thay đổi.
    /// Cho phép cấu hình phân cụm lân cận (ClusterCode) để gom chuyến tối ưu, không hard-code.
    /// </summary>
    public class DeliveryZone
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "Tên khu vực")]
        public string ZoneName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string ProvinceName { get; set; } = string.Empty;

        [StringLength(20)]
        public string? ProvinceCode { get; set; }

        [Required]
        [StringLength(50)]
        public string ClusterCode { get; set; } = string.Empty; // Mã cụm: HCM_NW, HCM_CEN, HCM_EST, HCM_SOU...

        [StringLength(100)]
        public string? ClusterName { get; set; } // Tên hiển thị cụm: Cụm Tây Bắc, Cụm Trung tâm...

        public int SortOrder { get; set; } = 0; // Thứ tự di chuyển tối ưu giữa các khu vực trong cụm

        [StringLength(100)]
        public string? AdjacentClusterCodes { get; set; } // Các cụm kề nhau, cách nhau dấu phẩy (dùng ghép chuyến khi tổng đơn < N)

        [StringLength(255)]
        public string? Notes { get; set; } // Ghi chú (đánh dấu "Cần kiểm chứng" cho tên đơn vị hành chính mới/sáp nhập)

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
