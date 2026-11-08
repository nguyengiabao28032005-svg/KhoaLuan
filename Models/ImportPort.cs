using System;
using System.ComponentModel.DataAnnotations;

namespace BeautyShop.Models
{
    /// <summary>
    /// Thực thể danh mục cảng nhập khẩu (cảng biển hoặc cảng hàng không quốc tế)
    /// Dùng để kiểm tra ràng buộc: Hàng nhập khẩu chỉ được nhập về kho thuộc tỉnh có cảng đang mở nhập khẩu.
    /// </summary>
    public class ImportPort
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mã cảng.")]
        [MaxLength(50)]
        public string PortCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập tên cảng.")]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Loại cảng: "Sea" (Cảng biển) hoặc "Air" (Cảng hàng không quốc tế)
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string PortType { get; set; } = "Sea";

        /// <summary>
        /// Tên tỉnh / thành phố trực thuộc trung ương
        /// </summary>
        [Required(ErrorMessage = "Vui lòng nhập tỉnh/thành phố đặt cảng.")]
        [MaxLength(100)]
        public string ProvinceName { get; set; } = string.Empty;

        /// <summary>
        /// Mã định danh tỉnh/thành (chuẩn Tổng cục Thống kê, ví dụ: 79 = TP.HCM, 01 = Hà Nội, 31 = Hải Phòng...)
        /// </summary>
        [MaxLength(50)]
        public string? ProvinceCode { get; set; }

        /// <summary>
        /// Cờ cho phép thông quan nhập khẩu hàng hóa tại cảng này
        /// </summary>
        public bool AllowImport { get; set; } = true;

        /// <summary>
        /// Nguồn tham khảo quy chuẩn thực tế hoặc ghi chú "Cần kiểm chứng"
        /// </summary>
        [MaxLength(500)]
        public string? SourceReference { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }
    }
}
