using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AISportCoach.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChangeVideoUploadUserFKToApplicationUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_VideoUploads_UserProfiles_UserId",
                table: "VideoUploads");

            migrationBuilder.AddForeignKey(
                name: "FK_VideoUploads_AspNetUsers_UserId",
                table: "VideoUploads",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_VideoUploads_AspNetUsers_UserId",
                table: "VideoUploads");

            migrationBuilder.AddForeignKey(
                name: "FK_VideoUploads_UserProfiles_UserId",
                table: "VideoUploads",
                column: "UserId",
                principalTable: "UserProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
