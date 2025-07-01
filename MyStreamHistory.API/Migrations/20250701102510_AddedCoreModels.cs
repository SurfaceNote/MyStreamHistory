using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MyStreamHistory.API.Migrations
{
    /// <inheritdoc />
    public partial class AddedCoreModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Streams",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StreamId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Finished = table.Column<bool>(type: "boolean", nullable: false),
                    WithInfoAboutViewers = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Streams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Streams_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TwitchCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TwitchId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    BoxArtUrl = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TwitchCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Viewers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Login = table.Column<string>(type: "text", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: false),
                    TwitchId = table.Column<int>(type: "integer", nullable: false),
                    LogoUser = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BroadcasterType = table.Column<int>(type: "integer", nullable: false),
                    IsBot = table.Column<bool>(type: "boolean", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Viewers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TwitchCategoriesOnStream",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TwitchCategoryId = table.Column<int>(type: "integer", nullable: false),
                    StreamId = table.Column<int>(type: "integer", nullable: false),
                    Duration = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TwitchCategoriesOnStream", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TwitchCategoriesOnStream_Streams_StreamId",
                        column: x => x.StreamId,
                        principalTable: "Streams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TwitchCategoriesOnStream_TwitchCategories_TwitchCategoryId",
                        column: x => x.TwitchCategoryId,
                        principalTable: "TwitchCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ViewersPerTwitchCategoryOnStream",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TwitchCategoryOnStreamId = table.Column<int>(type: "integer", nullable: false),
                    ViewerId = table.Column<int>(type: "integer", nullable: false),
                    WatchedMinutes = table.Column<int>(type: "integer", nullable: false),
                    EarnedMsgPoints = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ViewersPerTwitchCategoryOnStream", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ViewersPerTwitchCategoryOnStream_TwitchCategoriesOnStream_T~",
                        column: x => x.TwitchCategoryOnStreamId,
                        principalTable: "TwitchCategoriesOnStream",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ViewersPerTwitchCategoryOnStream_Viewers_ViewerId",
                        column: x => x.ViewerId,
                        principalTable: "Viewers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Streams_StreamId",
                table: "Streams",
                column: "StreamId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Streams_UserId",
                table: "Streams",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TwitchCategories_TwitchId",
                table: "TwitchCategories",
                column: "TwitchId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TwitchCategoriesOnStream_StreamId",
                table: "TwitchCategoriesOnStream",
                column: "StreamId");

            migrationBuilder.CreateIndex(
                name: "IX_TwitchCategoriesOnStream_TwitchCategoryId",
                table: "TwitchCategoriesOnStream",
                column: "TwitchCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Viewers_TwitchId",
                table: "Viewers",
                column: "TwitchId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ViewersPerTwitchCategoryOnStream_TwitchCategoryOnStreamId",
                table: "ViewersPerTwitchCategoryOnStream",
                column: "TwitchCategoryOnStreamId");

            migrationBuilder.CreateIndex(
                name: "IX_ViewersPerTwitchCategoryOnStream_ViewerId",
                table: "ViewersPerTwitchCategoryOnStream",
                column: "ViewerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ViewersPerTwitchCategoryOnStream");

            migrationBuilder.DropTable(
                name: "TwitchCategoriesOnStream");

            migrationBuilder.DropTable(
                name: "Viewers");

            migrationBuilder.DropTable(
                name: "Streams");

            migrationBuilder.DropTable(
                name: "TwitchCategories");
        }
    }
}
