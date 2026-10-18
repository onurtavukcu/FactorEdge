using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactorEdge.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddCameraDevices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DeviceType",
                table: "Devices",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "DeviceCameras",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    StreamUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    ContainerName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    LastOperatorStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    LastStatusAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceCameras", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceCameras_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceCameras_DeviceId",
                table: "DeviceCameras",
                column: "DeviceId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeviceCameras");

            migrationBuilder.DropColumn(
                name: "DeviceType",
                table: "Devices");
        }
    }
}
