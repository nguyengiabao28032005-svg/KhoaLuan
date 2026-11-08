using System.ComponentModel.DataAnnotations;

namespace BeautyShop.Models
{
    public class InventoryLog
    {
        public int Id { get; set; }

        public int InventoryId { get; set; }

        [Required]
        [MaxLength(50)]
        public string ChangeType { get; set; }

        public int QuantityChanged { get; set; }

        public string? Note { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public Inventory? Inventory { get; set; }
    }
}