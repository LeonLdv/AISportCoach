using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AISportCoach.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceIvfflatWithHnsw : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS "IX_ReportEmbeddings_Embedding";
                CREATE INDEX ON "ReportEmbeddings" USING hnsw ("Embedding" vector_cosine_ops);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS "IX_ReportEmbeddings_Embedding";
                CREATE INDEX ON "ReportEmbeddings" USING ivfflat ("Embedding" vector_cosine_ops);
                """);
        }
    }
}
