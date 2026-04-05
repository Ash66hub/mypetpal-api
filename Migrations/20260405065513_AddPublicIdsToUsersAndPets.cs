using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace mypetpal.Migrations
{
    /// <inheritdoc />
    public partial class AddPublicIdsToUsersAndPets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PublicId",
                table: "Users",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PublicId",
                table: "PetAttributes",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE \"Users\" SET \"PublicId\" = 'usr_' || substring(md5(random()::text || clock_timestamp()::text || \"UserId\"::text), 1, 21) WHERE \"PublicId\" IS NULL;");

            migrationBuilder.Sql(
                "UPDATE \"PetAttributes\" SET \"PublicId\" = 'pet_' || substring(md5(random()::text || clock_timestamp()::text || \"PetId\"::text), 1, 21) WHERE \"PublicId\" IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "PublicId",
                table: "Users",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PublicId",
                table: "PetAttributes",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_PublicId",
                table: "Users",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PetAttributes_PublicId",
                table: "PetAttributes",
                column: "PublicId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_PublicId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_PetAttributes_PublicId",
                table: "PetAttributes");

            migrationBuilder.DropColumn(
                name: "PublicId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PublicId",
                table: "PetAttributes");
        }
    }
}
