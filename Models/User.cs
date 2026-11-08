using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BeautyShop.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required]
        public int RoleId { get; set; }

        [Required]
        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [StringLength(255)]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [StringLength(20)]
        public string? PhoneNumber { get; set; }

        public string? AvatarUrl { get; set; }

        public bool IsActive { get; set; } = true;

        [StringLength(50)]
        public string? Position { get; set; } // Chỉ dùng làm chức danh hiển thị phụ trên hồ sơ; phân quyền thực tế dựa trên Role & RolePermission (Bước 6).

        [StringLength(100)]
        public string SecurityStamp { get; set; } = Guid.NewGuid().ToString(); // Đổi tự động khi Admin thay đổi vai trò/phạm vi để làm mới quyền ngay lập tức

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ForeignKey("RoleId")]
        public Role? Role { get; set; }

        public ICollection<UserWarehouseScope> WarehouseScopes { get; set; } = new List<UserWarehouseScope>();

        public ICollection<UserAddress>? UserAddresses { get; set; }

        public ICollection<Cart>? Carts { get; set; }
        public Membership? Membership { get; set; }
    }
}