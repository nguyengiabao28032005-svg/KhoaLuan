using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BeautyShop.Models
{
    /// <summary>
    /// Nhật ký thay đổi phân quyền & phạm vi dữ liệu (AuditLog):
    /// Ghi nhận ai đổi vai trò/phạm vi/trạng thái của ai, khi nào, giá trị cũ và mới.
    /// </summary>
    public class AuditLog
    {
        public int Id { get; set; }

        public int? PerformedByUserId { get; set; }

        [ForeignKey("PerformedByUserId")]
        public User? PerformedByUser { get; set; }

        [StringLength(150)]
        public string? PerformedByUserName { get; set; }

        [Required]
        [StringLength(100)]
        public string Action { get; set; } = string.Empty; // ChangeRole, UpdateScope, LockUser, UnlockUser, UpdatePermissions...

        public int? TargetUserId { get; set; }

        [ForeignKey("TargetUserId")]
        public User? TargetUser { get; set; }

        [StringLength(150)]
        public string? TargetUserName { get; set; }

        public string? OldValue { get; set; }

        public string? NewValue { get; set; }

        [StringLength(500)]
        public string? Note { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
