using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyStreamHistory.API.Migrations
{
    /// <inheritdoc />
    public partial class AddNewFieldToStreamer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TwitchId",
                table: "Streamers",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TwitchId",
                table: "Streamers");
        }
    }
}
