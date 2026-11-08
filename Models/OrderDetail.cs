namespace BeautyShop.Models
{
    public class OrderDetail
    {
        public int Id { get; set; }

        public int OrderId { get; set; }

        public int VariantId { get; set; }

        public int Quantity { get; set; }

        public decimal UnitPrice { get; set; }

        // Giá vốn đơn vị tại thời điểm bán (để tính COGS và Lợi nhuận gộp bất biến lịch sử)
        public decimal CostPrice { get; set; } = 0;

        public Order? Order { get; set; }

        public ProductVariant? Variant { get; set; }
    }
}