using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace BeautyShop.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryOptimizationAndReconciliation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Position",
                table: "Users",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryShift",
                table: "Orders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryStatus",
                table: "Orders",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "DeliveryZoneId",
                table: "Orders",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpectedDeliveryDate",
                table: "Orders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FailedDeliveryAttempts",
                table: "Orders",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "DailyReconciliations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReconciliationDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    CashierUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    AdminApprovedUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    TotalTripsSettled = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalOrdersDelivered = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalOrdersFailed = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalCodExpected = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalCodReceived = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalCashSubmitted = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DifferenceAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DifferenceReason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    TopSellingProductsJson = table.Column<string>(type: "TEXT", nullable: true),
                    SlowSellingProductsJson = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyReconciliations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DailyReconciliations_Users_AdminApprovedUserId",
                        column: x => x.AdminApprovedUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DailyReconciliations_Users_CashierUserId",
                        column: x => x.CashierUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DeliveryTrips",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TripCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    TripDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Shift = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    ClusterCode = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ShipperUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    TotalOrders = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalCodExpected = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalCodCollected = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CashierUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    SettlementStatus = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    SettledAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SettlementReceivedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    SettlementDifference = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    SettlementNotes = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    CreatedByUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryTrips", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeliveryTrips_Users_CashierUserId",
                        column: x => x.CashierUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DeliveryTrips_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DeliveryTrips_Users_ShipperUserId",
                        column: x => x.ShipperUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "DeliveryZones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ZoneName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ProvinceName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ProvinceCode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    ClusterCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ClusterName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    AdjacentClusterCodes = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryZones", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeliveryTripStops",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DeliveryTripId = table.Column<int>(type: "INTEGER", nullable: false),
                    OrderId = table.Column<int>(type: "INTEGER", nullable: false),
                    StopOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    StopStatus = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    CodExpectedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CodCollectedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FailureReason = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryTripStops", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeliveryTripStops_DeliveryTrips_DeliveryTripId",
                        column: x => x.DeliveryTripId,
                        principalTable: "DeliveryTrips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DeliveryTripStops_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "DeliveryZones",
                columns: new[] { "Id", "AdjacentClusterCodes", "ClusterCode", "ClusterName", "CreatedAt", "IsActive", "Notes", "ProvinceCode", "ProvinceName", "SortOrder", "ZoneName" },
                values: new object[,]
                {
                    { 1, "HCM_CEN", "HCM_NW", "Cụm Tây Bắc (Hóc Môn - Củ Chi - Q12)", new DateTime(2026, 10, 16, 0, 6, 8, 687, DateTimeKind.Local).AddTicks(2700), true, "Cần kiểm chứng (đang có đề án chuyển đổi cấp hành chính)", "79", "Thành phố Hồ Chí Minh", 1, "Hóc Môn" },
                    { 2, "HCM_NW", "HCM_NW", "Cụm Tây Bắc (Hóc Môn - Củ Chi - Q12)", new DateTime(2026, 10, 16, 0, 6, 8, 687, DateTimeKind.Local).AddTicks(2706), true, "Cần kiểm chứng (đang có đề án chuyển đổi cấp hành chính)", "79", "Thành phố Hồ Chí Minh", 2, "Củ Chi" },
                    { 3, "HCM_CEN", "HCM_NW", "Cụm Tây Bắc (Hóc Môn - Củ Chi - Q12)", new DateTime(2026, 10, 16, 0, 6, 8, 687, DateTimeKind.Local).AddTicks(2710), true, "Khu vực ổn định", "79", "Thành phố Hồ Chí Minh", 3, "Quận 12" },
                    { 4, "HCM_NW,HCM_EST", "HCM_CEN", "Cụm Trung Tâm (Gò Vấp - Bình Thạnh - Q1 - Q3 - Phú Nhuận)", new DateTime(2026, 10, 16, 0, 6, 8, 687, DateTimeKind.Local).AddTicks(2712), true, "Khu vực ổn định kề Tây Bắc", "79", "Thành phố Hồ Chí Minh", 4, "Gò Vấp" },
                    { 5, "HCM_EST", "HCM_CEN", "Cụm Trung Tâm (Gò Vấp - Bình Thạnh - Q1 - Q3 - Phú Nhuận)", new DateTime(2026, 10, 16, 0, 6, 8, 687, DateTimeKind.Local).AddTicks(2715), true, "Cửa ngõ kề TP. Thủ Đức", "79", "Thành phố Hồ Chí Minh", 5, "Bình Thạnh" },
                    { 6, "HCM_CEN", "HCM_CEN", "Cụm Trung Tâm (Gò Vấp - Bình Thạnh - Q1 - Q3 - Phú Nhuận)", new DateTime(2026, 10, 16, 0, 6, 8, 687, DateTimeKind.Local).AddTicks(2717), true, "Khu vực trung tâm nội đô", "79", "Thành phố Hồ Chí Minh", 6, "Phú Nhuận" },
                    { 7, "HCM_SOU", "HCM_CEN", "Cụm Trung Tâm (Gò Vấp - Bình Thạnh - Q1 - Q3 - Phú Nhuận)", new DateTime(2026, 10, 16, 0, 6, 8, 687, DateTimeKind.Local).AddTicks(2719), true, "Khu vực trung tâm nội đô", "79", "Thành phố Hồ Chí Minh", 7, "Quận 1" },
                    { 8, "HCM_CEN", "HCM_CEN", "Cụm Trung Tâm (Gò Vấp - Bình Thạnh - Q1 - Q3 - Phú Nhuận)", new DateTime(2026, 10, 16, 0, 6, 8, 687, DateTimeKind.Local).AddTicks(2721), true, "Khu vực trung tâm nội đô", "79", "Thành phố Hồ Chí Minh", 8, "Quận 3" },
                    { 9, "HCM_CEN", "HCM_EST", "Cụm Phía Đông (TP. Thủ Đức)", new DateTime(2026, 10, 16, 0, 6, 8, 687, DateTimeKind.Local).AddTicks(2724), true, "Đã sáp nhập Q2, Q9 và Thủ Đức cũ thành TP. Thủ Đức từ 2021", "79", "Thành phố Hồ Chí Minh", 9, "Thành phố Thủ Đức" },
                    { 10, "HCM_CEN", "HCM_SOU", "Cụm Phía Nam (Q7 - Nhà Bè - Bình Chánh)", new DateTime(2026, 10, 16, 0, 6, 8, 687, DateTimeKind.Local).AddTicks(2726), true, "Khu vực Nam Sài Gòn", "79", "Thành phố Hồ Chí Minh", 10, "Quận 7" },
                    { 11, "HCM_SOU", "HCM_SOU", "Cụm Phía Nam (Q7 - Nhà Bè - Bình Chánh)", new DateTime(2026, 10, 16, 0, 6, 8, 687, DateTimeKind.Local).AddTicks(2728), true, "Cần kiểm chứng (đang có đề án chuyển đổi cấp hành chính)", "79", "Thành phố Hồ Chí Minh", 11, "Nhà Bè" },
                    { 12, "HCM_NW", "HCM_SOU", "Cụm Phía Nam (Q7 - Nhà Bè - Bình Chánh)", new DateTime(2026, 10, 16, 0, 6, 8, 687, DateTimeKind.Local).AddTicks(2730), true, "Cần kiểm chứng (đang có đề án chuyển đổi cấp hành chính)", "79", "Thành phố Hồ Chí Minh", 12, "Bình Chánh" },
                    { 13, "LD_DUCTRONG", "LD_DALAT", "Cụm Đà Lạt", new DateTime(2026, 10, 16, 0, 6, 8, 687, DateTimeKind.Local).AddTicks(2732), true, "Cần kiểm chứng (đang sắp xếp mở rộng địa giới hành chính)", "68", "Tỉnh Lâm Đồng", 13, "Thành phố Đà Lạt" },
                    { 14, "LD_DALAT", "LD_DUCTRONG", "Cụm Đức Trọng", new DateTime(2026, 10, 16, 0, 6, 8, 687, DateTimeKind.Local).AddTicks(2734), true, "Cần kiểm chứng (khu vực Sân bay Liên Khương, có đề án lên thị xã)", "68", "Tỉnh Lâm Đồng", 14, "Huyện Đức Trọng" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_DeliveryZoneId",
                table: "Orders",
                column: "DeliveryZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyReconciliations_AdminApprovedUserId",
                table: "DailyReconciliations",
                column: "AdminApprovedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyReconciliations_CashierUserId",
                table: "DailyReconciliations",
                column: "CashierUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyReconciliations_Code",
                table: "DailyReconciliations",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DailyReconciliations_ReconciliationDate",
                table: "DailyReconciliations",
                column: "ReconciliationDate");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryTrips_CashierUserId",
                table: "DeliveryTrips",
                column: "CashierUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryTrips_CreatedByUserId",
                table: "DeliveryTrips",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryTrips_ShipperUserId",
                table: "DeliveryTrips",
                column: "ShipperUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryTrips_TripCode",
                table: "DeliveryTrips",
                column: "TripCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryTrips_TripDate_Shift_ClusterCode",
                table: "DeliveryTrips",
                columns: new[] { "TripDate", "Shift", "ClusterCode" });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryTripStops_DeliveryTripId",
                table: "DeliveryTripStops",
                column: "DeliveryTripId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryTripStops_OrderId",
                table: "DeliveryTripStops",
                column: "OrderId",
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryZones_ClusterCode",
                table: "DeliveryZones",
                column: "ClusterCode");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryZones_ProvinceName_ZoneName",
                table: "DeliveryZones",
                columns: new[] { "ProvinceName", "ZoneName" });

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_DeliveryZones_DeliveryZoneId",
                table: "Orders",
                column: "DeliveryZoneId",
                principalTable: "DeliveryZones",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_DeliveryZones_DeliveryZoneId",
                table: "Orders");

            migrationBuilder.DropTable(
                name: "DailyReconciliations");

            migrationBuilder.DropTable(
                name: "DeliveryTripStops");

            migrationBuilder.DropTable(
                name: "DeliveryZones");

            migrationBuilder.DropTable(
                name: "DeliveryTrips");

            migrationBuilder.DropIndex(
                name: "IX_Orders_DeliveryZoneId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Position",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DeliveryShift",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DeliveryStatus",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DeliveryZoneId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ExpectedDeliveryDate",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "FailedDeliveryAttempts",
                table: "Orders");
        }
    }
}
