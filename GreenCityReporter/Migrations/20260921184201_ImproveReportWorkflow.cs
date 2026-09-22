using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GreenCityReporter.Migrations
{
    /// <inheritdoc />
    public partial class ImproveReportWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "AiConfidence",
                table: "Reports",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AiSuggestedCategoryId",
                table: "Reports",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DepartmentId",
                table: "Reports",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCritical",
                table: "Reports",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "DefaultDepartmentId",
                table: "Categories",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Departments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Reports_AiSuggestedCategoryId",
                table: "Reports",
                column: "AiSuggestedCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_DepartmentId",
                table: "Reports",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_DefaultDepartmentId",
                table: "Categories",
                column: "DefaultDepartmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_Departments_DefaultDepartmentId",
                table: "Categories",
                column: "DefaultDepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Reports_Categories_AiSuggestedCategoryId",
                table: "Reports",
                column: "AiSuggestedCategoryId",
                principalTable: "Categories",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Reports_Departments_DepartmentId",
                table: "Reports",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Categories_Departments_DefaultDepartmentId",
                table: "Categories");

            migrationBuilder.DropForeignKey(
                name: "FK_Reports_Categories_AiSuggestedCategoryId",
                table: "Reports");

            migrationBuilder.DropForeignKey(
                name: "FK_Reports_Departments_DepartmentId",
                table: "Reports");

            migrationBuilder.DropTable(
                name: "Departments");

            migrationBuilder.DropIndex(
                name: "IX_Reports_AiSuggestedCategoryId",
                table: "Reports");

            migrationBuilder.DropIndex(
                name: "IX_Reports_DepartmentId",
                table: "Reports");

            migrationBuilder.DropIndex(
                name: "IX_Categories_DefaultDepartmentId",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "AiConfidence",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "AiSuggestedCategoryId",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "IsCritical",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "DefaultDepartmentId",
                table: "Categories");
        }
    }
}
