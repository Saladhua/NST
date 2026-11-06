using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSummaryFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Summary",
                table: "order_item",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Summary",
                table: "customer_part",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Summary",
                table: "order_item");

            migrationBuilder.DropColumn(
                name: "Summary",
                table: "customer_part");
        }
    }
}
