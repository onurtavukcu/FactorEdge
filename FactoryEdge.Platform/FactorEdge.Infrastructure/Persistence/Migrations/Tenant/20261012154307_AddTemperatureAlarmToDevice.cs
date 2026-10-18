using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactorEdge.Infrastructure.Persistence.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddTemperatureAlarmToDevice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsTemperatureAlarmActive",
                table: "Devices",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<double>(
                name: "TemperatureAlarmCelsius",
                table: "Devices",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TemperatureAlarmChangedAtUtc",
                table: "Devices",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsTemperatureAlarmActive",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "TemperatureAlarmCelsius",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "TemperatureAlarmChangedAtUtc",
                table: "Devices");
        }
    }
}
