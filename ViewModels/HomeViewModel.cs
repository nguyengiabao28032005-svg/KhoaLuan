using BeautyShop.Models;

namespace BeautyShop.ViewModels
{
    public class HomeViewModel
    {
        public List<Product> NewProducts { get; set; }
            = new();

        public List<Product> FeaturedProducts { get; set; }
            = new();

        public List<Brand> Brands { get; set; }
            = new();
        public List<Product> RecommendedProducts { get; set; } = new();
        public List<ComboSuggestion> SmartCombos { get; set; } = new();

    }
}