using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyStreamHistory.ContentService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddArticleLanguages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Articles_Type_Slug",
                table: "Articles");

            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "Articles",
                type: "character varying(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "en");

            migrationBuilder.AddColumn<Guid>(
                name: "TranslationGroupId",
                table: "Articles",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.Sql("UPDATE \"Articles\" SET \"TranslationGroupId\" = \"Id\"");

            migrationBuilder.CreateIndex(
                name: "IX_Articles_Language_Type_Slug",
                table: "Articles",
                columns: new[] { "Language", "Type", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Articles_TranslationGroupId_Language",
                table: "Articles",
                columns: new[] { "TranslationGroupId", "Language" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Articles_Language_Type_Slug",
                table: "Articles");

            migrationBuilder.DropIndex(
                name: "IX_Articles_TranslationGroupId_Language",
                table: "Articles");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "Articles");

            migrationBuilder.DropColumn(
                name: "TranslationGroupId",
                table: "Articles");

            migrationBuilder.CreateIndex(
                name: "IX_Articles_Type_Slug",
                table: "Articles",
                columns: new[] { "Type", "Slug" },
                unique: true);
        }
    }
}
