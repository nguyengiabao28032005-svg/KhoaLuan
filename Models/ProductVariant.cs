using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace BeautyShop.Models
{
    public class ProductVariant
    {
        public int Id { get; set; }

        public int ProductId { get; set; }

        [Required]
        [MaxLength(100)]
        public string SKU { get; set; }

        [MaxLength(100)]
        public string? Barcode { get; set; }

        [MaxLength(20)]
        public string? ColorCode { get; set; }

        [MaxLength(100)]
        public string? ColorName { get; set; }

        [MaxLength(50)]
        public string? Volume { get; set; }

        // Giá gốc tham chiếu từ phiếu nhập gần nhất (null nếu chưa nhập hàng)
        public decimal? OriginalPrice { get; set; }

        // Giá bán lẻ (null = chưa thiết lập giá bán, không được hiển thị để mua)
        public decimal? RetailPrice { get; set; }

        // Giá bán sỉ (null = chưa thiết lập)
        public decimal? WholesalePrice { get; set; }

        // Kiểm tra biến thể đã thiết lập giá bán hay chưa
        public bool HasPrice => RetailPrice.HasValue && RetailPrice.Value > 0;

        public string? ImageUrl { get; set; }

        public Product? Product { get; set; }
        public ICollection<Inventory>? Inventories { get; set; }
        public ICollection<CartItem>? CartItems { get; set; }
    }
}