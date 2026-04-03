using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AISportCoach.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAnalysisJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CoachingReports_AnalysisJobs_AnalysisJobId",
                table: "CoachingReports");

            migrationBuilder.DropTable(
                name: "AnalysisJobs");

            migrationBuilder.RenameColumn(
                name: "AnalysisJobId",
                table: "CoachingReports",
                newName: "VideoUploadId");

            migrationBuilder.RenameIndex(
                name: "IX_CoachingReports_AnalysisJobId",
                table: "CoachingReports",
                newName: "IX_CoachingReports_VideoUploadId");

            migrationBuilder.AddForeignKey(
                name: "FK_CoachingReports_VideoUploads_VideoUploadId",
                table: "CoachingReports",
                column: "VideoUploadId",
                principalTable: "VideoUploads",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CoachingReports_VideoUploads_VideoUploadId",
                table: "CoachingReports");

            migrationBuilder.RenameColumn(
                name: "VideoUploadId",
                table: "CoachingReports",
                newName: "AnalysisJobId");

            migrationBuilder.RenameIndex(
                name: "IX_CoachingReports_VideoUploadId",
                table: "CoachingReports",
                newName: "IX_CoachingReports_AnalysisJobId");

            migrationBuilder.CreateTable(
                name: "AnalysisJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VideoUploadId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalysisJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnalysisJobs_VideoUploads_VideoUploadId",
                        column: x => x.VideoUploadId,
                        principalTable: "VideoUploads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisJobs_VideoUploadId",
                table: "AnalysisJobs",
                column: "VideoUploadId");

            migrationBuilder.AddForeignKey(
                name: "FK_CoachingReports_AnalysisJobs_AnalysisJobId",
                table: "CoachingReports",
                column: "AnalysisJobId",
                principalTable: "AnalysisJobs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
