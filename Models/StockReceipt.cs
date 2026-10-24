using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace BeautyShop.Models
{
    public class StockReceipt
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string ReceiptCode { get; set; } = string.Empty;

        /// <summary>
        /// Biến thể chính hoặc đầu tiên (duy trì tương thích ngược với dữ liệu và tests cũ)
        /// </summary>
        public int VariantId { get; set; }

        /// <summary>
        /// Tổng số lượng nhập (hoặc số lượng của biến thể nếu phiếu cũ)
        /// </summary>
        [Range(0, int.MaxValue, ErrorMessage = "Số lượng nhập không được âm.")]
        public int Quantity { get; set; }

        /// <summary>
        /// Đơn giá nhập của sản phẩm đơn hoặc giá tham chiếu
        /// </summary>
        [Range(0, double.MaxValue, ErrorMessage = "Giá nhập không được âm.")]
        public decimal ImportPrice { get; set; } = 0;

        /// <summary>
        /// Kho nhập hàng
        /// </summary>
        [Required]
        public int StoreId { get; set; } = 1;

        /// <summary>
        /// Loại phiếu nhập: "Domestic" (Nhập trong nước) hoặc "Import" (Nhập khẩu)
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string ReceiptType { get; set; } = "Domestic";

        /// <summary>
        /// Cảng biển hoặc cảng hàng không quốc tế thực hiện thông quan nhập khẩu
        /// </summary>
        public int? ImportPortId { get; set; }

        /// <summary>
        /// Số vận đơn (Bill of Lading) hoặc số tờ khai hải quan thông quan hàng nhập khẩu
        /// </summary>
        [MaxLength(100)]
        public string? CustomsDeclarationNumber { get; set; }

        /// <summary>
        /// Mã định danh nhà cung cấp trong bảng Suppliers
        /// </summary>
        public int? SupplierId { get; set; }

        /// <summary>
        /// Tên nhà cung cấp (lưu chuỗi để độc lập truy vấn và đối soát chứng từ)
        /// </summary>
        [MaxLength(255)]
        public string? Supplier { get; set; }

        /// <summary>
        /// Số hóa đơn mua hàng / Hóa đơn tài chính VAT
        /// </summary>
        [MaxLength(100)]
        public string? InvoiceNumber { get; set; }

        /// <summary>
        /// Quốc gia xuất xứ hàng hóa (Việt Nam, Hàn Quốc, Pháp, Nhật Bản, Mỹ...)
        /// </summary>
        [MaxLength(100)]
        public string OriginCountry { get; set; } = "Việt Nam";

        /// <summary>
        /// Trạng thái thanh toán: "Paid" (Đã thanh toán), "Unpaid" (Chưa thanh toán / Ghi nhận công nợ), "Partial" (Thanh toán 1 phần)
        /// </summary>
        [MaxLength(50)]
        public string PaymentStatus { get; set; } = "Paid";

        /// <summary>
        /// Tổng giá trị tiền hàng của phiếu nhập
        /// </summary>
        public decimal TotalAmount { get; set; } = 0;

        /// <summary>
        /// Số tiền đã thanh toán cho nhà cung cấp
        /// </summary>
        public decimal PaidAmount { get; set; } = 0;

        /// <summary>
        /// Số tiền công nợ còn phải trả nhà cung cấp (TotalAmount - PaidAmount)
        /// </summary>
        public decimal DebtAmount { get; set; } = 0;

        public DateTime ImportDate { get; set; } = DateTime.Now;

        public int? CreatedByUserId { get; set; }

        [MaxLength(100)]
        public string? CreatedByName { get; set; }

        [MaxLength(500)]
        public string? Note { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation properties
        public ProductVariant? Variant { get; set; }
        public Store? Store { get; set; }
        public ImportPort? ImportPort { get; set; }
        public User? CreatedByUser { get; set; }
        public Supplier? SupplierRef { get; set; }
        public ICollection<StockReceiptItem> Items { get; set; } = new List<StockReceiptItem>();
    }
}
