using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BeautyShop.Models
{
    public class ConsultationProduct
    {
        public int Id { get; set; }

        [Required]
        public int ConsultationId { get; set; }

        [Required]
        public int ProductId { get; set; }

        [MaxLength(500)]
        public string? Note { get; set; } // Ghi chú, liều lượng, cách dùng sản phẩm được đề xuất

        [ForeignKey("ConsultationId")]
        public Consultation? Consultation { get; set; }

        [ForeignKey("ProductId")]
        public Product? Product { get; set; }
    }
}
