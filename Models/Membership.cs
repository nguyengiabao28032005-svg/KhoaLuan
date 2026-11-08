namespace BeautyShop.Models
{
    public class Membership
    {
        public int UserId { get; set; }

        public int CurrentPoints { get; set; } = 0;

        public int TotalEarnedPoints { get; set; } = 0;

        public string Tier { get; set; } = "Member";

        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        public User? User { get; set; }
    }
}