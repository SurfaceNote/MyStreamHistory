using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyStreamHistory.TwitchTrackingService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StreamId",
                table: "StreamSessions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TwitchCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TwitchId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BoxArtUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IgdbId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TwitchCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StreamCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StreamSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TwitchCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StreamCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StreamCategories_StreamSessions_StreamSessionId",
                        column: x => x.StreamSessionId,
                        principalTable: "StreamSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StreamCategories_TwitchCategories_TwitchCategoryId",
                        column: x => x.TwitchCategoryId,
                        principalTable: "TwitchCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StreamSessions_StreamId",
                table: "StreamSessions",
                column: "StreamId");

            migrationBuilder.CreateIndex(
                name: "IX_StreamCategories_StreamSessionId_EndedAt",
                table: "StreamCategories",
                columns: new[] { "StreamSessionId", "EndedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StreamCategories_StreamSessionId_StartedAt",
                table: "StreamCategories",
                columns: new[] { "StreamSessionId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StreamCategories_TwitchCategoryId",
                table: "StreamCategories",
                column: "TwitchCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_TwitchCategories_TwitchId",
                table: "TwitchCategories",
                column: "TwitchId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StreamCategories");

            migrationBuilder.DropTable(
                name: "TwitchCategories");

            migrationBuilder.DropIndex(
                name: "IX_StreamSessions_StreamId",
                table: "StreamSessions");

            migrationBuilder.DropColumn(
                name: "StreamId",
                table: "StreamSessions");
        }
    }
}
