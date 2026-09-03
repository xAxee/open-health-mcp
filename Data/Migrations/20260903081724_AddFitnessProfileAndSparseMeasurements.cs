using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace OpenHealthMCP.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFitnessProfileAndSparseMeasurements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "blood_pressure_measurements",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LocalDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TimestampUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TimestampLocal = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Systolic = table.Column<int>(type: "integer", nullable: false),
                    Diastolic = table.Column<int>(type: "integer", nullable: false),
                    Pulse = table.Column<int>(type: "integer", nullable: true),
                    ProviderSourceType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SourceType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_blood_pressure_measurements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "body_composition_measurements",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LocalDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TimestampUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    WeightKilograms = table.Column<double>(type: "double precision", nullable: true),
                    Bmi = table.Column<double>(type: "double precision", nullable: true),
                    BodyFatPercent = table.Column<double>(type: "double precision", nullable: true),
                    MuscleMassKilograms = table.Column<double>(type: "double precision", nullable: true),
                    BoneMassKilograms = table.Column<double>(type: "double precision", nullable: true),
                    BodyWaterPercent = table.Column<double>(type: "double precision", nullable: true),
                    VisceralFat = table.Column<double>(type: "double precision", nullable: true),
                    MetabolicAge = table.Column<double>(type: "double precision", nullable: true),
                    SourceType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_body_composition_measurements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "configured_heart_rate_zones",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Sport = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TrainingMethod = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RestingHeartRateUsed = table.Column<double>(type: "double precision", nullable: true),
                    LactateThresholdHeartRateUsed = table.Column<double>(type: "double precision", nullable: true),
                    MaxHeartRateUsed = table.Column<double>(type: "double precision", nullable: true),
                    Zone1Floor = table.Column<double>(type: "double precision", nullable: true),
                    Zone2Floor = table.Column<double>(type: "double precision", nullable: true),
                    Zone3Floor = table.Column<double>(type: "double precision", nullable: true),
                    Zone4Floor = table.Column<double>(type: "double precision", nullable: true),
                    Zone5Floor = table.Column<double>(type: "double precision", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_configured_heart_rate_zones", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_fitness_profiles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProviderProfileId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Vo2MaxRunning = table.Column<double>(type: "double precision", nullable: true),
                    Vo2MaxCycling = table.Column<double>(type: "double precision", nullable: true),
                    FitnessAge = table.Column<double>(type: "double precision", nullable: true),
                    AchievableFitnessAge = table.Column<double>(type: "double precision", nullable: true),
                    FitnessAgeUpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_fitness_profiles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_blood_pressure_measurements_Source_ExternalId",
                table: "blood_pressure_measurements",
                columns: new[] { "Source", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_blood_pressure_measurements_Source_LocalDate",
                table: "blood_pressure_measurements",
                columns: new[] { "Source", "LocalDate" });

            migrationBuilder.CreateIndex(
                name: "IX_blood_pressure_measurements_Source_TimestampUtc",
                table: "blood_pressure_measurements",
                columns: new[] { "Source", "TimestampUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_body_composition_measurements_Source_ExternalId",
                table: "body_composition_measurements",
                columns: new[] { "Source", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_body_composition_measurements_Source_LocalDate",
                table: "body_composition_measurements",
                columns: new[] { "Source", "LocalDate" });

            migrationBuilder.CreateIndex(
                name: "IX_body_composition_measurements_Source_TimestampUtc",
                table: "body_composition_measurements",
                columns: new[] { "Source", "TimestampUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_configured_heart_rate_zones_Source_Sport",
                table: "configured_heart_rate_zones",
                columns: new[] { "Source", "Sport" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_fitness_profiles_Source",
                table: "user_fitness_profiles",
                column: "Source",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "blood_pressure_measurements");

            migrationBuilder.DropTable(
                name: "body_composition_measurements");

            migrationBuilder.DropTable(
                name: "configured_heart_rate_zones");

            migrationBuilder.DropTable(
                name: "user_fitness_profiles");
        }
    }
}
