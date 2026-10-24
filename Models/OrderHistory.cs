namespace BeautyShop.Models
{
    public class OrderHistory
    {
        public int Id { get; set; }

        public int OrderId { get; set; }

        public string? OldStatus { get; set; }

        public string NewStatus { get; set; } = string.Empty;
        public string? Note { get; set; }

        public int? CreatedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public Order? Order { get; set; }

        public User? CreatedByUser { get; set; }
    }
}