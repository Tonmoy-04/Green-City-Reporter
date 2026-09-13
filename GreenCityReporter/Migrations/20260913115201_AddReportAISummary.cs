using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GreenCityReporter.Migrations
{
    /// <inheritdoc />
    public partial class AddReportAISummary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AISummary",
                table: "Reports",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AISummary",
                table: "Reports");
        }
    }
}
