using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BeautyShop.Models
{
    public class ConsultationReview
    {
        public int Id { get; set; }

        [Required]
        public int ConsultationId { get; set; }

        [Required]
        public int UserId { get; set; }

        [Range(1, 5, ErrorMessage = "Đánh giá từ 1 đến 5 sao.")]
        public int Rating { get; set; }

        [MaxLength(1000)]
        public string? Comment { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ForeignKey("ConsultationId")]
        public Consultation? Consultation { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }
    }
}
