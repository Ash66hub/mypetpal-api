using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace mypetpal.Migrations
{
    /// <inheritdoc />
    public partial class AddMiniGameScores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MiniGameScores",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    SaveTheJunkHighScore = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MiniGameScores", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_MiniGameScores_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MiniGameScores");
        }
    }
}
