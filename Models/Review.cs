using System.ComponentModel.DataAnnotations;

namespace BeautyShop.Models
{
    public class Review
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        public int ProductId { get; set; }

        [Range(1, 5)]
        public int Rating { get; set; }

        public string? Comment { get; set; }

        public bool IsApproved { get; set; } = false;

        public bool IsHidden { get; set; } = false;

        public DateTime CreatedAt { get; set; }
            = DateTime.Now;

        public User? User { get; set; }

        public Product? Product { get; set; }
    }
}