namespace BeautyShop.Models
{
    public class PointHistory
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        public int? OrderId { get; set; }

        public int Points { get; set; }

        public string TransactionType { get; set; } = string.Empty;

        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public Membership? Membership { get; set; }

        public Order? Order { get; set; }
    }
}