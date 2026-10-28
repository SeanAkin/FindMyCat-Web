using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FindMyCat.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceEventsAndIngestKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IngestApiKeyHash",
                table: "SharedCredentials",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DeviceEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UniqueId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Payload = table.Column<string>(type: "TEXT", nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceEvents_UniqueId_Kind_Id",
                table: "DeviceEvents",
                columns: new[] { "UniqueId", "Kind", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeviceEvents");

            migrationBuilder.DropColumn(
                name: "IngestApiKeyHash",
                table: "SharedCredentials");
        }
    }
}
