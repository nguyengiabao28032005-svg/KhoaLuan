namespace BeautyShop.Models
{
    public class WishlistItem
    {
        public int Id { get; set; }

        public int WishlistId { get; set; }

        public int ProductId { get; set; }

        public Wishlist? Wishlist { get; set; }

        public Product? Product { get; set; }
    }
}