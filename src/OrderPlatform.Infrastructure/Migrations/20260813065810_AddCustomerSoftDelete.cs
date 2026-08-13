using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "customer",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "customer");
        }
    }
}
