using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DLInventoryApp.Migrations
{
    /// <inheritdoc />
    public partial class UpdateCustomFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsRequired",
                table: "CustomFields");

            migrationBuilder.RenameColumn(
                name: "IsUnique",
                table: "CustomFields",
                newName: "ShowInTable");

            migrationBuilder.AlterColumn<string>(
                name: "CustomId",
                table: "Items",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "CustomFields",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                table: "CustomFields");

            migrationBuilder.RenameColumn(
                name: "ShowInTable",
                table: "CustomFields",
                newName: "IsUnique");

            migrationBuilder.AlterColumn<string>(
                name: "CustomId",
                table: "Items",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<bool>(
                name: "IsRequired",
                table: "CustomFields",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
