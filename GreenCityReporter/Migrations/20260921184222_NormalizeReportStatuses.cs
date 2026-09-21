using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GreenCityReporter.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeReportStatuses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE Reports
                SET CurrentStatus = CASE CurrentStatus
                    WHEN 0 THEN 0 -- Pending
                    WHEN 1 THEN 0 -- InReview -> Pending
                    WHEN 2 THEN 1 -- Assigned
                    WHEN 3 THEN 1 -- InProgress -> Assigned
                    WHEN 4 THEN 2 -- Resolved
                    WHEN 5 THEN 3 -- Rejected
                    ELSE 0
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
