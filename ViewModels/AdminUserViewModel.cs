using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace BeautyShop.ViewModels
{
    public class AdminUserViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập họ tên")]
        public string FullName { get; set; } = "";

        [Required(ErrorMessage = "Vui lòng nhập email")]
        public string Email { get; set; } = "";

        public string? PhoneNumber { get; set; }

        public string? Password { get; set; }

        public int RoleId { get; set; }

        public string? Position { get; set; }

        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Danh sách ID các kho (UserWarehouseScope) được phân công cho nhân viên này
        /// </summary>
        public List<int> SelectedStoreIds { get; set; } = new List<int>();
    }
}