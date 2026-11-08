using BeautyShop.Models;

namespace BeautyShop.ViewModels
{
    public class ComboSuggestion
    {
        // Danh sách sản phẩm trong combo (2, 3, 4 sản phẩm)
        public List<Product> Products { get; set; } = new();

        // Tổng giá gốc
        public decimal OriginalTotal { get; set; }

        // Giá combo sau giảm (mặc định giảm 10%)
        public decimal ComboPrice { get; set; }

        // Phần trăm giảm giá
        public int DiscountPercent { get; set; }

        // Độ phổ biến (số lần bộ sản phẩm này được mua cùng nhau)
        public int Support { get; set; }
    }
}