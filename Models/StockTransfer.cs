using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace BeautyShop.Models
{
    /// <summary>
    /// Thực thể Phiếu chuyển kho
    /// Quản lý từng chặng vận chuyển của một lô hàng giữa các kho
    /// </summary>
    public class StockTransfer
    {
        public int Id { get; set; }

        /// <summary>
        /// Mã phiếu chuyển kho tự sinh (ví dụ: CK-20261015-0001)
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string TransferCode { get; set; } = string.Empty;

        /// <summary>
        /// Mã lô hàng chung cho các chặng (ví dụ: LOT-20261015-0001)
        /// Giúp theo dõi toàn bộ hành trình từ kho xuất phát ban đầu đến kho đích cuối cùng qua Hub
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string BatchCode { get; set; } = string.Empty;

        /// <summary>
        /// Thứ tự chặng trong lô (Chặng 1, Chặng 2...)
        /// </summary>
        public int LegIndex { get; set; } = 1;

        /// <summary>
        /// Kho gửi hàng của chặng hiện tại
        /// </summary>
        [Required]
        public int FromStoreId { get; set; }

        /// <summary>
        /// Kho nhận hàng của chặng hiện tại (có thể là Hub hoặc kho đích)
        /// </summary>
        [Required]
        public int ToStoreId { get; set; }

        /// <summary>
        /// Kho đích cuối cùng của toàn bộ lô hàng
        /// </summary>
        [Required]
        public int FinalDestinationStoreId { get; set; }

        /// <summary>
        /// Trạng thái: "InTransit" (Đang chuyển), "Received" (Đã nhận), "Cancelled" (Đã hủy)
        /// </summary>
        [Required]
        [MaxLength(30)]
        public string Status { get; set; } = "InTransit";

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public int? CreatedByUserId { get; set; }

        [MaxLength(100)]
        public string? CreatedByName { get; set; }

        public DateTime? ReceivedAt { get; set; }

        public int? ReceivedByUserId { get; set; }

        [MaxLength(100)]
        public string? ReceivedByName { get; set; }

        public DateTime? CancelledAt { get; set; }

        [MaxLength(255)]
        public string? CancelledReason { get; set; }

        [MaxLength(500)]
        public string? Note { get; set; }

        // Navigation properties
        public Store? FromStore { get; set; }
        public Store? ToStore { get; set; }
        public Store? FinalDestinationStore { get; set; }
        public User? CreatedByUser { get; set; }
        public User? ReceivedByUser { get; set; }

        public ICollection<StockTransferLine> Lines { get; set; } = new List<StockTransferLine>();
    }
}
