using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace BeautyShop.Models
{
    public class Product
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        public string? ThumbnailUrl { get; set; }

        public string? Description { get; set; }

        public string? Ingredients { get; set; }

        public string? HowToUse { get; set; }

        public bool IsActive { get; set; } = true;

        public int CategoryId { get; set; }

        public int BrandId { get; set; }

        public Category? Category { get; set; }

        public Brand? Brand { get; set; }

        public ICollection<ProductVariant>? ProductVariants { get; set; }
    }
}