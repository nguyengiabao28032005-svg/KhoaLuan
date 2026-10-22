namespace BeautyShop.Models
{
    public class ProductView
    {
        public int Id { get; set; }

        public int? UserId { get; set; }

        public int ProductId { get; set; }

        public DateTime ViewedAt { get; set; } = DateTime.Now;

        public User? User { get; set; }

        public Product? Product { get; set; }
    }
}