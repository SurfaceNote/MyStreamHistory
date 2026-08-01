using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MyStreamHistory.TwitchTrackingService.Infrastructure.Persistence;

#nullable disable

namespace MyStreamHistory.TwitchTrackingService.Infrastructure.Persistence.Migrations;

[DbContext(typeof(TwitchTrackingDbContext))]
[Migration("20260801190000_AddStreamReconciliation")]
public partial class AddStreamReconciliation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "LastConfirmedLiveAt",
            table: "StreamSessions",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "MissingSinceAt",
            table: "StreamSessions",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.Sql("""
            WITH ranked AS (
                SELECT "Id",
                       ROW_NUMBER() OVER (
                           PARTITION BY "TwitchUserId"
                           ORDER BY "StartedAt" DESC, "Id" DESC) AS row_number,
                       MAX("StartedAt") OVER (
                           PARTITION BY "TwitchUserId") AS newest_started_at
                FROM "StreamSessions"
                WHERE "IsLive" = true
            ),
            closed_categories AS (
                UPDATE "StreamCategories" AS category
                SET "EndedAt" = GREATEST(category."StartedAt", ranked.newest_started_at)
                FROM ranked
                WHERE category."StreamSessionId" = ranked."Id"
                  AND category."EndedAt" IS NULL
                  AND ranked.row_number > 1
                RETURNING category."Id"
            )
            UPDATE "StreamSessions" AS session
            SET "IsLive" = false,
                "EndedAt" = COALESCE(session."EndedAt", ranked.newest_started_at)
            FROM ranked
            WHERE session."Id" = ranked."Id"
              AND ranked.row_number > 1;
            """);

        migrationBuilder.Sql("""
            UPDATE "StreamSessions"
            SET "LastConfirmedLiveAt" = now()
            WHERE "IsLive" = true;
            """);

        migrationBuilder.CreateIndex(
            name: "ux_stream_session_active_user",
            table: "StreamSessions",
            column: "TwitchUserId",
            unique: true,
            filter: "\"IsLive\" = true");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ux_stream_session_active_user",
            table: "StreamSessions");

        migrationBuilder.DropColumn(
            name: "LastConfirmedLiveAt",
            table: "StreamSessions");

        migrationBuilder.DropColumn(
            name: "MissingSinceAt",
            table: "StreamSessions");
    }
}
