using System.ComponentModel.DataAnnotations;

namespace BeautyShop.Models
{
    public class Store
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(255)]
        public string Name { get; set; }

        [Required]
        public string Address { get; set; }

        [MaxLength(20)]
        public string? Phone { get; set; }

        public decimal? Latitude { get; set; }

        public decimal? Longitude { get; set; }

        public bool IsActive { get; set; } = true;

        [MaxLength(100)]
        public string? ProvinceName { get; set; }

        [MaxLength(50)]
        public string? ProvinceCode { get; set; }

        /// <summary>
        /// Khu vực / Miền: Miền Nam, Miền Bắc, Miền Trung, Tây Nguyên...
        /// </summary>
        [MaxLength(50)]
        public string? Region { get; set; }

        /// <summary>
        /// Loại kho: Kho tổng (Central), Kho trung chuyển (Transit), Kho phân phối / bán lẻ (Retail)...
        /// </summary>
        [MaxLength(50)]
        public string? StoreType { get; set; }

        public ICollection<Inventory>? Inventories { get; set; }
    }
}