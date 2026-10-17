using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyStreamHistory.TwitchTrackingService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAudienceSamples : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ViewerCountSamples",
                columns: table => new
                {
                    StreamSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Minute = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ObservedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ViewerCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ViewerCountSamples", x => new { x.StreamSessionId, x.Minute });
                    table.ForeignKey(
                        name: "FK_ViewerCountSamples_StreamSessions_StreamSessionId",
                        column: x => x.StreamSessionId,
                        principalTable: "StreamSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ViewerCountSamples");
        }
    }
}

