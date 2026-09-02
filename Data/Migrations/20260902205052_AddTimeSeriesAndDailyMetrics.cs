using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace OpenHealthMCP.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTimeSeriesAndDailyMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ActiveCalories",
                table: "daily_metrics",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AverageRespirationRate",
                table: "daily_metrics",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AverageSpo2",
                table: "daily_metrics",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AwakeSleepSeconds",
                table: "daily_metrics",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DeepSleepSeconds",
                table: "daily_metrics",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LightSleepSeconds",
                table: "daily_metrics",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ModerateIntensityMinutes",
                table: "daily_metrics",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RemSleepSeconds",
                table: "daily_metrics",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SleepDurationSeconds",
                table: "daily_metrics",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VigorousIntensityMinutes",
                table: "daily_metrics",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StreamsSyncedAt",
                table: "activities",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "activity_streams",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ActivityId = table.Column<long>(type: "bigint", nullable: false),
                    SampleCount = table.Column<int>(type: "integer", nullable: false),
                    AvailableMetrics = table.Column<string[]>(type: "text[]", nullable: false),
                    Samples = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activity_streams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_activity_streams_activities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "activities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "daily_timelines",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Metric = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SampleCount = table.Column<int>(type: "integer", nullable: false),
                    Samples = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_daily_timelines", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_activities_Source_ActivityType_StartedAt",
                table: "activities",
                columns: new[] { "Source", "ActivityType", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_activity_streams_ActivityId",
                table: "activity_streams",
                column: "ActivityId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_daily_timelines_Source_Date_Metric",
                table: "daily_timelines",
                columns: new[] { "Source", "Date", "Metric" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "activity_streams");

            migrationBuilder.DropTable(
                name: "daily_timelines");

            migrationBuilder.DropIndex(
                name: "IX_activities_Source_ActivityType_StartedAt",
                table: "activities");

            migrationBuilder.DropColumn(
                name: "ActiveCalories",
                table: "daily_metrics");

            migrationBuilder.DropColumn(
                name: "AverageRespirationRate",
                table: "daily_metrics");

            migrationBuilder.DropColumn(
                name: "AverageSpo2",
                table: "daily_metrics");

            migrationBuilder.DropColumn(
                name: "AwakeSleepSeconds",
                table: "daily_metrics");

            migrationBuilder.DropColumn(
                name: "DeepSleepSeconds",
                table: "daily_metrics");

            migrationBuilder.DropColumn(
                name: "LightSleepSeconds",
                table: "daily_metrics");

            migrationBuilder.DropColumn(
                name: "ModerateIntensityMinutes",
                table: "daily_metrics");

            migrationBuilder.DropColumn(
                name: "RemSleepSeconds",
                table: "daily_metrics");

            migrationBuilder.DropColumn(
                name: "SleepDurationSeconds",
                table: "daily_metrics");

            migrationBuilder.DropColumn(
                name: "VigorousIntensityMinutes",
                table: "daily_metrics");

            migrationBuilder.DropColumn(
                name: "StreamsSyncedAt",
                table: "activities");
        }
    }
}
