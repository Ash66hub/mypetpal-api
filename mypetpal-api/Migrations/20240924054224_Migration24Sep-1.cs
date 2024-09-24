using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace mypetpalapi.Migrations
{
    /// <inheritdoc />
    public partial class Migration24Sep1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Metadata",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Metadata",
                table: "PetAttributes",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Metadata",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Metadata",
                table: "PetAttributes");
        }
    }
}
