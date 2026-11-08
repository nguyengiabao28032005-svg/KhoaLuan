namespace BeautyShop.Models
{
    public class FlashSaleItem
    {
        public int Id { get; set; }

        public int FlashSaleId { get; set; }

        public int VariantId { get; set; }

        public decimal PromotionPrice { get; set; }

        public int QuantityLimit { get; set; }

        public int SoldQuantity { get; set; }

        public int MaxQuantityPerUser { get; set; }

        public FlashSale? FlashSale { get; set; }

        public ProductVariant? Variant { get; set; }
    }
}