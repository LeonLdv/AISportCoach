using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AISportCoach.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveJobFrameTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FramesAnalyzed",
                table: "AnalysisJobs");

            migrationBuilder.DropColumn(
                name: "FramesExtracted",
                table: "AnalysisJobs");

            migrationBuilder.DropColumn(
                name: "ProgressPercent",
                table: "AnalysisJobs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FramesAnalyzed",
                table: "AnalysisJobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FramesExtracted",
                table: "AnalysisJobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ProgressPercent",
                table: "AnalysisJobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
