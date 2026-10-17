using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyStreamHistory.ViewerService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCommunityAnalytics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.CreateTable(
                name: "CommunityExclusions",
                columns: table => new
                {
                    StreamerTwitchUserId = table.Column<string>(type: "text", nullable: false),
                    ViewerTwitchUserId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityExclusions", x => new { x.StreamerTwitchUserId, x.ViewerTwitchUserId });
                });

            migrationBuilder.CreateTable(
                name: "CommunityMessageAggregates",
                columns: table => new
                {
                    StreamerTwitchUserId = table.Column<string>(type: "text", nullable: false),
                    StreamSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ViewerTwitchUserId = table.Column<string>(type: "text", nullable: false),
                    Count = table.Column<int>(type: "integer", nullable: false),
                    Provisional = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityMessageAggregates", x => new { x.StreamerTwitchUserId, x.StreamSessionId, x.ViewerTwitchUserId });
                });

            migrationBuilder.CreateTable(
                name: "CommunityMessageReceipts",
                columns: table => new
                {
                    StreamerTwitchUserId = table.Column<string>(type: "text", nullable: false),
                    MessageId = table.Column<string>(type: "text", nullable: false),
                    StreamSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ViewerTwitchUserId = table.Column<string>(type: "text", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Provisional = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityMessageReceipts", x => new { x.StreamerTwitchUserId, x.MessageId });
                });

            migrationBuilder.CreateTable(
                name: "CommunityPolls",
                columns: table => new
                {
                    StreamerTwitchUserId = table.Column<string>(type: "text", nullable: false),
                    Minute = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StreamSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ObservedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Complete = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityPolls", x => new { x.StreamerTwitchUserId, x.Minute });
                });

            migrationBuilder.CreateTable(
                name: "CommunityPresences",
                columns: table => new
                {
                    StreamerTwitchUserId = table.Column<string>(type: "text", nullable: false),
                    ViewerTwitchUserId = table.Column<string>(type: "text", nullable: false),
                    Minute = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StreamSessionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityPresences", x => new { x.StreamerTwitchUserId, x.ViewerTwitchUserId, x.Minute });
                });

            migrationBuilder.CreateTable(
                name: "CommunitySettings",
                columns: table => new
                {
                    StreamerTwitchUserId = table.Column<string>(type: "text", nullable: false),
                    TimeZoneId = table.Column<string>(type: "text", nullable: false),
                    TimeZoneConfigured = table.Column<bool>(type: "boolean", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    Pending = table.Column<bool>(type: "boolean", nullable: false),
                    TrackingStartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CalculatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReportJson = table.Column<string>(type: "jsonb", nullable: true),
                    MembersJson = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunitySettings", x => x.StreamerTwitchUserId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommunityPolls_StreamSessionId",
                table: "CommunityPolls",
                column: "StreamSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityPresences_StreamerTwitchUserId_StreamSessionId",
                table: "CommunityPresences",
                columns: new[] { "StreamerTwitchUserId", "StreamSessionId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommunityDailyStates");

            migrationBuilder.DropTable(
                name: "CommunityExclusions");

            migrationBuilder.DropTable(
                name: "CommunityMessageAggregates");

            migrationBuilder.DropTable(
                name: "CommunityMessageReceipts");

            migrationBuilder.DropTable(
                name: "CommunityPolls");

            migrationBuilder.DropTable(
                name: "CommunityPresences");

            migrationBuilder.DropTable(
                name: "CommunitySettings");
        }
    }
}
