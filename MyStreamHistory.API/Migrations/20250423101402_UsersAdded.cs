using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MyStreamHistory.API.Migrations
{
    /// <inheritdoc />
    public partial class UsersAdded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Streamers");

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TwitchId = table.Column<int>(type: "integer", nullable: false),
                    Username = table.Column<string>(type: "text", nullable: false),
                    BroadcasterType = table.Column<int>(type: "integer", nullable: false),
                    LogoUser = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsStreamer = table.Column<bool>(type: "boolean", nullable: true),
                    AccessToken = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    RefreshToken = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    FreshToken = table.Column<bool>(type: "boolean", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_TwitchId",
                table: "Users",
                column: "TwitchId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.CreateTable(
                name: "Streamers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AccessToken = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    BroadcasterType = table.Column<int>(type: "integer", nullable: false),
                    ChannelName = table.Column<string>(type: "text", nullable: false),
                    FreshToken = table.Column<bool>(type: "boolean", nullable: true),
                    LogoUser = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RefreshToken = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    TwitchId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Streamers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Streamers_TwitchId",
                table: "Streamers",
                column: "TwitchId",
                unique: true);
        }
    }
}
