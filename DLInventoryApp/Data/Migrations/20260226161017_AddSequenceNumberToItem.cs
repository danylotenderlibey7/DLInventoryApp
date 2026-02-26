using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DLInventoryApp.Migrations
{
    /// <inheritdoc />
    public partial class AddSequenceNumberToItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SequenceNumber",
                table: "Items",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SequenceNumber",
                table: "Items");
        }
    }
}
