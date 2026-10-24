using System.ComponentModel.DataAnnotations;

namespace BeautyShop.ViewModels
{
    public class CheckoutViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập họ và tên người nhận.")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "Họ và tên phải từ 2 đến 50 ký tự.")]
        public string CustomerName { get; set; } = "";

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
        [RegularExpression(@"^(03|05|07|08|09)\d{8}$", ErrorMessage = "Số điện thoại không đúng định dạng Việt Nam (10 chữ số).")]
        public string PhoneNumber { get; set; } = "";

        [Required(ErrorMessage = "Vui lòng nhập địa chỉ nhận hàng chi tiết.")]
        [StringLength(255, MinimumLength = 10, ErrorMessage = "Địa chỉ nhận hàng quá ngắn, vui lòng ghi rõ số nhà, tên đường, phường/xã.")]
        // BỘ LỌC CHẶN CHUỖI VÔ NGHĨA: Ép buộc phải có ít nhất ký tự chữ cái, không được chỉ nhập toàn số hoặc ký tự đặc biệt bừa bãi
        [RegularExpression(@"^(?=.*[a-zA-ZÀ-ỹ]).+$", ErrorMessage = "Địa chỉ không hợp lệ. Vui lòng nhập rõ ràng cả chữ và số (ví dụ: 123 Đường Nguyễn Trãi).")]
        public string ShippingAddress { get; set; } = "";

        public string PaymentMethod { get; set; } = "COD";
    }
}