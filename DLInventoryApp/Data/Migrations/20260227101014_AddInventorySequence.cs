using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DLInventoryApp.Migrations
{
    /// <inheritdoc />
    public partial class AddInventorySequence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InventorySequences",
                columns: table => new
                {
                    InventoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    NextValue = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventorySequences", x => x.InventoryId);
                    table.ForeignKey(
                        name: "FK_InventorySequences_Inventories_InventoryId",
                        column: x => x.InventoryId,
                        principalTable: "Inventories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InventorySequences");
        }
    }
}
