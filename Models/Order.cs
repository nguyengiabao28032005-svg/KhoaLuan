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

        // Ngày hoàn thành đơn hàng (dùng để ghi nhận doanh thu và lợi nhuận chuẩn)
        public DateTime? CompletedAt { get; set; }

        // ===== CÁC TRƯỜNG PHỤC VỤ THỐNG KÊ DOANH THU & THEO DÕI TIỀN (YÊU CẦU 4) =====
        public decimal DepositAmount { get; set; } = 0; // Số tiền đặt cọc
        public DateTime? DepositDate { get; set; } // Ngày đặt cọc
        public decimal PaymentAmount { get; set; } = 0; // Số tiền thanh toán
        public DateTime? PaymentDate { get; set; } // Ngày thanh toán
        public DateTime? MoneyReceivedDate { get; set; } // Ngày thực nhận tiền (để thống kê doanh thu theo ngày nhận tiền)
        public DateTime? StatusUpdatedAt { get; set; } // Ngày giờ cập nhật tình trạng mới nhất

        // ===== CÁC TRƯỜNG PHỤC VỤ PHÂN LUỒNG GIAO HÀNG (YÊU CẦU 3) =====
        /// <summary>
        /// "Internal" (Giao nội bộ TP.HCM) hoặc "ThirdParty" (Giao qua GHN / Chuyển phát đi tỉnh)
        /// </summary>
        public string DeliveryType { get; set; } = "Internal";

        // Luồng chuyển phát bên thứ 3 (GHN, Đi tỉnh)
        public string? CarrierStaffName { get; set; } // Nhân viên phụ trách chuyển hàng qua bưu cục
        public DateTime? CarrierHandoverAt { get; set; } // Giờ bàn giao cho bưu cục
        public DateTime? CarrierPaymentReceivedDate { get; set; } // Ngày nhận tiền đối soát chuyển phát
        public string? CarrierPaymentMethod { get; set; } // Hình thức nhận tiền (Chuyển khoản MBBank, Vietcombank...)

        // Luồng giao nội bộ (TP.HCM)
        public string? InternalDeliveryStaffName { get; set; } // Nhân viên kho phân công / giao hàng
        public DateTime? InternalDepartureTime { get; set; } // Giờ nhân viên kho bàn giao / shipper nhận hàng xuất phát
        public string? SettlementConfirmedBy { get; set; } // Nhân viên kho / thu ngân nhận nộp tiền
        public DateTime? SettlementConfirmedAt { get; set; } // Ngày giờ nhân viên kho nhận tiền từ shipper và hoàn tất đơn

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