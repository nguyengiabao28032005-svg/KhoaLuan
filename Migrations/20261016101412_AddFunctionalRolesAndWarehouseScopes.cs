using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace BeautyShop.Migrations
{
    /// <inheritdoc />
    public partial class AddFunctionalRolesAndWarehouseScopes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SecurityStamp",
                table: "Users",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Roles",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "Roles",
                type: "TEXT",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StoreId",
                table: "DeliveryTrips",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StoreId",
                table: "DailyReconciliations",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PerformedByUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    PerformedByUserName = table.Column<string>(type: "TEXT", maxLength: 150, nullable: true),
                    Action = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    TargetUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    TargetUserName = table.Column<string>(type: "TEXT", maxLength: 150, nullable: true),
                    OldValue = table.Column<string>(type: "TEXT", nullable: true),
                    NewValue = table.Column<string>(type: "TEXT", nullable: true),
                    Note = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditLogs_Users_PerformedByUserId",
                        column: x => x.PerformedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AuditLogs_Users_TargetUserId",
                        column: x => x.TargetUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Permissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Code = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserWarehouseScopes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    StoreId = table.Column<int>(type: "INTEGER", nullable: false),
                    AssignedByUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    AssignedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserWarehouseScopes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserWarehouseScopes_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserWarehouseScopes_Users_AssignedByUserId",
                        column: x => x.AssignedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_UserWarehouseScopes_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RoleId = table.Column<int>(type: "INTEGER", nullable: false),
                    PermissionId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RolePermissions_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RolePermissions_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "DeliveryZones",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 10, 16, 17, 14, 8, 521, DateTimeKind.Local).AddTicks(8462));

            migrationBuilder.UpdateData(
                table: "DeliveryZones",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 10, 16, 17, 14, 8, 521, DateTimeKind.Local).AddTicks(8467));

            migrationBuilder.UpdateData(
                table: "DeliveryZones",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 10, 16, 17, 14, 8, 521, DateTimeKind.Local).AddTicks(8470));

            migrationBuilder.UpdateData(
                table: "DeliveryZones",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 10, 16, 17, 14, 8, 521, DateTimeKind.Local).AddTicks(8471));

            migrationBuilder.UpdateData(
                table: "DeliveryZones",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 10, 16, 17, 14, 8, 521, DateTimeKind.Local).AddTicks(8473));

            migrationBuilder.UpdateData(
                table: "DeliveryZones",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 10, 16, 17, 14, 8, 521, DateTimeKind.Local).AddTicks(8474));

            migrationBuilder.UpdateData(
                table: "DeliveryZones",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2026, 10, 16, 17, 14, 8, 521, DateTimeKind.Local).AddTicks(8476));

            migrationBuilder.UpdateData(
                table: "DeliveryZones",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2026, 10, 16, 17, 14, 8, 521, DateTimeKind.Local).AddTicks(8477));

            migrationBuilder.UpdateData(
                table: "DeliveryZones",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedAt",
                value: new DateTime(2026, 10, 16, 17, 14, 8, 521, DateTimeKind.Local).AddTicks(8479));

            migrationBuilder.UpdateData(
                table: "DeliveryZones",
                keyColumn: "Id",
                keyValue: 10,
                column: "CreatedAt",
                value: new DateTime(2026, 10, 16, 17, 14, 8, 521, DateTimeKind.Local).AddTicks(8480));

            migrationBuilder.UpdateData(
                table: "DeliveryZones",
                keyColumn: "Id",
                keyValue: 11,
                column: "CreatedAt",
                value: new DateTime(2026, 10, 16, 17, 14, 8, 521, DateTimeKind.Local).AddTicks(8482));

            migrationBuilder.UpdateData(
                table: "DeliveryZones",
                keyColumn: "Id",
                keyValue: 12,
                column: "CreatedAt",
                value: new DateTime(2026, 10, 16, 17, 14, 8, 521, DateTimeKind.Local).AddTicks(8483));

            migrationBuilder.UpdateData(
                table: "DeliveryZones",
                keyColumn: "Id",
                keyValue: 13,
                column: "CreatedAt",
                value: new DateTime(2026, 10, 16, 17, 14, 8, 521, DateTimeKind.Local).AddTicks(8485));

            migrationBuilder.UpdateData(
                table: "DeliveryZones",
                keyColumn: "Id",
                keyValue: 14,
                column: "CreatedAt",
                value: new DateTime(2026, 10, 16, 17, 14, 8, 521, DateTimeKind.Local).AddTicks(8486));

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Category", "Code", "Description", "Name" },
                values: new object[,]
                {
                    { 1, "Sản phẩm", "product.manage", "Xem, thêm, sửa, xóa sản phẩm, danh mục và thương hiệu", "Quản lý sản phẩm" },
                    { 2, "Giá & Khuyến mãi", "price.set", "Cấu hình giá bán lẻ, giá bán sỉ, voucher và chương trình Flash Sale", "Thiết lập giá bán" },
                    { 3, "Kho bãi", "inventory.view", "Xem số lượng tồn kho thực tế, hàng đang giữ và lịch sử biến động kho thuộc phạm vi", "Xem tồn kho" },
                    { 4, "Kho bãi", "inventory.receive", "Lập phiếu nhập khẩu theo cảng và nhập hàng nội địa cho các kho thuộc phạm vi", "Lập phiếu nhập kho" },
                    { 5, "Kho bãi", "inventory.transfer", "Tạo phiếu xuất chuyển kho và xác nhận nhận hàng chuyển kho thuộc phạm vi", "Luân chuyển kho" },
                    { 6, "Giao hàng", "delivery.dispatch", "Gom chuyến giao tự động, gán nhân viên giao hàng và sắp xếp thứ tự điểm dừng", "Điều phối chuyến giao" },
                    { 7, "Giao hàng", "delivery.execute", "Xem chuyến giao của chính mình, cập nhật trạng thái điểm dừng và ghi nhận tiền thu hộ COD", "Thực hiện giao hàng" },
                    { 8, "Thu ngân", "payment.process", "Xử lý đơn hàng, xác nhận thanh toán và cập nhật trạng thái đơn trong phạm vi kho", "Thanh toán đơn hàng" },
                    { 9, "Thu ngân", "cashier.reconcile", "Nhận tiền COD từ nhân viên giao hàng, quyết toán chuyến và lập bảng đối soát cuối ngày nộp Admin", "Quyết toán & Đối soát COD" },
                    { 10, "Báo cáo", "report.view", "Xem thống kê doanh thu, sản phẩm bán chạy và sản phẩm bán chậm trong phạm vi", "Xem báo cáo kinh doanh" },
                    { 11, "Hệ thống", "user.manage", "Tạo tài khoản nhân viên, gán vai trò, phân công phạm vi kho và khóa/mở tài khoản", "Quản lý nhân viên" },
                    { 12, "Hệ thống", "role.manage", "Xem và cấu hình ma trận Vai trò x Quyền, tra cứu nhật ký thay đổi phân quyền (AuditLog)", "Quản lý vai trò & Phân quyền" },
                    { 13, "Kho bãi", "port.manage", "Thêm, sửa, bật/tắt cảng nhập khẩu hợp lệ trong danh mục quốc gia", "Quản lý cảng nhập khẩu" },
                    { 14, "Kho bãi", "route.manage", "Cấu hình tuyến luân chuyển liên tỉnh và kho trung chuyển (Hub)", "Cấu hình tuyến chuyển kho" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryTrips_StoreId",
                table: "DeliveryTrips",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyReconciliations_StoreId",
                table: "DailyReconciliations",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_CreatedAt",
                table: "AuditLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_PerformedByUserId",
                table: "AuditLogs",
                column: "PerformedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_TargetUserId",
                table: "AuditLogs",
                column: "TargetUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_Code",
                table: "Permissions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_PermissionId",
                table: "RolePermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_RoleId_PermissionId",
                table: "RolePermissions",
                columns: new[] { "RoleId", "PermissionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserWarehouseScopes_AssignedByUserId",
                table: "UserWarehouseScopes",
                column: "AssignedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserWarehouseScopes_StoreId",
                table: "UserWarehouseScopes",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_UserWarehouseScopes_UserId_StoreId",
                table: "UserWarehouseScopes",
                columns: new[] { "UserId", "StoreId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_DailyReconciliations_Stores_StoreId",
                table: "DailyReconciliations",
                column: "StoreId",
                principalTable: "Stores",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_DeliveryTrips_Stores_StoreId",
                table: "DeliveryTrips",
                column: "StoreId",
                principalTable: "Stores",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DailyReconciliations_Stores_StoreId",
                table: "DailyReconciliations");

            migrationBuilder.DropForeignKey(
                name: "FK_DeliveryTrips_Stores_StoreId",
                table: "DeliveryTrips");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "RolePermissions");

            migrationBuilder.DropTable(
                name: "UserWarehouseScopes");

            migrationBuilder.DropTable(
                name: "Permissions");

            migrationBuilder.DropIndex(
                name: "IX_DeliveryTrips_StoreId",
                table: "DeliveryTrips");

            migrationBuilder.DropIndex(
                name: "IX_DailyReconciliations_StoreId",
                table: "DailyReconciliations");

            migrationBuilder.DropColumn(
                name: "SecurityStamp",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "StoreId",
                table: "DeliveryTrips");

            migrationBuilder.DropColumn(
                name: "StoreId",
                table: "DailyReconciliations");

            migrationBuilder.UpdateData(
                table: "DeliveryZones",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 10, 16, 0, 6, 8, 687, DateTimeKind.Local).AddTicks(2700));

            migrationBuilder.UpdateData(
                table: "DeliveryZones",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 10, 16, 0, 6, 8, 687, DateTimeKind.Local).AddTicks(2706));

            migrationBuilder.UpdateData(
                table: "DeliveryZones",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 10, 16, 0, 6, 8, 687, DateTimeKind.Local).AddTicks(2710));

            migrationBuilder.UpdateData(
                table: "DeliveryZones",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 10, 16, 0, 6, 8, 687, DateTimeKind.Local).AddTicks(2712));

            migrationBuilder.UpdateData(
                table: "DeliveryZones",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 10, 16, 0, 6, 8, 687, DateTimeKind.Local).AddTicks(2715));

            migrationBuilder.UpdateData(
                table: "DeliveryZones",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 10, 16, 0, 6, 8, 687, DateTimeKind.Local).AddTicks(2717));

            migrationBuilder.UpdateData(
                table: "DeliveryZones",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2026, 10, 16, 0, 6, 8, 687, DateTimeKind.Local).AddTicks(2719));

            migrationBuilder.UpdateData(
                table: "DeliveryZones",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2026, 10, 16, 0, 6, 8, 687, DateTimeKind.Local).AddTicks(2721));

            migrationBuilder.UpdateData(
                table: "DeliveryZones",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedAt",
                value: new DateTime(2026, 10, 16, 0, 6, 8, 687, DateTimeKind.Local).AddTicks(2724));

            migrationBuilder.UpdateData(
                table: "DeliveryZones",
                keyColumn: "Id",
                keyValue: 10,
                column: "CreatedAt",
                value: new DateTime(2026, 10, 16, 0, 6, 8, 687, DateTimeKind.Local).AddTicks(2726));

            migrationBuilder.UpdateData(
                table: "DeliveryZones",
                keyColumn: "Id",
                keyValue: 11,
                column: "CreatedAt",
                value: new DateTime(2026, 10, 16, 0, 6, 8, 687, DateTimeKind.Local).AddTicks(2728));

            migrationBuilder.UpdateData(
                table: "DeliveryZones",
                keyColumn: "Id",
                keyValue: 12,
                column: "CreatedAt",
                value: new DateTime(2026, 10, 16, 0, 6, 8, 687, DateTimeKind.Local).AddTicks(2730));

            migrationBuilder.UpdateData(
                table: "DeliveryZones",
                keyColumn: "Id",
                keyValue: 13,
                column: "CreatedAt",
                value: new DateTime(2026, 10, 16, 0, 6, 8, 687, DateTimeKind.Local).AddTicks(2732));

            migrationBuilder.UpdateData(
                table: "DeliveryZones",
                keyColumn: "Id",
                keyValue: 14,
                column: "CreatedAt",
                value: new DateTime(2026, 10, 16, 0, 6, 8, 687, DateTimeKind.Local).AddTicks(2734));
        }
    }
}
