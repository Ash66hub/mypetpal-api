using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace mypetpal.Migrations
{
    /// <inheritdoc />
    public partial class RenamePublicIdColumnsToId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PublicId",
                table: "Users",
                newName: "Id");

            migrationBuilder.RenameIndex(
                name: "IX_Users_PublicId",
                table: "Users",
                newName: "IX_Users_Id");

            migrationBuilder.RenameColumn(
                name: "PublicId",
                table: "PetAttributes",
                newName: "Id");

            migrationBuilder.RenameIndex(
                name: "IX_PetAttributes_PublicId",
                table: "PetAttributes",
                newName: "IX_PetAttributes_Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Id",
                table: "Users",
                newName: "PublicId");

            migrationBuilder.RenameIndex(
                name: "IX_Users_Id",
                table: "Users",
                newName: "IX_Users_PublicId");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "PetAttributes",
                newName: "PublicId");

            migrationBuilder.RenameIndex(
                name: "IX_PetAttributes_Id",
                table: "PetAttributes",
                newName: "IX_PetAttributes_PublicId");
        }
    }
}
