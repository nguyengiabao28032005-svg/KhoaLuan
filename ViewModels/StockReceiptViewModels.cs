using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using BeautyShop.Models;

namespace BeautyShop.ViewModels
{
    /// <summary>
    /// ViewModel tạo phiếu nhập kho nhiều sản phẩm (Yêu cầu 1, 2, 3, 4, 5, 6, 7, 10)
    /// </summary>
    public class CreateStockReceiptViewModel
    {
        [Required(ErrorMessage = "Vui lòng chọn kho tiếp nhận hàng.")]
        [Display(Name = "Kho lưu trữ")]
        public int StoreId { get; set; } = 1;

        [Required]
        [Display(Name = "Hình thức nhập")]
        public string ReceiptType { get; set; } = "Domestic"; // "Domestic" hoặc "Import"

        [Display(Name = "Cảng thông quan")]
        public int? ImportPortId { get; set; }

        [MaxLength(100)]
        [Display(Name = "Số tờ khai hải quan / Số vận đơn")]
        public string? CustomsDeclarationNumber { get; set; }

        [Display(Name = "Nhà cung cấp")]
        public int? SupplierId { get; set; }

        [MaxLength(255)]
        [Display(Name = "Tên nhà cung cấp")]
        public string? SupplierName { get; set; }

        [MaxLength(100)]
        [Display(Name = "Số hóa đơn tài chính")]
        public string? InvoiceNumber { get; set; }

        [MaxLength(100)]
        [Display(Name = "Xuất xứ hàng hóa")]
        public string OriginCountry { get; set; } = "Việt Nam";

        [Required]
        [Display(Name = "Trạng thái thanh toán")]
        public string PaymentStatus { get; set; } = "Paid"; // "Paid", "Unpaid", "Partial"

        [Range(0, double.MaxValue, ErrorMessage = "Số tiền thanh toán không hợp lệ.")]
        [Display(Name = "Số tiền đã thanh toán")]
        public decimal PaidAmount { get; set; } = 0;

        [Display(Name = "Ngày nhập hàng")]
        public DateTime ImportDate { get; set; } = DateTime.Now;

        [MaxLength(500)]
        [Display(Name = "Ghi chú")]
        public string? Note { get; set; }

        /// <summary>
        /// Danh sách các mặt hàng nhập (Bắt buộc >= 2 sản phẩm theo Yêu cầu 1)
        /// </summary>
        public List<ReceiptItemInputModel> Items { get; set; } = new();
    }

    /// <summary>
    /// Dòng sản phẩm chi tiết trong phiếu nhập
    /// </summary>
    public class ReceiptItemInputModel
    {
        [Required(ErrorMessage = "Vui lòng chọn biến thể sản phẩm.")]
        public int VariantId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số lượng.")]
        [Range(1, int.MaxValue, ErrorMessage = "Số lượng phải lớn hơn 0.")]
        public int Quantity { get; set; } = 1;

        [Required(ErrorMessage = "Vui lòng nhập giá nhập.")]
        [Range(1, double.MaxValue, ErrorMessage = "Đơn giá nhập phải lớn hơn 0 VNĐ.")]
        public decimal ImportPrice { get; set; }

        /// <summary>
        /// Giá bán lẻ mới định lại (nếu có điều chỉnh theo giá nhập mới - Yêu cầu 10)
        /// </summary>
        public decimal? NewRetailPrice { get; set; }
    }

    /// <summary>
    /// ViewModel đối chiếu giá của sản phẩm tương tự từ các nhà cung cấp khác (Yêu cầu 9)
    /// </summary>
    public class PriceComparisonItemViewModel
    {
        public string SupplierName { get; set; } = string.Empty;
        public string? SupplierPhone { get; set; }
        public string? SupplierAddress { get; set; }
        public string? ProductName { get; set; }
        public string? SKU { get; set; }
        public string? Volume { get; set; }
        public string? ColorName { get; set; }
        public decimal ImportPrice { get; set; }
        public DateTime? ImportDate { get; set; }
        public string SourceType { get; set; } = "Lịch sử phiếu nhập"; // hoặc "Bảng cung ứng tham chiếu"
    }

    /// <summary>
    /// ViewModel chi tiết phiếu nhập phục vụ đối soát và thanh toán
    /// </summary>
    public class ReceiptDetailViewModel
    {
        public int Id { get; set; }
        public string ReceiptCode { get; set; } = string.Empty;
        public string ReceiptType { get; set; } = "Domestic";
        public string ReceiptTypeLabel { get; set; } = string.Empty;
        public string? PortName { get; set; }
        public string? PortType { get; set; }
        public string? CustomsDeclarationNumber { get; set; }
        public string StoreName { get; set; } = string.Empty;
        public string StoreAddress { get; set; } = string.Empty;
        public string StoreProvince { get; set; } = string.Empty;
        public string? StoreRegion { get; set; }
        public string? StoreType { get; set; }
        public string? SupplierName { get; set; }
        public string? SupplierAddress { get; set; }
        public string? SupplierPhone { get; set; }
        public string? InvoiceNumber { get; set; }
        public string OriginCountry { get; set; } = "Việt Nam";
        public string PaymentStatus { get; set; } = "Paid";
        public string PaymentStatusLabel { get; set; } = "Đã thanh toán";
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal DebtAmount { get; set; }
        public string ImportDate { get; set; } = string.Empty;
        public string CreatedAt { get; set; } = string.Empty;
        public string CreatedByName { get; set; } = string.Empty;
        public string? Note { get; set; }
        public List<ReceiptItemDetailViewModel> Items { get; set; } = new();
    }

    public class ReceiptItemDetailViewModel
    {
        public int VariantId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string SKU { get; set; } = string.Empty;
        public string? ColorName { get; set; }
        public string? Volume { get; set; }
        public string? CategoryName { get; set; }
        public string? BrandName { get; set; }
        public int Quantity { get; set; }
        public decimal ImportPrice { get; set; }
        public decimal TotalPrice { get; set; }
        public decimal? OldRetailPrice { get; set; }
        public decimal? NewRetailPrice { get; set; }
        public decimal? CurrentRetailPrice { get; set; }
    }
}
