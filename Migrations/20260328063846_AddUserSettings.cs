using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace mypetpal.Migrations
{
    /// <inheritdoc />
    public partial class AddUserSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserSettings",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    LastPetX = table.Column<float>(type: "float", nullable: false),
                    LastPetY = table.Column<float>(type: "float", nullable: false),
                    ZoomLevel = table.Column<float>(type: "float", nullable: false),
                    IsMuted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    MusicVolume = table.Column<float>(type: "float", nullable: false),
                    SoundVolume = table.Column<float>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSettings", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_UserSettings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserSettings");
        }
    }
}
