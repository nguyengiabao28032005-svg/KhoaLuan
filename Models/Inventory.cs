using System.ComponentModel.DataAnnotations;

namespace BeautyShop.Models
{
    public class Inventory
    {
        public int Id { get; set; }

        public int StoreId { get; set; }

        public int VariantId { get; set; }

        public int StockQuantity { get; set; } = 0;

        public int ReservedQuantity { get; set; } = 0;

        /// <summary>
        /// Số lượng hàng đang trên đường luân chuyển (InTransit)
        /// </summary>
        public int InTransitQuantity { get; set; } = 0;

        public byte[]? RowVersion { get; set; }

        public Store? Store { get; set; }

        public ProductVariant? Variant { get; set; }

        public ICollection<InventoryLog>? InventoryLogs { get; set; }
    }
}