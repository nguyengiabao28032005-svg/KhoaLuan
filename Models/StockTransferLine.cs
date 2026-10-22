using System.ComponentModel.DataAnnotations;

namespace BeautyShop.Models
{
    /// <summary>
    /// Chi tiết từng mặt hàng và số lượng trong một phiếu chuyển kho
    /// </summary>
    public class StockTransferLine
    {
        public int Id { get; set; }

        [Required]
        public int StockTransferId { get; set; }

        [Required]
        public int VariantId { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Số lượng chuyển phải lớn hơn 0.")]
        public int Quantity { get; set; }

        // Navigation properties
        public StockTransfer? StockTransfer { get; set; }
        public ProductVariant? Variant { get; set; }
    }
}
