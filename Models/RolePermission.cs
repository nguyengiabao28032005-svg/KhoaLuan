using System.ComponentModel.DataAnnotations.Schema;

namespace BeautyShop.Models
{
    /// <summary>
    /// Bảng liên kết nhiều - nhiều giữa Vai trò (Role) và Quyền chức năng (Permission).
    /// Cho phép Admin xem ma trận Vai trò x Quyền và điều chỉnh quyền linh hoạt.
    /// </summary>
    public class RolePermission
    {
        public int Id { get; set; }

        public int RoleId { get; set; }

        [ForeignKey("RoleId")]
        public Role? Role { get; set; }

        public int PermissionId { get; set; }

        [ForeignKey("PermissionId")]
        public Permission? Permission { get; set; }
    }
}
