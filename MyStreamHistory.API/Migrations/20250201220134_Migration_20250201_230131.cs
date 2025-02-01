using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyStreamHistory.API.Migrations
{
    /// <inheritdoc />
    public partial class Migration_20250201_230131 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Streamers_TwitchId",
                table: "Streamers",
                column: "TwitchId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Streamers_TwitchId",
                table: "Streamers");
        }
    }
}
