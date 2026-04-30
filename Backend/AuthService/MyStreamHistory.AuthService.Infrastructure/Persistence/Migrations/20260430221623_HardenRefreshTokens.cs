using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyStreamHistory.AuthService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HardenRefreshTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM \"RefreshToken\"");
            
            migrationBuilder.RenameColumn(
                name: "Token",
                table: "RefreshToken",
                newName: "TokenHash");

            migrationBuilder.RenameIndex(
                name: "IX_RefreshToken_Token",
                table: "RefreshToken",
                newName: "IX_RefreshToken_TokenHash");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "RefreshToken",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedByIp",
                table: "RefreshToken",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReplacedByTokenId",
                table: "RefreshToken",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RevokedAt",
                table: "RefreshToken",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TokenFamilyId",
                table: "RefreshToken",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TokenId",
                table: "RefreshToken",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_RefreshToken_ExpiresAt",
                table: "RefreshToken",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshToken_TokenFamilyId",
                table: "RefreshToken",
                column: "TokenFamilyId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshToken_TokenId",
                table: "RefreshToken",
                column: "TokenId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RefreshToken_ExpiresAt",
                table: "RefreshToken");

            migrationBuilder.DropIndex(
                name: "IX_RefreshToken_TokenFamilyId",
                table: "RefreshToken");

            migrationBuilder.DropIndex(
                name: "IX_RefreshToken_TokenId",
                table: "RefreshToken");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "RefreshToken");

            migrationBuilder.DropColumn(
                name: "CreatedByIp",
                table: "RefreshToken");

            migrationBuilder.DropColumn(
                name: "ReplacedByTokenId",
                table: "RefreshToken");

            migrationBuilder.DropColumn(
                name: "RevokedAt",
                table: "RefreshToken");

            migrationBuilder.DropColumn(
                name: "TokenFamilyId",
                table: "RefreshToken");

            migrationBuilder.DropColumn(
                name: "TokenId",
                table: "RefreshToken");

            migrationBuilder.RenameColumn(
                name: "TokenHash",
                table: "RefreshToken",
                newName: "Token");

            migrationBuilder.RenameIndex(
                name: "IX_RefreshToken_TokenHash",
                table: "RefreshToken",
                newName: "IX_RefreshToken_Token");
        }
    }
}
