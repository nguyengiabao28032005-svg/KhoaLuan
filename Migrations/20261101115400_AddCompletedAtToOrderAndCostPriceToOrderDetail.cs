using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeautyShop.Migrations
{
    /// <inheritdoc />
    public partial class AddCompletedAtToOrderAndCostPriceToOrderDetail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "Orders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CostPrice",
                table: "OrderDetails",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.UpdateData(
                table: "DeliveryZones",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 11, 1, 18, 53, 59, 194, DateTimeKind.Local).AddTicks(7326));

            migrationBuilder.UpdateData(
                table: "DeliveryZones",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 11, 1, 18, 53, 59, 194, DateTimeKind.Local).AddTicks(7334));

            migrationBuilder.UpdateData(
                table: "DeliveryZones",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 11, 1, 18, 53, 59, 194, DateTimeKind.Local).AddTicks(7336));

            migrationBuilder.UpdateData(
                table: "DeliveryZones",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 11, 1, 18, 53, 59, 194, DateTimeKind.Local).AddTicks(7338));

            migrationBuilder.UpdateData(
                table: "DeliveryZones",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 11, 1, 18, 53, 59, 194, DateTimeKind.Local).AddTicks(7339));

            migrationBuilder.UpdateData(
                table: "DeliveryZones",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 11, 1, 18, 53, 59, 194, DateTimeKind.Local).AddTicks(7341));

            migrationBuilder.UpdateData(
                table: "DeliveryZones",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2026, 11, 1, 18, 53, 59, 194, DateTimeKind.Local).AddTicks(7342));

            migrationBuilder.UpdateData(
                table: "DeliveryZones",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2026, 11, 1, 18, 53, 59, 194, DateTimeKind.Local).AddTicks(7344));

            migrationBuilder.UpdateData(
                table: "DeliveryZones",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedAt",
                value: new DateTime(2026, 11, 1, 18, 53, 59, 194, DateTimeKind.Local).AddTicks(7345));

            migrationBuilder.UpdateData(
                table: "DeliveryZones",
                keyColumn: "Id",
                keyValue: 10,
                column: "CreatedAt",
                value: new DateTime(2026, 11, 1, 18, 53, 59, 194, DateTimeKind.Local).AddTicks(7347));

            migrationBuilder.UpdateData(
                table: "DeliveryZones",
                keyColumn: "Id",
                keyValue: 11,
                column: "CreatedAt",
                value: new DateTime(2026, 11, 1, 18, 53, 59, 194, DateTimeKind.Local).AddTicks(7348));

            migrationBuilder.UpdateData(
                table: "DeliveryZones",
                keyColumn: "Id",
                keyValue: 12,
                column: "CreatedAt",
                value: new DateTime(2026, 11, 1, 18, 53, 59, 194, DateTimeKind.Local).AddTicks(7350));

            migrationBuilder.UpdateData(
                table: "DeliveryZones",
                keyColumn: "Id",
                keyValue: 13,
                column: "CreatedAt",
                value: new DateTime(2026, 11, 1, 18, 53, 59, 194, DateTimeKind.Local).AddTicks(7351));

            migrationBuilder.UpdateData(
                table: "DeliveryZones",
                keyColumn: "Id",
                keyValue: 14,
                column: "CreatedAt",
                value: new DateTime(2026, 11, 1, 18, 53, 59, 194, DateTimeKind.Local).AddTicks(7353));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CostPrice",
                table: "OrderDetails");

            migrationBuilder.UpdateData(
                table: "DeliveryZones",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 10, 31, 23, 46, 4, 114, DateTimeKind.Local).AddTicks(7516));

            migrationBuilder.UpdateData(
                table: "DeliveryZones",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 10, 31, 23, 46, 4, 114, DateTimeKind.Local).AddTicks(7521));

            migrationBuilder.UpdateData(
                table: "DeliveryZones",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 10, 31, 23, 46, 4, 114, DateTimeKind.Local).AddTicks(7523));

            migrationBuilder.UpdateData(
                table: "DeliveryZones",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 10, 31, 23, 46, 4, 114, DateTimeKind.Local).AddTicks(7525));

            migrationBuilder.UpdateData(
                table: "DeliveryZones",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 10, 31, 23, 46, 4, 114, DateTimeKind.Local).AddTicks(7527));

            migrationBuilder.UpdateData(
                table: "DeliveryZones",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 10, 31, 23, 46, 4, 114, DateTimeKind.Local).AddTicks(7528));

            migrationBuilder.UpdateData(
                table: "DeliveryZones",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2026, 10, 31, 23, 46, 4, 114, DateTimeKind.Local).AddTicks(7530));

            migrationBuilder.UpdateData(
                table: "DeliveryZones",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2026, 10, 31, 23, 46, 4, 114, DateTimeKind.Local).AddTicks(7531));

            migrationBuilder.UpdateData(
                table: "DeliveryZones",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedAt",
                value: new DateTime(2026, 10, 31, 23, 46, 4, 114, DateTimeKind.Local).AddTicks(7533));

            migrationBuilder.UpdateData(
                table: "DeliveryZones",
                keyColumn: "Id",
                keyValue: 10,
                column: "CreatedAt",
                value: new DateTime(2026, 10, 31, 23, 46, 4, 114, DateTimeKind.Local).AddTicks(7534));

            migrationBuilder.UpdateData(
                table: "DeliveryZones",
                keyColumn: "Id",
                keyValue: 11,
                column: "CreatedAt",
                value: new DateTime(2026, 10, 31, 23, 46, 4, 114, DateTimeKind.Local).AddTicks(7536));

            migrationBuilder.UpdateData(
                table: "DeliveryZones",
                keyColumn: "Id",
                keyValue: 12,
                column: "CreatedAt",
                value: new DateTime(2026, 10, 31, 23, 46, 4, 114, DateTimeKind.Local).AddTicks(7537));

            migrationBuilder.UpdateData(
                table: "DeliveryZones",
                keyColumn: "Id",
                keyValue: 13,
                column: "CreatedAt",
                value: new DateTime(2026, 10, 31, 23, 46, 4, 114, DateTimeKind.Local).AddTicks(7539));

            migrationBuilder.UpdateData(
                table: "DeliveryZones",
                keyColumn: "Id",
                keyValue: 14,
                column: "CreatedAt",
                value: new DateTime(2026, 10, 31, 23, 46, 4, 114, DateTimeKind.Local).AddTicks(7541));
        }
    }
}
