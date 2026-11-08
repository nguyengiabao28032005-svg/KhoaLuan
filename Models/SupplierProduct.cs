using System;
using System.ComponentModel.DataAnnotations;

namespace BeautyShop.Models
{
    /// <summary>
    /// Bảng trung gian Cung Ứng giữa Nhà Cung Cấp và Biến thể Sản Phẩm (Variant).
    /// Cho biết mỗi nhà cung cấp phân phối những biến thể nào,
    /// kèm giá cung ứng tham chiếu theo từng biến thể và ngày cập nhật giá gần nhất.
    /// </summary>
    public class SupplierProduct
    {
        public int Id { get; set; }

        [Required]
        public int SupplierId { get; set; }

        [Required]
        public int VariantId { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Giá cung ứng không được âm.")]
        [Display(Name = "Giá cung ứng tham chiếu")]
        public decimal? SuppliedPrice { get; set; }

        [MaxLength(255)]
        [Display(Name = "Ghi chú")]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Display(Name = "Ngày cập nhật giá gần nhất")]
        public DateTime? UpdatedAt { get; set; }

        // Navigation
        public Supplier? Supplier { get; set; }
        public ProductVariant? Variant { get; set; }
    }
}
