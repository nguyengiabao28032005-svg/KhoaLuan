namespace BeautyShop.Models
{
    public class Order
    {

        public int Id { get; set; }

        public string OrderCode { get; set; } = string.Empty;

        public int? UserId { get; set; }

        public int? StoreId { get; set; }

        public int? CartId { get; set; }


        public string CustomerName { get; set; } = string.Empty;

        public string PhoneNumber { get; set; } = string.Empty;
        public string ShippingAddress { get; set; } = string.Empty;

        public decimal TotalAmount { get; set; }

        public decimal DeliveryFee { get; set; }

        public decimal DiscountAmount { get; set; }

        public decimal FinalAmount { get; set; }

        public string OrderStatus { get; set; } = string.Empty;

        public string PaymentMethod { get; set; } = string.Empty;

        public string PaymentStatus { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        public User? User { get; set; }

        public Store? Store { get; set; }

        public Cart? Cart { get; set; }

        public ICollection<OrderDetail>? OrderDetails { get; set; }
        public string? GhnOrderCode { get; set; }
        // Mã vận đơn GHN (được gán khi Admin xác nhận đơn hàng)
     
        public int ToDistrictId { get; set; }
        public string? ToWardCode { get; set; }

        // ===== CÁC TRƯỜNG PHỤC VỤ GIAO HÀNG TỐI ƯU (BƯỚC 5) =====
        public int? DeliveryZoneId { get; set; }
        public DeliveryZone? DeliveryZone { get; set; }

        public DateTime? ExpectedDeliveryDate { get; set; } // Ngày giao dự kiến
        public string? DeliveryShift { get; set; } // Ca giao: Morning (Sáng), Afternoon (Chiều), Evening (Tối)

        public int FailedDeliveryAttempts { get; set; } = 0; // Số lần giao thất bại (quá giới hạn thì chuyển sang PendingResolution)

        public string DeliveryStatus { get; set; } = "Ready"; // Ready (Chờ xếp chuyến), Assigned (Đã lên chuyến), Delivering (Đang giao), Delivered (Đã giao), Failed (Thất bại chờ xếp lại), PendingResolution (Chờ xử lý do quá số lần giao lại)
    }
}