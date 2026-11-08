using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyStreamHistory.TwitchTrackingService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddChannelActivityDays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChannelActivityDays",
                columns: table => new
                {
                    TwitchUserId = table.Column<int>(type: "integer", nullable: false),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    FirstSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChannelActivityDays", x => new { x.TwitchUserId, x.Day, x.Kind });
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChannelActivityDays_Day_Kind",
                table: "ChannelActivityDays",
                columns: new[] { "Day", "Kind" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChannelActivityDays");
        }
    }
}
