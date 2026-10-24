using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace BeautyShop.Models
{
    public class Supplier
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên nhà cung cấp.")]
        [MaxLength(255)]
        [Display(Name = "Tên nhà cung cấp")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(150)]
        [Display(Name = "Người đại diện liên hệ")]
        public string? ContactPerson { get; set; }

        [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
        [MaxLength(150)]
        public string? Email { get; set; }

        [MaxLength(50)]
        [Display(Name = "Số điện thoại")]
        public string? Phone { get; set; }

        /// <summary>
        /// Địa chỉ nhà cung cấp (bắt buộc - phục vụ phản hồi khiếu nại và đổi trả khi hàng lỗi)
        /// </summary>
        [Required(ErrorMessage = "Vui lòng nhập địa chỉ nhà cung cấp để phản hồi khi hàng lỗi.")]
        [MaxLength(500)]
        [Display(Name = "Địa chỉ nhà cung cấp")]
        public string Address { get; set; } = string.Empty;

        [MaxLength(50)]
        [Display(Name = "Mã số thuế")]
        public string? TaxCode { get; set; }

        [MaxLength(100)]
        [Display(Name = "Quốc gia xuất xứ")]
        public string Country { get; set; } = "Việt Nam";

        [Display(Name = "Đang hoạt động")]
        public bool IsActive { get; set; } = true;

        [MaxLength(500)]
        [Display(Name = "Ghi chú hợp tác")]
        public string? Note { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation properties
        public ICollection<SupplierProduct> SupplierProducts { get; set; } = new List<SupplierProduct>();
        public ICollection<StockReceipt> Receipts { get; set; } = new List<StockReceipt>();
    }
}
