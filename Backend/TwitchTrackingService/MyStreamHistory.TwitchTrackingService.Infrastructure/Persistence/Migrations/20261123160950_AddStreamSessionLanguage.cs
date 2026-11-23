using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyStreamHistory.TwitchTrackingService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStreamSessionLanguage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "StreamSessions",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Language",
                table: "StreamSessions");
        }
    }
}
