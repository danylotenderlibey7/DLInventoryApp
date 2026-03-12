using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DLInventoryApp.Migrations
{
    /// <inheritdoc />
    public partial class UseXminConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Version",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Inventories");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "CustomFields");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "Version",
                table: "Items",
                type: "bytea",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "Version",
                table: "Inventories",
                type: "bytea",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "Version",
                table: "CustomFields",
                type: "bytea",
                rowVersion: true,
                nullable: true);
        }
    }
}