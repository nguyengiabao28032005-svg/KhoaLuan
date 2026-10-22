using System;
using System.ComponentModel.DataAnnotations;

namespace BeautyShop.Models
{
    public class StockReceipt
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string ReceiptCode { get; set; } = string.Empty;

        [Required]
        public int VariantId { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Số lượng nhập phải lớn hơn 0.")]
        public int Quantity { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Giá nhập không được âm.")]
        public decimal ImportPrice { get; set; } = 0;

        /// <summary>
        /// Kho nhập hàng
        /// </summary>
        [Required]
        public int StoreId { get; set; } = 1;

        /// <summary>
        /// Loại phiếu nhập: "Domestic" (Nhập trong nước) hoặc "Import" (Nhập khẩu)
        /// Phiếu nhập khẩu chỉ được nhập vào kho thuộc tỉnh có cảng đang mở (AllowImport = true).
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string ReceiptType { get; set; } = "Domestic";

        /// <summary>
        /// Cảng biển hoặc cảng hàng không quốc tế thực hiện thông quan nhập khẩu (bắt buộc đối với phiếu Import, để trống đối với Domestic)
        /// </summary>
        public int? ImportPortId { get; set; }

        /// <summary>
        /// Số vận đơn (Bill of Lading) hoặc số tờ khai hải quan thông quan hàng nhập khẩu
        /// </summary>
        [MaxLength(100)]
        public string? CustomsDeclarationNumber { get; set; }

        [MaxLength(255)]
        public string? Supplier { get; set; }

        public DateTime ImportDate { get; set; } = DateTime.Now;

        public int? CreatedByUserId { get; set; }

        [MaxLength(100)]
        public string? CreatedByName { get; set; }

        [MaxLength(500)]
        public string? Note { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public ProductVariant? Variant { get; set; }

        public Store? Store { get; set; }

        public ImportPort? ImportPort { get; set; }

        public User? CreatedByUser { get; set; }
    }
}
