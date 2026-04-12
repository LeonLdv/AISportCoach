using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AISportCoach.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemovePlayerSkillLevel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlayerSkillLevel",
                table: "CoachingReports");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PlayerSkillLevel",
                table: "CoachingReports",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
