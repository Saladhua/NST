using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSpecPartsAndSyncPushFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ErpPrdNo",
                table: "order_item",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ItemPushStatus",
                table: "order_item",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "NotPushed");

            migrationBuilder.AddColumn<string>(
                name: "Material",
                table: "order_item",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MaterialSyncStatus",
                table: "order_item",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "NotSynced");

            migrationBuilder.AddColumn<decimal>(
                name: "Module",
                table: "order_item",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OuterDiameter",
                table: "order_item",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PushedAt",
                table: "order_item",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShouKou",
                table: "order_item",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "SyncedAt",
                table: "order_item",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WallThickness",
                table: "order_item",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShouKou",
                table: "customer_part",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ErpPrdNo",
                table: "order_item");

            migrationBuilder.DropColumn(
                name: "ItemPushStatus",
                table: "order_item");

            migrationBuilder.DropColumn(
                name: "Material",
                table: "order_item");

            migrationBuilder.DropColumn(
                name: "MaterialSyncStatus",
                table: "order_item");

            migrationBuilder.DropColumn(
                name: "Module",
                table: "order_item");

            migrationBuilder.DropColumn(
                name: "OuterDiameter",
                table: "order_item");

            migrationBuilder.DropColumn(
                name: "PushedAt",
                table: "order_item");

            migrationBuilder.DropColumn(
                name: "ShouKou",
                table: "order_item");

            migrationBuilder.DropColumn(
                name: "SyncedAt",
                table: "order_item");

            migrationBuilder.DropColumn(
                name: "WallThickness",
                table: "order_item");

            migrationBuilder.DropColumn(
                name: "ShouKou",
                table: "customer_part");
        }
    }
}
