using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace OpenHealthMCP.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddExtendedGarminActivityData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ActiveLengths",
                table: "activities",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AerobicTrainingEffect",
                table: "activities",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AnaerobicTrainingEffect",
                table: "activities",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AverageCadence",
                table: "activities",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AveragePaceSecondsPerKilometer",
                table: "activities",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AveragePowerWatts",
                table: "activities",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AverageRespirationRate",
                table: "activities",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AverageSpeedMetersPerSecond",
                table: "activities",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AverageSwolf",
                table: "activities",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CadenceUnit",
                table: "activities",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ElapsedDurationSeconds",
                table: "activities",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ElevationLossMeters",
                table: "activities",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "HeartRateZonesSyncedAt",
                table: "activities",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "IntensityFactor",
                table: "activities",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LapsSyncedAt",
                table: "activities",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "MaxCadence",
                table: "activities",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "MaxPowerWatts",
                table: "activities",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "MaxRespirationRate",
                table: "activities",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "MaxSpeedMetersPerSecond",
                table: "activities",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "MaxTemperatureCelsius",
                table: "activities",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "MinRespirationRate",
                table: "activities",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "MinTemperatureCelsius",
                table: "activities",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "MovingDurationSeconds",
                table: "activities",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "NormalizedPowerWatts",
                table: "activities",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Steps",
                table: "activities",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "TrainingLoad",
                table: "activities",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "TrainingStressScore",
                table: "activities",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Vo2Max",
                table: "activities",
                type: "double precision",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "activity_heart_rate_zones",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ActivityId = table.Column<long>(type: "bigint", nullable: false),
                    ZoneNumber = table.Column<int>(type: "integer", nullable: false),
                    TimeSeconds = table.Column<double>(type: "double precision", nullable: false),
                    Percentage = table.Column<double>(type: "double precision", nullable: true),
                    LowBoundaryBpm = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activity_heart_rate_zones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_activity_heart_rate_zones_activities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "activities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "activity_laps",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ActivityId = table.Column<long>(type: "bigint", nullable: false),
                    LapIndex = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DurationSeconds = table.Column<double>(type: "double precision", nullable: true),
                    ElapsedDurationSeconds = table.Column<double>(type: "double precision", nullable: true),
                    MovingDurationSeconds = table.Column<double>(type: "double precision", nullable: true),
                    DistanceMeters = table.Column<double>(type: "double precision", nullable: true),
                    AverageSpeedMetersPerSecond = table.Column<double>(type: "double precision", nullable: true),
                    MaxSpeedMetersPerSecond = table.Column<double>(type: "double precision", nullable: true),
                    AveragePaceSecondsPerKilometer = table.Column<double>(type: "double precision", nullable: true),
                    Calories = table.Column<int>(type: "integer", nullable: true),
                    AverageHeartRate = table.Column<int>(type: "integer", nullable: true),
                    MaxHeartRate = table.Column<int>(type: "integer", nullable: true),
                    ElevationGainMeters = table.Column<double>(type: "double precision", nullable: true),
                    ElevationLossMeters = table.Column<double>(type: "double precision", nullable: true),
                    MinElevationMeters = table.Column<double>(type: "double precision", nullable: true),
                    MaxElevationMeters = table.Column<double>(type: "double precision", nullable: true),
                    AverageCadence = table.Column<double>(type: "double precision", nullable: true),
                    MaxCadence = table.Column<double>(type: "double precision", nullable: true),
                    CadenceUnit = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    AverageTemperatureCelsius = table.Column<double>(type: "double precision", nullable: true),
                    MinTemperatureCelsius = table.Column<double>(type: "double precision", nullable: true),
                    MaxTemperatureCelsius = table.Column<double>(type: "double precision", nullable: true),
                    AverageRespirationRate = table.Column<double>(type: "double precision", nullable: true),
                    MaxRespirationRate = table.Column<double>(type: "double precision", nullable: true),
                    IntensityType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activity_laps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_activity_laps_activities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "activities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_activity_heart_rate_zones_ActivityId_ZoneNumber",
                table: "activity_heart_rate_zones",
                columns: new[] { "ActivityId", "ZoneNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_activity_laps_ActivityId_LapIndex",
                table: "activity_laps",
                columns: new[] { "ActivityId", "LapIndex" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "activity_heart_rate_zones");

            migrationBuilder.DropTable(
                name: "activity_laps");

            migrationBuilder.DropColumn(
                name: "ActiveLengths",
                table: "activities");

            migrationBuilder.DropColumn(
                name: "AerobicTrainingEffect",
                table: "activities");

            migrationBuilder.DropColumn(
                name: "AnaerobicTrainingEffect",
                table: "activities");

            migrationBuilder.DropColumn(
                name: "AverageCadence",
                table: "activities");

            migrationBuilder.DropColumn(
                name: "AveragePaceSecondsPerKilometer",
                table: "activities");

            migrationBuilder.DropColumn(
                name: "AveragePowerWatts",
                table: "activities");

            migrationBuilder.DropColumn(
                name: "AverageRespirationRate",
                table: "activities");

            migrationBuilder.DropColumn(
                name: "AverageSpeedMetersPerSecond",
                table: "activities");

            migrationBuilder.DropColumn(
                name: "AverageSwolf",
                table: "activities");

            migrationBuilder.DropColumn(
                name: "CadenceUnit",
                table: "activities");

            migrationBuilder.DropColumn(
                name: "ElapsedDurationSeconds",
                table: "activities");

            migrationBuilder.DropColumn(
                name: "ElevationLossMeters",
                table: "activities");

            migrationBuilder.DropColumn(
                name: "HeartRateZonesSyncedAt",
                table: "activities");

            migrationBuilder.DropColumn(
                name: "IntensityFactor",
                table: "activities");

            migrationBuilder.DropColumn(
                name: "LapsSyncedAt",
                table: "activities");

            migrationBuilder.DropColumn(
                name: "MaxCadence",
                table: "activities");

            migrationBuilder.DropColumn(
                name: "MaxPowerWatts",
                table: "activities");

            migrationBuilder.DropColumn(
                name: "MaxRespirationRate",
                table: "activities");

            migrationBuilder.DropColumn(
                name: "MaxSpeedMetersPerSecond",
                table: "activities");

            migrationBuilder.DropColumn(
                name: "MaxTemperatureCelsius",
                table: "activities");

            migrationBuilder.DropColumn(
                name: "MinRespirationRate",
                table: "activities");

            migrationBuilder.DropColumn(
                name: "MinTemperatureCelsius",
                table: "activities");

            migrationBuilder.DropColumn(
                name: "MovingDurationSeconds",
                table: "activities");

            migrationBuilder.DropColumn(
                name: "NormalizedPowerWatts",
                table: "activities");

            migrationBuilder.DropColumn(
                name: "Steps",
                table: "activities");

            migrationBuilder.DropColumn(
                name: "TrainingLoad",
                table: "activities");

            migrationBuilder.DropColumn(
                name: "TrainingStressScore",
                table: "activities");

            migrationBuilder.DropColumn(
                name: "Vo2Max",
                table: "activities");
        }
    }
}
