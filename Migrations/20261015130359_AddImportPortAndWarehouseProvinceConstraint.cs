using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace BeautyShop.Migrations
{
    /// <inheritdoc />
    public partial class AddImportPortAndWarehouseProvinceConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProvinceCode",
                table: "Stores",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProvinceName",
                table: "Stores",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceiptType",
                table: "StockReceipts",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "Domestic");

            migrationBuilder.AddColumn<int>(
                name: "StoreId",
                table: "StockReceipts",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            // Gán dữ liệu cho kho hiện có và phiếu nhập cũ mà không làm mất dữ liệu
            migrationBuilder.Sql("UPDATE \"Stores\" SET \"ProvinceName\" = 'Thành phố Hồ Chí Minh', \"ProvinceCode\" = '79' WHERE \"ProvinceName\" IS NULL OR \"ProvinceName\" = '';");
            migrationBuilder.Sql("UPDATE \"StockReceipts\" SET \"ReceiptType\" = 'Domestic' WHERE \"ReceiptType\" IS NULL OR \"ReceiptType\" = '';");
            migrationBuilder.Sql("UPDATE \"StockReceipts\" SET \"StoreId\" = 1 WHERE \"StoreId\" = 0;");

            migrationBuilder.CreateTable(
                name: "ImportPorts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PortCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    PortType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ProvinceName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ProvinceCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    AllowImport = table.Column<bool>(type: "INTEGER", nullable: false),
                    SourceReference = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportPorts", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "ImportPorts",
                columns: new[] { "Id", "AllowImport", "CreatedAt", "Name", "PortCode", "PortType", "ProvinceCode", "ProvinceName", "SourceReference", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Sân bay Quốc tế Tân Sơn Nhất", "SGN-AIR", "Air", "79", "Thành phố Hồ Chí Minh", "Quyết định 236/QĐ-TTg & Cục Hàng không Việt Nam", null },
                    { 2, true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Cảng Tân Cảng - Cát Lái", "SGN-SEA-CATLAI", "Sea", "79", "Thành phố Hồ Chí Minh", "Quyết định 1579/QĐ-TTg (Quy hoạch phát triển hệ thống cảng biển VN)", null },
                    { 3, true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Sân bay Quốc tế Nội Bài", "HAN-AIR", "Air", "01", "Thành phố Hà Nội", "Quyết định 236/QĐ-TTg & Cục Hàng không Việt Nam", null },
                    { 4, true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Cảng Quốc tế Lạch Huyện - Hải Phòng", "HPH-SEA-LACHHUYEN", "Sea", "31", "Thành phố Hải Phòng", "Quyết định 1579/QĐ-TTg (Cảng biển loại đặc biệt)", null },
                    { 5, true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Sân bay Quốc tế Cát Bi", "HPH-AIR", "Air", "31", "Thành phố Hải Phòng", "Cục Hàng không Việt Nam", null },
                    { 6, true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Sân bay Quốc tế Đà Nẵng", "DAD-AIR", "Air", "48", "Thành phố Đà Nẵng", "Cục Hàng không Việt Nam", null },
                    { 7, true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Cảng Tiên Sa - Đà Nẵng", "DAD-SEA-TIENSA", "Sea", "48", "Thành phố Đà Nẵng", "Quyết định 1579/QĐ-TTg (Cảng biển loại I)", null },
                    { 8, true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Cụm Cảng Cái Mép - Thị Vải", "VTU-SEA-CAIMEP", "Sea", "77", "Tỉnh Bà Rịa - Vũng Tàu", "Quyết định 1579/QĐ-TTg (Cảng biển loại đặc biệt cửa ngõ quốc tế)", null },
                    { 9, true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Sân bay Quốc tế Cam Ranh", "CXR-AIR", "Air", "56", "Tỉnh Khánh Hòa", "Cục Hàng không Việt Nam", null },
                    { 10, true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Cảng Quy Nhơn", "UIH-SEA-QUYNHON", "Sea", "52", "Tỉnh Bình Định", "Quyết định 1579/QĐ-TTg (Cảng biển loại I)", null },
                    { 11, true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Sân bay Quốc tế Cần Thơ", "VCA-AIR", "Air", "92", "Thành phố Cần Thơ", "Cục Hàng không Việt Nam", null },
                    { 12, true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Sân bay Quốc tế Phú Quốc", "PQC-AIR", "Air", "91", "Tỉnh Kiên Giang", "Cục Hàng không Việt Nam", null },
                    { 13, true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Sân bay Quốc tế Vân Đồn", "VDO-AIR", "Air", "22", "Tỉnh Quảng Ninh", "Quyết định 236/QĐ-TTg & Cục Hàng không Việt Nam", null },
                    { 14, true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Cảng Cái Lân - Quảng Ninh", "QNH-SEA-CAILAN", "Sea", "22", "Tỉnh Quảng Ninh", "Quyết định 1579/QĐ-TTg (Cảng biển loại I)", null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockReceipts_StoreId",
                table: "StockReceipts",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportPorts_PortCode",
                table: "ImportPorts",
                column: "PortCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImportPorts_ProvinceCode",
                table: "ImportPorts",
                column: "ProvinceCode");

            migrationBuilder.CreateIndex(
                name: "IX_ImportPorts_ProvinceName",
                table: "ImportPorts",
                column: "ProvinceName");

            migrationBuilder.AddForeignKey(
                name: "FK_StockReceipts_Stores_StoreId",
                table: "StockReceipts",
                column: "StoreId",
                principalTable: "Stores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StockReceipts_Stores_StoreId",
                table: "StockReceipts");

            migrationBuilder.DropTable(
                name: "ImportPorts");

            migrationBuilder.DropIndex(
                name: "IX_StockReceipts_StoreId",
                table: "StockReceipts");

            migrationBuilder.DropColumn(
                name: "ProvinceCode",
                table: "Stores");

            migrationBuilder.DropColumn(
                name: "ProvinceName",
                table: "Stores");

            migrationBuilder.DropColumn(
                name: "ReceiptType",
                table: "StockReceipts");

            migrationBuilder.DropColumn(
                name: "StoreId",
                table: "StockReceipts");
        }
    }
}
