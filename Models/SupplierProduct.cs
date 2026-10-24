using System;
using System.ComponentModel.DataAnnotations;

namespace BeautyShop.Models
{
    /// <summary>
    /// Bảng trung gian Cung Ứng giữa Nhà Cung Cấp và Sản Phẩm.
    /// Cho biết mỗi nhà cung cấp phân phối những sản phẩm nào,
    /// kèm giá cung ứng tham chiếu để đối chiếu và gợi ý nơi nhập.
    /// </summary>
    public class SupplierProduct
    {
        public int Id { get; set; }

        [Required]
        public int SupplierId { get; set; }

        [Required]
        public int ProductId { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Giá cung ứng không được âm.")]
        [Display(Name = "Giá cung ứng tham chiếu")]
        public decimal? SuppliedPrice { get; set; }

        [MaxLength(255)]
        [Display(Name = "Ghi chú")]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation
        public Supplier? Supplier { get; set; }
        public Product? Product { get; set; }
    }
}
