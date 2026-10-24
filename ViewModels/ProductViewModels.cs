using System.ComponentModel.DataAnnotations;

namespace BeautyShop.ViewModels
{
    /// <summary>
    /// ViewModel dùng cho form tạo mới sản phẩm (Tách biệt hoàn toàn khỏi giá bán và tồn kho)
    /// </summary>
    public class CreateProductViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập tên sản phẩm.")]
        [StringLength(255, ErrorMessage = "Tên sản phẩm không được vượt quá 255 ký tự.")]
        [Display(Name = "Tên sản phẩm")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng chọn danh mục sản phẩm.")]
        [Display(Name = "Danh mục")]
        public int CategoryId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn thương hiệu sản phẩm.")]
        [Display(Name = "Thương hiệu")]
        public int BrandId { get; set; }

        [Display(Name = "Ảnh đại diện (URL)")]
        [StringLength(500, ErrorMessage = "Đường dẫn ảnh không được vượt quá 500 ký tự.")]
        public string? ThumbnailUrl { get; set; }

        [Display(Name = "Dung tích")]
        [StringLength(50, ErrorMessage = "Dung tích không được vượt quá 50 ký tự.")]
        public string? Volume { get; set; }

        [Display(Name = "Màu sắc")]
        [StringLength(50, ErrorMessage = "Tên màu sắc không được vượt quá 50 ký tự.")]
        public string? ColorName { get; set; }

        [Display(Name = "Mô tả sản phẩm")]
        public string? Description { get; set; }

        [Display(Name = "Thành phần")]
        public string? Ingredients { get; set; }

        [Display(Name = "Hướng dẫn sử dụng")]
        public string? HowToUse { get; set; }

        [Display(Name = "Kích hoạt")]
        public bool IsActive { get; set; } = true;
    }

    /// <summary>
    /// ViewModel thêm biến thể mới cho sản phẩm (Không có giá và tồn kho)
    /// </summary>
    public class CreateVariantViewModel
    {
        [Required(ErrorMessage = "Thiếu mã sản phẩm cha.")]
        public int ProductId { get; set; }

        [Display(Name = "Mã SKU")]
        [StringLength(100, ErrorMessage = "Mã SKU không được vượt quá 100 ký tự.")]
        public string? SKU { get; set; }

        [Display(Name = "Dung tích")]
        [StringLength(50, ErrorMessage = "Dung tích không được vượt quá 50 ký tự.")]
        public string? Volume { get; set; }

        [Display(Name = "Màu sắc")]
        [StringLength(50, ErrorMessage = "Tên màu không được vượt quá 50 ký tự.")]
        public string? ColorName { get; set; }
    }

    /// <summary>
    /// ViewModel thiết lập giá bán lẻ và giá bán sỉ cho biến thể
    /// </summary>
    public class SetPricingViewModel
    {
        [Required(ErrorMessage = "Vui lòng chọn biến thể cần cài đặt giá.")]
        public int VariantId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập giá bán lẻ.")]
        [Range(1000, 1000000000, ErrorMessage = "Giá bán lẻ phải từ 1.000đ trở lên.")]
        [Display(Name = "Giá bán lẻ (VNĐ)")]
        public decimal RetailPrice { get; set; }

        [Range(0, 1000000000, ErrorMessage = "Giá bán sỉ không được âm.")]
        [Display(Name = "Giá bán sỉ (VNĐ)")]
        public decimal? WholesalePrice { get; set; }
    }

    /// <summary>
    /// ViewModel hiển thị một dòng biến thể trên màn hình Thiết lập giá bán
    /// </summary>
    public class VariantPricingRowViewModel
    {
        public int VariantId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? BrandName { get; set; }
        public string? CategoryName { get; set; }
        public string SKU { get; set; } = string.Empty;
        public string? Volume { get; set; }
        public string? ColorName { get; set; }
        public string? ThumbnailUrl { get; set; }
        public int StockQuantity { get; set; }
        public decimal? CostPrice { get; set; } // Giá vốn nhập lần cuối từ phiếu nhập
        public decimal? RetailPrice { get; set; } // Giá bán lẻ hiện tại
        public decimal? WholesalePrice { get; set; } // Giá bán sỉ hiện tại
        public bool HasPrice => RetailPrice.HasValue && RetailPrice.Value > 0;
        public bool CanSetPrice => StockQuantity > 0; // Đã có hàng trong kho
    }
}
