using System;
using System.ComponentModel.DataAnnotations;

namespace BeautyShop.Models
{
    /// <summary>
    /// Mặt hàng chi tiết trong phiếu nhập kho.
    /// Cho phép một phiếu nhập chứa nhiều sản phẩm (son, phấn, chì kẻ...).
    /// </summary>
    public class StockReceiptItem
    {
        public int Id { get; set; }

        [Required]
        public int StockReceiptId { get; set; }

        [Required]
        public int VariantId { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Số lượng nhập phải lớn hơn 0.")]
        public int Quantity { get; set; }

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Đơn giá nhập phải lớn hơn 0.")]
        public decimal ImportPrice { get; set; }

        public decimal TotalPrice { get; set; }

        /// <summary>
        /// Giá bán lẻ trước khi nhập
        /// </summary>
        public decimal? OldRetailPrice { get; set; }

        /// <summary>
        /// Giá bán lẻ mới sau khi người dùng định lại theo giá nhập (Yêu cầu 10)
        /// </summary>
        public decimal? NewRetailPrice { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation
        public StockReceipt? StockReceipt { get; set; }
        public ProductVariant? Variant { get; set; }
    }
}
