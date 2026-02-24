using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DLInventoryApp.Migrations
{
    /// <inheritdoc />
    public partial class ChangeDiscussionPosts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DiscussionPost_AspNetUsers_AuthorId",
                table: "DiscussionPost");

            migrationBuilder.DropForeignKey(
                name: "FK_DiscussionPost_Inventories_InventoryId",
                table: "DiscussionPost");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DiscussionPost",
                table: "DiscussionPost");

            migrationBuilder.RenameTable(
                name: "DiscussionPost",
                newName: "DiscussionPosts");

            migrationBuilder.RenameIndex(
                name: "IX_DiscussionPost_InventoryId",
                table: "DiscussionPosts",
                newName: "IX_DiscussionPosts_InventoryId");

            migrationBuilder.RenameIndex(
                name: "IX_DiscussionPost_AuthorId",
                table: "DiscussionPosts",
                newName: "IX_DiscussionPosts_AuthorId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DiscussionPosts",
                table: "DiscussionPosts",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_DiscussionPosts_AspNetUsers_AuthorId",
                table: "DiscussionPosts",
                column: "AuthorId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_DiscussionPosts_Inventories_InventoryId",
                table: "DiscussionPosts",
                column: "InventoryId",
                principalTable: "Inventories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DiscussionPosts_AspNetUsers_AuthorId",
                table: "DiscussionPosts");

            migrationBuilder.DropForeignKey(
                name: "FK_DiscussionPosts_Inventories_InventoryId",
                table: "DiscussionPosts");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DiscussionPosts",
                table: "DiscussionPosts");

            migrationBuilder.RenameTable(
                name: "DiscussionPosts",
                newName: "DiscussionPost");

            migrationBuilder.RenameIndex(
                name: "IX_DiscussionPosts_InventoryId",
                table: "DiscussionPost",
                newName: "IX_DiscussionPost_InventoryId");

            migrationBuilder.RenameIndex(
                name: "IX_DiscussionPosts_AuthorId",
                table: "DiscussionPost",
                newName: "IX_DiscussionPost_AuthorId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DiscussionPost",
                table: "DiscussionPost",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_DiscussionPost_AspNetUsers_AuthorId",
                table: "DiscussionPost",
                column: "AuthorId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_DiscussionPost_Inventories_InventoryId",
                table: "DiscussionPost",
                column: "InventoryId",
                principalTable: "Inventories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
