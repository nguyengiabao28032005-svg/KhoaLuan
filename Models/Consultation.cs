using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BeautyShop.Models
{
    public class Consultation
    {
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        public int? EmployeeId { get; set; }

        [Required]
        [MaxLength(100)]
        public string ConsultationType { get; set; } = string.Empty; // "Tư vấn sản phẩm", "Tư vấn chăm sóc da"

        [Required]
        public DateTime AppointmentDate { get; set; }

        [Required]
        public TimeSpan StartTime { get; set; }

        [Required]
        public TimeSpan EndTime { get; set; }

        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Chờ xác nhận";
        // "Chờ xác nhận", "Đã xác nhận", "Đã phân công", "Đang tư vấn", "Đã hoàn thành", "Đã hủy"

        [MaxLength(1000)]
        public string? CustomerNote { get; set; }

        [MaxLength(1000)]
        public string? EmployeeNote { get; set; }

        public string? ConsultationResult { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        [ForeignKey("UserId")]
        public User? User { get; set; }

        [ForeignKey("EmployeeId")]
        public User? Employee { get; set; }

        public ConsultationReview? Review { get; set; }

        public ICollection<ConsultationProduct>? ConsultationProducts { get; set; }
    }
}
