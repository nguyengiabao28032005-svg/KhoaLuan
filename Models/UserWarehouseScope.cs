using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace BeautyShop.Models
{
    /// <summary>
    /// Bảng gán phạm vi dữ liệu kho (UserWarehouseScope):
    /// Thay thế cho việc phân quyền cứng theo miền.
    /// Mỗi nhân viên (Nhân viên kho, Thu ngân...) được gán một hoặc nhiều kho.
    /// Admin mặc định xem toàn bộ các kho (IsAdmin = true) mà không cần tạo bản ghi trong bảng này.
    /// </summary>
    public class UserWarehouseScope
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }

        public int StoreId { get; set; }

        [ForeignKey("StoreId")]
        public Store? Store { get; set; }

        public int? AssignedByUserId { get; set; }

        [ForeignKey("AssignedByUserId")]
        public User? AssignedByUser { get; set; }

        public DateTime AssignedAt { get; set; } = DateTime.Now;
    }
}
