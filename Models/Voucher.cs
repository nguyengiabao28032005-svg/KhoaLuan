namespace BeautyShop.Models
{
    public class Voucher
    {
        public int Id { get; set; }

        public string Code { get; set; } = string.Empty;

        public string DiscountType { get; set; } = string.Empty;

        public decimal DiscountValue { get; set; }

        public decimal MinOrderValue { get; set; }

        public decimal? MaxDiscountAmount { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public int? TotalUsageLimit { get; set; }

        public int UsedCount { get; set; }

        public string Name { get; set; } = string.Empty;

        public int MaxUsesPerUser { get; set; } = 1;

        public string Scope { get; set; } = "All";

        public bool IsActive { get; set; } = true;

        public ICollection<UserVoucher>? UserVouchers { get; set; }
    }
}