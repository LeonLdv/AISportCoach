using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AISportCoach.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReportEmbeddings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Enable pgvector extension
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS vector");

            // Add UserId to VideoUploads — defaults to MockUser.Id for existing rows
            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "VideoUploads",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000001"));

            // Create ReportEmbeddings table (EF-managed columns)
            migrationBuilder.CreateTable(
                name: "ReportEmbeddings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CoachingReportId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportEmbeddings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReportEmbeddings_CoachingReports_CoachingReportId",
                        column: x => x.CoachingReportId,
                        principalTable: "CoachingReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Add vector column — not managed by EF (requires pgvector type)
            var zeros = string.Join(",", Enumerable.Repeat("0", 768));
            migrationBuilder.Sql($"""ALTER TABLE "ReportEmbeddings" ADD COLUMN "Embedding" vector(768) NOT NULL DEFAULT '[{zeros}]'::vector""");
            migrationBuilder.Sql("""ALTER TABLE "ReportEmbeddings" ALTER COLUMN "Embedding" DROP DEFAULT""");

            // IVFFlat index for approximate nearest-neighbour cosine search
            migrationBuilder.Sql("""CREATE INDEX ON "ReportEmbeddings" USING ivfflat ("Embedding" vector_cosine_ops)""");

            migrationBuilder.CreateIndex(
                name: "IX_ReportEmbeddings_CoachingReportId",
                table: "ReportEmbeddings",
                column: "CoachingReportId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ReportEmbeddings");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "VideoUploads");
        }
    }
}
