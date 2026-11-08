using System;
using System.ComponentModel.DataAnnotations;

namespace BeautyShop.Models
{
    /// <summary>
    /// Thực thể Tuyến chuyển kho hợp lệ trong hệ thống
    /// Xác định kho gửi, kho nhận và kho trung chuyển (nếu có yêu cầu qua Hub)
    /// </summary>
    public class TransferRoute
    {
        public int Id { get; set; }

        [Required]
        public int FromStoreId { get; set; }

        [Required]
        public int ToStoreId { get; set; }

        /// <summary>
        /// Kho trung chuyển (Hub). Nếu null: tuyến được phép đi thẳng trực tiếp.
        /// Nếu khác null: tuyến liên miền bắt buộc phải dừng chân tại Hub này trước khi đi tiếp.
        /// </summary>
        public int? TransitStoreId { get; set; }

        /// <summary>
        /// Khoảng cách ước tính (km)
        /// </summary>
        public decimal? DistanceKm { get; set; }

        /// <summary>
        /// Thời gian vận chuyển ước tính (giờ)
        /// </summary>
        public decimal? EstimatedHours { get; set; }

        /// <summary>
        /// Tuyến đang hoạt động hay tạm dừng
        /// </summary>
        public bool IsActive { get; set; } = true;

        [MaxLength(255)]
        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation properties
        public Store? FromStore { get; set; }
        public Store? ToStore { get; set; }
        public Store? TransitStore { get; set; }
    }
}
