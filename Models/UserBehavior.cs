namespace BeautyShop.Models
{
    public class UserBehavior
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        public int ProductId { get; set; }

        public string ActionType { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public User? User { get; set; }

        public Product? Product { get; set; }
    }
}