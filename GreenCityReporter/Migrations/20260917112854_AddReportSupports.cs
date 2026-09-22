using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GreenCityReporter.Migrations
{
    /// <inheritdoc />
    public partial class AddReportSupports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reports_CategoryId",
                table: "Reports");

            migrationBuilder.CreateTable(
                name: "ReportSupports",
                columns: table => new
                {
                    ReportId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportSupports", x => new { x.ReportId, x.UserId });
                    table.ForeignKey(
                        name: "FK_ReportSupports_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReportSupports_Reports_ReportId",
                        column: x => x.ReportId,
                        principalTable: "Reports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Reports_CategoryId_CurrentStatus_Latitude_Longitude",
                table: "Reports",
                columns: new[] { "CategoryId", "CurrentStatus", "Latitude", "Longitude" });

            migrationBuilder.CreateIndex(
                name: "IX_ReportSupports_UserId_CreatedAt",
                table: "ReportSupports",
                columns: new[] { "UserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReportSupports");

            migrationBuilder.DropIndex(
                name: "IX_Reports_CategoryId_CurrentStatus_Latitude_Longitude",
                table: "Reports");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_CategoryId",
                table: "Reports",
                column: "CategoryId");
        }
    }
}
