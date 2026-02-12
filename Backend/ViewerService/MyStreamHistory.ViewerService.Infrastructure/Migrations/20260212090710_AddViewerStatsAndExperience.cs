using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyStreamHistory.ViewerService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddViewerStatsAndExperience : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Experience",
                table: "ViewerCategoryWatches",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "ViewerStats",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ViewerId = table.Column<Guid>(type: "uuid", nullable: false),
                    StreamerTwitchUserId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MinutesWatched = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    EarnedMsgPoints = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    Experience = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    LastUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ViewerStats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ViewerStats_Viewers_ViewerId",
                        column: x => x.ViewerId,
                        principalTable: "Viewers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ViewerStats_StreamerTwitchUserId",
                table: "ViewerStats",
                column: "StreamerTwitchUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ViewerStats_StreamerTwitchUserId_Experience",
                table: "ViewerStats",
                columns: new[] { "StreamerTwitchUserId", "Experience" });

            migrationBuilder.CreateIndex(
                name: "IX_ViewerStats_ViewerId",
                table: "ViewerStats",
                column: "ViewerId");

            migrationBuilder.CreateIndex(
                name: "IX_ViewerStats_ViewerId_StreamerTwitchUserId",
                table: "ViewerStats",
                columns: new[] { "ViewerId", "StreamerTwitchUserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ViewerStats");

            migrationBuilder.DropColumn(
                name: "Experience",
                table: "ViewerCategoryWatches");
        }
    }
}
