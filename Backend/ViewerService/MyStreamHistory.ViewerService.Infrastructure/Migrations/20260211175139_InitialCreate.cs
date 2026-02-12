using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyStreamHistory.ViewerService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProcessedEventSubMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EventType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessedEventSubMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Viewers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TwitchUserId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Login = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Viewers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ViewerCategoryWatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ViewerId = table.Column<Guid>(type: "uuid", nullable: false),
                    StreamCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    MinutesWatched = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ChatPoints = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    LastUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ViewerCategoryWatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ViewerCategoryWatches_Viewers_ViewerId",
                        column: x => x.ViewerId,
                        principalTable: "Viewers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedEventSubMessages_MessageId",
                table: "ProcessedEventSubMessages",
                column: "MessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedEventSubMessages_ProcessedAt",
                table: "ProcessedEventSubMessages",
                column: "ProcessedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ViewerCategoryWatches_StreamCategoryId",
                table: "ViewerCategoryWatches",
                column: "StreamCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ViewerCategoryWatches_ViewerId",
                table: "ViewerCategoryWatches",
                column: "ViewerId");

            migrationBuilder.CreateIndex(
                name: "IX_ViewerCategoryWatches_ViewerId_StreamCategoryId",
                table: "ViewerCategoryWatches",
                columns: new[] { "ViewerId", "StreamCategoryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Viewers_TwitchUserId",
                table: "Viewers",
                column: "TwitchUserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProcessedEventSubMessages");

            migrationBuilder.DropTable(
                name: "ViewerCategoryWatches");

            migrationBuilder.DropTable(
                name: "Viewers");
        }
    }
}
