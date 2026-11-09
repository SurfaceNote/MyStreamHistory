using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyStreamHistory.ViewerService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CommunitySegmentsPerBroadcast : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommunityDailyStates");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastActivityAt",
                table: "CommunitySettings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CommunitySessionSummaries",
                columns: table => new
                {
                    StreamerTwitchUserId = table.Column<string>(type: "text", nullable: false),
                    StreamSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpectedMinutes = table.Column<int>(type: "integer", nullable: false),
                    CompleteMinutes = table.Column<int>(type: "integer", nullable: false),
                    LongestGap = table.Column<int>(type: "integer", nullable: false),
                    Legacy = table.Column<bool>(type: "boolean", nullable: false),
                    AggregatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunitySessionSummaries", x => new { x.StreamerTwitchUserId, x.StreamSessionId });
                });

            migrationBuilder.CreateTable(
                name: "CommunitySessionVisits",
                columns: table => new
                {
                    StreamerTwitchUserId = table.Column<string>(type: "text", nullable: false),
                    StreamSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ViewerTwitchUserId = table.Column<string>(type: "text", nullable: false),
                    Minutes = table.Column<int>(type: "integer", nullable: false),
                    LegacyChat = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunitySessionVisits", x => new { x.StreamerTwitchUserId, x.StreamSessionId, x.ViewerTwitchUserId });
                });

            // Cached v1 reports have a different shape; every channel is rebuilt with the per-broadcast method.
            migrationBuilder.Sql("""
                UPDATE "CommunitySettings" SET "ReportJson" = NULL, "MembersJson" = NULL, "Pending" = TRUE, "Revision" = "Revision" + 1;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommunitySessionSummaries");

            migrationBuilder.DropTable(
                name: "CommunitySessionVisits");

            migrationBuilder.DropColumn(
                name: "LastActivityAt",
                table: "CommunitySettings");

            migrationBuilder.CreateTable(
                name: "CommunityDailyStates",
                columns: table => new
                {
                    StreamerTwitchUserId = table.Column<string>(type: "text", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    MethodVersion = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    ViewerTwitchUserId = table.Column<string>(type: "text", nullable: false),
                    Level = table.Column<string>(type: "text", nullable: false),
                    Quality = table.Column<string>(type: "text", nullable: false),
                    Transition = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityDailyStates", x => new { x.StreamerTwitchUserId, x.Revision, x.MethodVersion, x.Date, x.ViewerTwitchUserId });
                });
        }
    }
}
