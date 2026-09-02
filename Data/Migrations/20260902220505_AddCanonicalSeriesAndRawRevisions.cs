using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace OpenHealthMCP.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCanonicalSeriesAndRawRevisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_raw_provider_data_Source_DataType_ExternalId",
                table: "raw_provider_data");

            migrationBuilder.AddColumn<string>(
                name: "Endpoint",
                table: "raw_provider_data",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HttpStatusCode",
                table: "raw_provider_data",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParserVersion",
                table: "raw_provider_data",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "garmin-v0");

            migrationBuilder.AddColumn<string>(
                name: "PayloadHash",
                table: "raw_provider_data",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "activity_samples",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ActivityId = table.Column<long>(type: "bigint", nullable: false),
                    TimestampUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ElapsedSeconds = table.Column<double>(type: "double precision", nullable: false),
                    HeartRateBpm = table.Column<double>(type: "double precision", nullable: true),
                    DistanceMeters = table.Column<double>(type: "double precision", nullable: true),
                    SpeedMetersPerSecond = table.Column<double>(type: "double precision", nullable: true),
                    PaceSecondsPerKilometer = table.Column<double>(type: "double precision", nullable: true),
                    ElevationMeters = table.Column<double>(type: "double precision", nullable: true),
                    Cadence = table.Column<double>(type: "double precision", nullable: true),
                    PowerWatts = table.Column<double>(type: "double precision", nullable: true),
                    TemperatureCelsius = table.Column<double>(type: "double precision", nullable: true),
                    RespirationRate = table.Column<double>(type: "double precision", nullable: true),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    SourceType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activity_samples", x => x.Id);
                    table.ForeignKey(
                        name: "FK_activity_samples_activities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "activities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "health_metric_samples",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Metric = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    LocalDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TimestampUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ValueNumeric = table.Column<double>(type: "double precision", nullable: true),
                    ValueText = table.Column<string>(type: "text", nullable: true),
                    Unit = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SourceType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Quality = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_health_metric_samples", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_raw_provider_data_Source_DataType_ExternalId_FetchedAt",
                table: "raw_provider_data",
                columns: new[] { "Source", "DataType", "ExternalId", "FetchedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_raw_provider_data_Source_DataType_ExternalId_PayloadHash",
                table: "raw_provider_data",
                columns: new[] { "Source", "DataType", "ExternalId", "PayloadHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_activity_samples_ActivityId_ElapsedSeconds",
                table: "activity_samples",
                columns: new[] { "ActivityId", "ElapsedSeconds" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_activity_samples_ActivityId_TimestampUtc",
                table: "activity_samples",
                columns: new[] { "ActivityId", "TimestampUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_health_metric_samples_Source_LocalDate_Metric",
                table: "health_metric_samples",
                columns: new[] { "Source", "LocalDate", "Metric" });

            migrationBuilder.CreateIndex(
                name: "IX_health_metric_samples_Source_Metric_TimestampUtc",
                table: "health_metric_samples",
                columns: new[] { "Source", "Metric", "TimestampUtc" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "activity_samples");

            migrationBuilder.DropTable(
                name: "health_metric_samples");

            migrationBuilder.DropIndex(
                name: "IX_raw_provider_data_Source_DataType_ExternalId_FetchedAt",
                table: "raw_provider_data");

            migrationBuilder.DropIndex(
                name: "IX_raw_provider_data_Source_DataType_ExternalId_PayloadHash",
                table: "raw_provider_data");

            migrationBuilder.DropColumn(
                name: "Endpoint",
                table: "raw_provider_data");

            migrationBuilder.DropColumn(
                name: "HttpStatusCode",
                table: "raw_provider_data");

            migrationBuilder.DropColumn(
                name: "ParserVersion",
                table: "raw_provider_data");

            migrationBuilder.DropColumn(
                name: "PayloadHash",
                table: "raw_provider_data");

            migrationBuilder.CreateIndex(
                name: "IX_raw_provider_data_Source_DataType_ExternalId",
                table: "raw_provider_data",
                columns: new[] { "Source", "DataType", "ExternalId" },
                unique: true);
        }
    }
}
