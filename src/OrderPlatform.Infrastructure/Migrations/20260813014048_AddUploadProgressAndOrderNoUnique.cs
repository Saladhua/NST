using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUploadProgressAndOrderNoUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_order_main_OrderNo",
                table: "order_main");

            migrationBuilder.AddColumn<string>(
                name: "ErrorMessage",
                table: "upload_batch",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Progress",
                table: "upload_batch",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_order_main_OrderNo",
                table: "order_main",
                column: "OrderNo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_order_main_OrderNo",
                table: "order_main");

            migrationBuilder.DropColumn(
                name: "ErrorMessage",
                table: "upload_batch");

            migrationBuilder.DropColumn(
                name: "Progress",
                table: "upload_batch");

            migrationBuilder.CreateIndex(
                name: "IX_order_main_OrderNo",
                table: "order_main",
                column: "OrderNo");
        }
    }
}
