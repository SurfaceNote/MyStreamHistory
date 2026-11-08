using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyStreamHistory.ViewerService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRankingExcludedAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RankingExcludedAccounts",
                columns: table => new
                {
                    Login = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: false),
                    AddedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AddedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RankingExcludedAccounts", x => x.Login);
                });

            // Seed the list that was previously hard-coded; admins manage it from here on.
            // Kept literal so that this migration never changes when the defaults in code do.
            var seededAt = new DateTime(2026, 11, 9, 0, 0, 0, DateTimeKind.Utc);
            string[] defaultBotLogins =
            [
                "nightbot",
                "streamelements",
                "streamlabs",
                "moobot",
                "fossabot",
                "wizebot",
                "sery_bot",
                "soundalerts",
                "streamstickers",
                "commanderroot",
                "pokemoncommunitygame",
                "kofistreambot",
                "botrixoficial",
                "blerp",
                "tangiabot",
                "own3d",
                "frostytoolsdotcom",
                "lolrankbot",
            ];
            foreach (var login in defaultBotLogins)
            {
                migrationBuilder.InsertData(
                    table: "RankingExcludedAccounts",
                    columns: new[] { "Login", "AddedBy", "AddedAt" },
                    values: new object[] { login, "default", seededAt });
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RankingExcludedAccounts");
        }
    }
}
