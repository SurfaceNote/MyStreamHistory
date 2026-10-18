using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyStreamHistory.ViewerService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LimitCommunityMessageReceiptStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Provisional",
                table: "CommunityMessageReceipts");

            migrationBuilder.DropColumn(
                name: "StreamSessionId",
                table: "CommunityMessageReceipts");

            migrationBuilder.DropColumn(
                name: "ViewerTwitchUserId",
                table: "CommunityMessageReceipts");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityMessageReceipts_ReceivedAt",
                table: "CommunityMessageReceipts",
                column: "ReceivedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CommunityMessageReceipts_ReceivedAt",
                table: "CommunityMessageReceipts");

            migrationBuilder.AddColumn<bool>(
                name: "Provisional",
                table: "CommunityMessageReceipts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "StreamSessionId",
                table: "CommunityMessageReceipts",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "ViewerTwitchUserId",
                table: "CommunityMessageReceipts",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
