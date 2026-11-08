using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace BeautyShop.Models
{
    /// <summary>
    /// Thực thể Quyền chức năng (Permission).
    /// Đặt tên theo chuẩn module.action (ví dụ: product.manage, price.set, inventory.receive...).
    /// </summary>
    public class Permission
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [StringLength(150)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Category { get; set; } = string.Empty;

        [StringLength(300)]
        public string? Description { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public string DisplayName => Name;

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public string GroupName => Category;

        public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    }

    /// <summary>
    /// Hằng số mã quyền chuẩn trên toàn hệ thống
    /// </summary>
    public static class AppPermissions
    {
        public const string ProductManage = "product.manage";
        public const string PriceSet = "price.set";
        public const string PortManage = "port.manage";
        public const string RouteManage = "route.manage";
        public const string InventoryView = "inventory.view";
        public const string InventoryReceive = "inventory.receive";
        public const string InventoryTransfer = "inventory.transfer";
        public const string DeliveryDispatch = "delivery.dispatch";
        public const string DeliveryExecute = "delivery.execute";
        public const string PaymentProcess = "payment.process";
        public const string CashierReconcile = "cashier.reconcile";
        public const string ReportView = "report.view";
        public const string UserManage = "user.manage";
        public const string RoleManage = "role.manage";

        public static readonly string[] All = new[]
        {
            ProductManage,
            PriceSet,
            PortManage,
            RouteManage,
            InventoryView,
            InventoryReceive,
            InventoryTransfer,
            DeliveryDispatch,
            DeliveryExecute,
            PaymentProcess,
            CashierReconcile,
            ReportView,
            UserManage,
            RoleManage
        };

        public static string GetVietnameseName(string code) => code switch
        {
            ProductManage => "Quản lý sản phẩm, danh mục, thương hiệu",
            PriceSet => "Thiết lập giá bán lẻ, giá sỉ và khuyến mãi",
            PortManage => "Quản lý danh mục cảng nhập khẩu",
            RouteManage => "Cấu hình tuyến chuyển kho liên tỉnh",
            InventoryView => "Xem danh sách tồn kho",
            InventoryReceive => "Lập phiếu nhập kho (nhập khẩu & nội địa)",
            InventoryTransfer => "Lập và xác nhận phiếu chuyển kho",
            DeliveryDispatch => "Điều phối và lập chuyến giao hàng tối ưu",
            DeliveryExecute => "Thực hiện giao hàng và thu tiền hộ (COD)",
            PaymentProcess => "Xử lý đơn hàng và xác nhận thanh toán",
            CashierReconcile => "Quyết toán COD chuyến và đối soát cuối ngày",
            ReportView => "Xem báo cáo doanh thu và thống kê bán chạy/bán chậm",
            UserManage => "Quản lý nhân viên và phạm vi kho",
            RoleManage => "Quản lý vai trò, ma trận phân quyền và nhật ký hệ thống",
            _ => code
        };
    }

    /// <summary>
    /// Hằng số tên vai trò theo chức năng (Bước 6)
    /// </summary>
    public static class AppRoles
    {
        public const string Admin = "Admin";
        public const string Customer = "Customer";
        public const string Warehouse = "Warehouse";
        public const string Cashier = "Cashier";
        public const string Shipper = "Shipper";
        public const string Employee = "Employee";
        public const string LegacyEmployee = "Employee";

        public static string GetDisplayName(string? roleName) => roleName switch
        {
            Admin => "Quản trị viên (Admin)",
            Warehouse => "Nhân viên kho",
            Cashier => "Thu ngân",
            Shipper => "Nhân viên giao hàng",
            Customer => "Khách hàng",
            LegacyEmployee => "Nhân viên (Cũ)",
            _ => roleName ?? "Chưa xác định"
        };
    }
}
