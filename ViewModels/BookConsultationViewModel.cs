using System;
using System.ComponentModel.DataAnnotations;

namespace BeautyShop.ViewModels
{
    public class BookConsultationViewModel
    {
        [Required(ErrorMessage = "Vui lòng chọn loại hình tư vấn.")]
        [Display(Name = "Loại tư vấn")]
        public string ConsultationType { get; set; } = "Tư vấn chăm sóc da";

        [Required(ErrorMessage = "Vui lòng chọn ngày hẹn.")]
        [DataType(DataType.Date)]
        [Display(Name = "Ngày hẹn")]
        public DateTime AppointmentDate { get; set; } = DateTime.Today.AddDays(1);

        [Required(ErrorMessage = "Vui lòng chọn giờ bắt đầu.")]
        [Display(Name = "Giờ bắt đầu")]
        public string StartTime { get; set; } = "09:00";

        [Required(ErrorMessage = "Vui lòng chọn giờ kết thúc.")]
        [Display(Name = "Giờ kết thúc")]
        public string EndTime { get; set; } = "09:45";

        [Display(Name = "Ghi chú / Tình trạng da / Nhu cầu tư vấn")]
        [StringLength(1000, ErrorMessage = "Ghi chú không được vượt quá 1000 ký tự.")]
        public string? CustomerNote { get; set; }
    }
}
