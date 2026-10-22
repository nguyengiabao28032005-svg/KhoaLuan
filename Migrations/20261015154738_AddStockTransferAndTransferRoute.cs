using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace BeautyShop.Migrations
{
    /// <inheritdoc />
    public partial class AddStockTransferAndTransferRoute : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "InTransitQuantity",
                table: "Inventories",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "StockTransfers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TransferCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    BatchCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    LegIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    FromStoreId = table.Column<int>(type: "INTEGER", nullable: false),
                    ToStoreId = table.Column<int>(type: "INTEGER", nullable: false),
                    FinalDestinationStoreId = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedByName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ReceivedByUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    ReceivedByName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CancelledReason = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    Note = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockTransfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockTransfers_Stores_FinalDestinationStoreId",
                        column: x => x.FinalDestinationStoreId,
                        principalTable: "Stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTransfers_Stores_FromStoreId",
                        column: x => x.FromStoreId,
                        principalTable: "Stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTransfers_Stores_ToStoreId",
                        column: x => x.ToStoreId,
                        principalTable: "Stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTransfers_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_StockTransfers_Users_ReceivedByUserId",
                        column: x => x.ReceivedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TransferRoutes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FromStoreId = table.Column<int>(type: "INTEGER", nullable: false),
                    ToStoreId = table.Column<int>(type: "INTEGER", nullable: false),
                    TransitStoreId = table.Column<int>(type: "INTEGER", nullable: true),
                    DistanceKm = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    EstimatedHours = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransferRoutes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransferRoutes_Stores_FromStoreId",
                        column: x => x.FromStoreId,
                        principalTable: "Stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TransferRoutes_Stores_ToStoreId",
                        column: x => x.ToStoreId,
                        principalTable: "Stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TransferRoutes_Stores_TransitStoreId",
                        column: x => x.TransitStoreId,
                        principalTable: "Stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockTransferLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StockTransferId = table.Column<int>(type: "INTEGER", nullable: false),
                    VariantId = table.Column<int>(type: "INTEGER", nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockTransferLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockTransferLines_ProductVariants_VariantId",
                        column: x => x.VariantId,
                        principalTable: "ProductVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTransferLines_StockTransfers_StockTransferId",
                        column: x => x.StockTransferId,
                        principalTable: "StockTransfers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "TransferRoutes",
                columns: new[] { "Id", "CreatedAt", "Description", "DistanceKm", "EstimatedHours", "FromStoreId", "IsActive", "ToStoreId", "TransitStoreId" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Hà Nội đi Hub Đà Nẵng (trực tiếp)", 760m, 14m, 3, true, 5, null },
                    { 2, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Hải Phòng đi Hub Đà Nẵng (trực tiếp)", 800m, 15m, 4, true, 5, null },
                    { 3, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Hà Nội đi TP.HCM (qua Hub Đà Nẵng)", 1720m, 32m, 3, true, 1, 5 },
                    { 4, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Hà Nội đi Tây Nguyên - Lâm Đồng (qua Hub Đà Nẵng)", 1420m, 28m, 3, true, 2, 5 },
                    { 5, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Hà Nội đi Cần Thơ (qua Hub Đà Nẵng)", 1890m, 35m, 3, true, 6, 5 },
                    { 6, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Hải Phòng đi TP.HCM (qua Hub Đà Nẵng)", 1760m, 33m, 4, true, 1, 5 },
                    { 7, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Hải Phòng đi Tây Nguyên - Lâm Đồng (qua Hub Đà Nẵng)", 1460m, 29m, 4, true, 2, 5 },
                    { 8, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Hải Phòng đi Cần Thơ (qua Hub Đà Nẵng)", 1930m, 36m, 4, true, 6, 5 },
                    { 9, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Hub Đà Nẵng đi Kho tổng TP.HCM", 960m, 18m, 5, true, 1, null },
                    { 10, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Hub Đà Nẵng rẽ đi Kho Tây Nguyên (Lâm Đồng)", 660m, 14m, 5, true, 2, null },
                    { 11, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Hub Đà Nẵng rẽ đi Kho Tây Nam Bộ (Cần Thơ)", 1130m, 21m, 5, true, 6, null },
                    { 12, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Hub Đà Nẵng điều phối về Kho tổng Hà Nội", 760m, 14m, 5, true, 3, null },
                    { 13, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Hub Đà Nẵng điều phối về Kho cảng Hải Phòng", 800m, 15m, 5, true, 4, null },
                    { 14, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "TP.HCM đi Cần Thơ (trực tiếp)", 170m, 3.5m, 1, true, 6, null },
                    { 15, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "TP.HCM đi Lâm Đồng (trực tiếp)", 300m, 6m, 1, true, 2, null },
                    { 16, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Cần Thơ về TP.HCM (trực tiếp)", 170m, 3.5m, 6, true, 1, null },
                    { 17, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Lâm Đồng về TP.HCM (trực tiếp)", 300m, 6m, 2, true, 1, null },
                    { 18, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "TP.HCM đi Hub Đà Nẵng (trực tiếp)", 960m, 18m, 1, true, 5, null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockTransferLines_StockTransferId",
                table: "StockTransferLines",
                column: "StockTransferId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransferLines_VariantId",
                table: "StockTransferLines",
                column: "VariantId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_BatchCode",
                table: "StockTransfers",
                column: "BatchCode");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_CreatedByUserId",
                table: "StockTransfers",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_FinalDestinationStoreId",
                table: "StockTransfers",
                column: "FinalDestinationStoreId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_FromStoreId",
                table: "StockTransfers",
                column: "FromStoreId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_ReceivedByUserId",
                table: "StockTransfers",
                column: "ReceivedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_ToStoreId",
                table: "StockTransfers",
                column: "ToStoreId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_TransferCode",
                table: "StockTransfers",
                column: "TransferCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransferRoutes_FromStoreId",
                table: "TransferRoutes",
                column: "FromStoreId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferRoutes_ToStoreId",
                table: "TransferRoutes",
                column: "ToStoreId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferRoutes_TransitStoreId",
                table: "TransferRoutes",
                column: "TransitStoreId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StockTransferLines");

            migrationBuilder.DropTable(
                name: "TransferRoutes");

            migrationBuilder.DropTable(
                name: "StockTransfers");

            migrationBuilder.DropColumn(
                name: "InTransitQuantity",
                table: "Inventories");
        }
    }
}
