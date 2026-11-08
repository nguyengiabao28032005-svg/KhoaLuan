namespace BeautyShop.Models
{
    public class Cart
    {
        public int Id { get; set; }

        public int? UserId { get; set; }

        public string? SessionId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public User? User { get; set; }

        public ICollection<CartItem>? CartItems { get; set; }

       public ICollection<Order>? Orders { get; set; }

    }
}