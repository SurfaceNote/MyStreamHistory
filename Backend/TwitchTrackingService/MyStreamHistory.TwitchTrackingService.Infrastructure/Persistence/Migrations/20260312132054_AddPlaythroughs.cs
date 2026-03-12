using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyStreamHistory.TwitchTrackingService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlaythroughs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Playthroughs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TwitchUserId = table.Column<int>(type: "integer", nullable: false),
                    TwitchCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    AutoAddNewStreams = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Playthroughs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Playthroughs_TwitchCategories_TwitchCategoryId",
                        column: x => x.TwitchCategoryId,
                        principalTable: "TwitchCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlaythroughStreamCategories",
                columns: table => new
                {
                    PlaythroughId = table.Column<Guid>(type: "uuid", nullable: false),
                    StreamCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaythroughStreamCategories", x => new { x.PlaythroughId, x.StreamCategoryId });
                    table.ForeignKey(
                        name: "FK_PlaythroughStreamCategories_Playthroughs_PlaythroughId",
                        column: x => x.PlaythroughId,
                        principalTable: "Playthroughs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlaythroughStreamCategories_StreamCategories_StreamCategory~",
                        column: x => x.StreamCategoryId,
                        principalTable: "StreamCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Playthroughs_TwitchCategoryId",
                table: "Playthroughs",
                column: "TwitchCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Playthroughs_TwitchUserId",
                table: "Playthroughs",
                column: "TwitchUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Playthroughs_TwitchUserId_TwitchCategoryId",
                table: "Playthroughs",
                columns: new[] { "TwitchUserId", "TwitchCategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlaythroughStreamCategories_StreamCategoryId",
                table: "PlaythroughStreamCategories",
                column: "StreamCategoryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlaythroughStreamCategories");

            migrationBuilder.DropTable(
                name: "Playthroughs");
        }
    }
}
