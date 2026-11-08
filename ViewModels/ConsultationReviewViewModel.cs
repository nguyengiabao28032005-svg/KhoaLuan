using System.ComponentModel.DataAnnotations;

namespace BeautyShop.ViewModels
{
    public class ConsultationReviewViewModel
    {
        [Required]
        public int ConsultationId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn số sao đánh giá.")]
        [Range(1, 5, ErrorMessage = "Đánh giá từ 1 đến 5 sao.")]
        public int Rating { get; set; } = 5;

        [StringLength(1000, ErrorMessage = "Nhận xét không được vượt quá 1000 ký tự.")]
        [Display(Name = "Nhận xét của bạn")]
        public string? Comment { get; set; }
    }
}
