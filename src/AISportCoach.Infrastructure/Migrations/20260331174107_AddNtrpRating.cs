using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AISportCoach.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNtrpRating : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NtrpConfidence",
                table: "CoachingReports",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "NtrpRating",
                table: "CoachingReports",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NtrpRatingJustification",
                table: "CoachingReports",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "NtrpRatingMax",
                table: "CoachingReports",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "NtrpRatingMin",
                table: "CoachingReports",
                type: "double precision",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "NtrpEvidenceItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CoachingReportId = table.Column<Guid>(type: "uuid", nullable: false),
                    Observation = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    NtrpIndicator = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SupportedLevel = table.Column<double>(type: "double precision", nullable: false),
                    Weight = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NtrpEvidenceItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NtrpEvidenceItems_CoachingReports_CoachingReportId",
                        column: x => x.CoachingReportId,
                        principalTable: "CoachingReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NtrpEvidenceItems_CoachingReportId",
                table: "NtrpEvidenceItems",
                column: "CoachingReportId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NtrpEvidenceItems");

            migrationBuilder.DropColumn(
                name: "NtrpConfidence",
                table: "CoachingReports");

            migrationBuilder.DropColumn(
                name: "NtrpRating",
                table: "CoachingReports");

            migrationBuilder.DropColumn(
                name: "NtrpRatingJustification",
                table: "CoachingReports");

            migrationBuilder.DropColumn(
                name: "NtrpRatingMax",
                table: "CoachingReports");

            migrationBuilder.DropColumn(
                name: "NtrpRatingMin",
                table: "CoachingReports");
        }
    }
}
