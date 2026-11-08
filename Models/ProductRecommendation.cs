namespace BeautyShop.Models
{
    public class ProductRecommendation
    {
        public int Id { get; set; }

        public int ProductId { get; set; }

        public int RecommendedProductId { get; set; }

        public Product? Product { get; set; }

        public Product? RecommendedProduct { get; set; }
    }
}