namespace BeautyShop.Models
{
    public class UserVoucher
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        public int VoucherId { get; set; }

        public bool IsUsed { get; set; }

        public DateTime? UsedAt { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public User? User { get; set; }

        public Voucher? Voucher { get; set; }
    }
}