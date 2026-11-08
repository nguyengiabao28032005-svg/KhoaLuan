using System.ComponentModel.DataAnnotations;

namespace BeautyShop.Models
{
    public class Category
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(255)]
        public string Name { get; set; }

        public int? ParentCategoryId { get; set; }

        [Required]
        [MaxLength(255)]
        public string Slug { get; set; }

        public Category? ParentCategory { get; set; }

        public ICollection<Category>? ChildCategories { get; set; }

        public ICollection<Product>? Products { get; set; }
    }
}