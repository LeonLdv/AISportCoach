using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AISportCoach.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReportEmbeddingChunks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing single-vector embeddings are stale — clear before adding NOT NULL columns.
            migrationBuilder.Sql("""DELETE FROM "ReportEmbeddings";""");

            migrationBuilder.AddColumn<Guid>(
                name: "ChunkId",
                table: "ReportEmbeddings",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "ChunkType",
                table: "ReportEmbeddings",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChunkId",
                table: "ReportEmbeddings");

            migrationBuilder.DropColumn(
                name: "ChunkType",
                table: "ReportEmbeddings");
        }
    }
}
