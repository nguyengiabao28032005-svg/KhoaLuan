using System.ComponentModel.DataAnnotations;

namespace BeautyShop.Models
{
    public class Brand
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(255)]
        public string Name { get; set; }

        [MaxLength(100)]
        public string? Origin { get; set; }

        public string? LogoUrl { get; set; }

        public ICollection<Product>? Products { get; set; }
    }
}