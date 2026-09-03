using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenHealthMCP.Data.Migrations
{
    /// <inheritdoc />
    public partial class ExtendDailyHealth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EndTimestampUtc",
                table: "health_metric_samples",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ActiveSeconds",
                table: "daily_metrics",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ActivityStressSeconds",
                table: "daily_metrics",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AverageSleepRespirationRate",
                table: "daily_metrics",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AverageSleepSpo2",
                table: "daily_metrics",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AverageSleepStress",
                table: "daily_metrics",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BmrCalories",
                table: "daily_metrics",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BodyBatteryCharged",
                table: "daily_metrics",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BodyBatteryDrained",
                table: "daily_metrics",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BodyBatteryMostRecent",
                table: "daily_metrics",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "DistanceMeters",
                table: "daily_metrics",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "FloorsClimbed",
                table: "daily_metrics",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FloorsGoal",
                table: "daily_metrics",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "HighStressPercentage",
                table: "daily_metrics",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HighStressSeconds",
                table: "daily_metrics",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "HrvCreatedAt",
                table: "daily_metrics",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "HrvFiveMinuteHigh",
                table: "daily_metrics",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HrvStatus",
                table: "daily_metrics",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IntensityGoal",
                table: "daily_metrics",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LatestSpo2",
                table: "daily_metrics",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LowStressPercentage",
                table: "daily_metrics",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LowStressSeconds",
                table: "daily_metrics",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "MaximumRespirationRate",
                table: "daily_metrics",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "MediumStressPercentage",
                table: "daily_metrics",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MediumStressSeconds",
                table: "daily_metrics",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "MinimumRespirationRate",
                table: "daily_metrics",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "MinimumSpo2",
                table: "daily_metrics",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NapDurationSeconds",
                table: "daily_metrics",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "RestStressPercentage",
                table: "daily_metrics",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RestStressSeconds",
                table: "daily_metrics",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SleepAwakeCount",
                table: "daily_metrics",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SleepEndLocal",
                table: "daily_metrics",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SleepEndUtc",
                table: "daily_metrics",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SleepQualifier",
                table: "daily_metrics",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SleepStartLocal",
                table: "daily_metrics",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SleepStartUtc",
                table: "daily_metrics",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SleepSubScoresJson",
                table: "daily_metrics",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "Spo2WindowEndUtc",
                table: "daily_metrics",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "Spo2WindowStartUtc",
                table: "daily_metrics",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StepsGoal",
                table: "daily_metrics",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "StressMax",
                table: "daily_metrics",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StressQualifier",
                table: "daily_metrics",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TotalIntensityMinutes",
                table: "daily_metrics",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UnmeasurableSleepSeconds",
                table: "daily_metrics",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UtcOffsetMinutes",
                table: "daily_metrics",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WellnessEndLocal",
                table: "daily_metrics",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "WellnessEndUtc",
                table: "daily_metrics",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WellnessStartLocal",
                table: "daily_metrics",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "WellnessStartUtc",
                table: "daily_metrics",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EndTimestampUtc",
                table: "health_metric_samples");

            migrationBuilder.DropColumn(
                name: "ActiveSeconds",
                table: "daily_metrics");

            migrationBuilder.DropColumn(
                name: "ActivityStressSeconds",
                table: "daily_metrics");

            migrationBuilder.DropColumn(
                name: "AverageSleepRespirationRate",
                table: "daily_metrics");

            migrationBuilder.DropColumn(
                name: "AverageSleepSpo2",
                table: "daily_metrics");

            migrationBuilder.DropColumn(
                name: "AverageSleepStress",
                table: "daily_metrics");

            migrationBuilder.DropColumn(
                name: "BmrCalories",
                table: "daily_metrics");

            migrationBuilder.DropColumn(
                name: "BodyBatteryCharged",
                table: "daily_metrics");

            migrationBuilder.DropColumn(
                name: "BodyBatteryDrained",
                table: "daily_metrics");

            migrationBuilder.DropColumn(
                name: "BodyBatteryMostRecent",
                table: "daily_metrics");

            migrationBuilder.DropColumn(
                name: "DistanceMeters",
                table: "daily_metrics");

            migrationBuilder.DropColumn(
                name: "FloorsClimbed",
                table: "daily_metrics");

            migrationBuilder.DropColumn(
                name: "FloorsGoal",
                table: "daily_metrics");

            migrationBuilder.DropColumn(
                name: "HighStressPercentage",
                table: "daily_metrics");

            migrationBuilder.DropColumn(
                name: "HighStressSeconds",
                table: "daily_metrics");

            migrationBuilder.DropColumn(
                name: "HrvCreatedAt",
                table: "daily_metrics");

            migrationBuilder.DropColumn(
                name: "HrvFiveMinuteHigh",
                table: "daily_metrics");

            migrationBuilder.DropColumn(
                name: "HrvStatus",
                table: "daily_metrics");

            migrationBuilder.DropColumn(
                name: "IntensityGoal",
                table: "daily_metrics");

            migrationBuilder.DropColumn(
                name: "LatestSpo2",
                table: "daily_metrics");

            migrationBuilder.DropColumn(
                name: "LowStressPercentage",
                table: "daily_metrics");

            migrationBuilder.DropColumn(
                name: "LowStressSeconds",
                table: "daily_metrics");

            migrationBuilder.DropColumn(
                name: "MaximumRespirationRate",
                table: "daily_metrics");

            migrationBuilder.DropColumn(
                name: "MediumStressPercentage",
                table: "daily_metrics");

            migrationBuilder.DropColumn(
                name: "MediumStressSeconds",
                table: "daily_metrics");

            migrationBuilder.DropColumn(
                name: "MinimumRespirationRate",
                table: "daily_metrics");

            migrationBuilder.DropColumn(
                name: "MinimumSpo2",
                table: "daily_metrics");

            migrationBuilder.DropColumn(
                name: "NapDurationSeconds",
                table: "daily_metrics");

            migrationBuilder.DropColumn(
                name: "RestStressPercentage",
                table: "daily_metrics");

            migrationBuilder.DropColumn(
                name: "RestStressSeconds",
                table: "daily_metrics");

            migrationBuilder.DropColumn(
                name: "SleepAwakeCount",
                table: "daily_metrics");

            migrationBuilder.DropColumn(
                name: "SleepEndLocal",
                table: "daily_metrics");

            migrationBuilder.DropColumn(
                name: "SleepEndUtc",
                table: "daily_metrics");

            migrationBuilder.DropColumn(
                name: "SleepQualifier",
                table: "daily_metrics");

            migrationBuilder.DropColumn(
                name: "SleepStartLocal",
                table: "daily_metrics");

            migrationBuilder.DropColumn(
                name: "SleepStartUtc",
                table: "daily_metrics");

            migrationBuilder.DropColumn(
                name: "SleepSubScoresJson",
                table: "daily_metrics");

            migrationBuilder.DropColumn(
                name: "Spo2WindowEndUtc",
                table: "daily_metrics");

            migrationBuilder.DropColumn(
                name: "Spo2WindowStartUtc",
                table: "daily_metrics");

            migrationBuilder.DropColumn(
                name: "StepsGoal",
                table: "daily_metrics");

            migrationBuilder.DropColumn(
                name: "StressMax",
                table: "daily_metrics");

            migrationBuilder.DropColumn(
                name: "StressQualifier",
                table: "daily_metrics");

            migrationBuilder.DropColumn(
                name: "TotalIntensityMinutes",
                table: "daily_metrics");

            migrationBuilder.DropColumn(
                name: "UnmeasurableSleepSeconds",
                table: "daily_metrics");

            migrationBuilder.DropColumn(
                name: "UtcOffsetMinutes",
                table: "daily_metrics");

            migrationBuilder.DropColumn(
                name: "WellnessEndLocal",
                table: "daily_metrics");

            migrationBuilder.DropColumn(
                name: "WellnessEndUtc",
                table: "daily_metrics");

            migrationBuilder.DropColumn(
                name: "WellnessStartLocal",
                table: "daily_metrics");

            migrationBuilder.DropColumn(
                name: "WellnessStartUtc",
                table: "daily_metrics");
        }
    }
}
