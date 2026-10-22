using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace BeautyShop.ViewModels
{
    public class ConsultationResultViewModel
    {
        public int ConsultationId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn trạng thái buổi tư vấn.")]
        public string Status { get; set; } = "Đang tư vấn";

        [Display(Name = "Ghi chú nội bộ nhân viên")]
        public string? EmployeeNote { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập kết quả và nội dung tư vấn.")]
        [Display(Name = "Kết quả tư vấn & Lời khuyên cho khách hàng")]
        public string ConsultationResult { get; set; } = string.Empty;

        // Danh sách ID sản phẩm được đề xuất
        public List<int> SelectedProductIds { get; set; } = new();

        // Ghi chú cách dùng cho từng sản phẩm (Key: ProductId, Value: Note)
        public Dictionary<int, string> ProductNotes { get; set; } = new();
    }
}
