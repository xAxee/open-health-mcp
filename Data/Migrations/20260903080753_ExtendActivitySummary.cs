using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenHealthMCP.Data.Migrations
{
    /// <inheritdoc />
    public partial class ExtendActivitySummary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "AverageGroundContactTimeMilliseconds",
                table: "activities",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AverageStrideLengthMeters",
                table: "activities",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AverageVerticalOscillationMillimeters",
                table: "activities",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsParent",
                table: "activities",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "MaxElevationMeters",
                table: "activities",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "MaxTwentyMinutePowerWatts",
                table: "activities",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "MinElevationMeters",
                table: "activities",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParentExternalId",
                table: "activities",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AverageGroundContactTimeMilliseconds",
                table: "activities");

            migrationBuilder.DropColumn(
                name: "AverageStrideLengthMeters",
                table: "activities");

            migrationBuilder.DropColumn(
                name: "AverageVerticalOscillationMillimeters",
                table: "activities");

            migrationBuilder.DropColumn(
                name: "IsParent",
                table: "activities");

            migrationBuilder.DropColumn(
                name: "MaxElevationMeters",
                table: "activities");

            migrationBuilder.DropColumn(
                name: "MaxTwentyMinutePowerWatts",
                table: "activities");

            migrationBuilder.DropColumn(
                name: "MinElevationMeters",
                table: "activities");

            migrationBuilder.DropColumn(
                name: "ParentExternalId",
                table: "activities");
        }
    }
}
