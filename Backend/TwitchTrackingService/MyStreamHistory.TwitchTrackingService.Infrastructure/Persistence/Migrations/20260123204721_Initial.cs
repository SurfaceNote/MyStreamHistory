using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyStreamHistory.TwitchTrackingService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EventSubSubscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TwitchSubscriptionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TwitchUserId = table.Column<int>(type: "integer", nullable: false),
                    EventType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventSubSubscriptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StreamSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TwitchUserId = table.Column<int>(type: "integer", nullable: false),
                    StreamerLogin = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StreamerDisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsLive = table.Column<bool>(type: "boolean", nullable: false),
                    StreamTitle = table.Column<string>(type: "text", nullable: true),
                    GameName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ViewerCount = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StreamSessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EventSubSubscriptions_TwitchSubscriptionId",
                table: "EventSubSubscriptions",
                column: "TwitchSubscriptionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EventSubSubscriptions_TwitchUserId",
                table: "EventSubSubscriptions",
                column: "TwitchUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StreamSessions_IsLive",
                table: "StreamSessions",
                column: "IsLive");

            migrationBuilder.CreateIndex(
                name: "IX_StreamSessions_TwitchUserId",
                table: "StreamSessions",
                column: "TwitchUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventSubSubscriptions");

            migrationBuilder.DropTable(
                name: "StreamSessions");
        }
    }
}
