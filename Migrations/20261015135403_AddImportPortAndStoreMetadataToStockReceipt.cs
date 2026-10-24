using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeautyShop.Migrations
{
    /// <inheritdoc />
    public partial class AddImportPortAndStoreMetadataToStockReceipt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Region",
                table: "Stores",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StoreType",
                table: "Stores",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomsDeclarationNumber",
                table: "StockReceipts",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ImportPortId",
                table: "StockReceipts",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockReceipts_ImportPortId",
                table: "StockReceipts",
                column: "ImportPortId");

            migrationBuilder.AddForeignKey(
                name: "FK_StockReceipts_ImportPorts_ImportPortId",
                table: "StockReceipts",
                column: "ImportPortId",
                principalTable: "ImportPorts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StockReceipts_ImportPorts_ImportPortId",
                table: "StockReceipts");

            migrationBuilder.DropIndex(
                name: "IX_StockReceipts_ImportPortId",
                table: "StockReceipts");

            migrationBuilder.DropColumn(
                name: "Region",
                table: "Stores");

            migrationBuilder.DropColumn(
                name: "StoreType",
                table: "Stores");

            migrationBuilder.DropColumn(
                name: "CustomsDeclarationNumber",
                table: "StockReceipts");

            migrationBuilder.DropColumn(
                name: "ImportPortId",
                table: "StockReceipts");
        }
    }
}
