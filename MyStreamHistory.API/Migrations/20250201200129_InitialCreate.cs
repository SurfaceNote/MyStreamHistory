using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MyStreamHistory.API.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Streamers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChannelName = table.Column<string>(type: "text", nullable: false),
                    BroadcasterType = table.Column<int>(type: "integer", nullable: false),
                    LogoUser = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AccessToken = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    RefreshToken = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    FreshToken = table.Column<bool>(type: "boolean", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Streamers", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Streamers");
        }
    }
}
