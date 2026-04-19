using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AISportCoach.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UploadedAt",
                table: "VideoUploads",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "GeneratedAt",
                table: "CoachingReports",
                newName: "CreatedAt");

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                table: "VideoUploads",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "LastModifiedAt",
                table: "VideoUploads",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedBy",
                table: "VideoUploads",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                table: "ReportEmbeddings",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "LastModifiedAt",
                table: "ReportEmbeddings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedBy",
                table: "ReportEmbeddings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                table: "CoachingReports",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "LastModifiedAt",
                table: "CoachingReports",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedBy",
                table: "CoachingReports",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "VideoUploads");

            migrationBuilder.DropColumn(
                name: "LastModifiedAt",
                table: "VideoUploads");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                table: "VideoUploads");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ReportEmbeddings");

            migrationBuilder.DropColumn(
                name: "LastModifiedAt",
                table: "ReportEmbeddings");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                table: "ReportEmbeddings");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "CoachingReports");

            migrationBuilder.DropColumn(
                name: "LastModifiedAt",
                table: "CoachingReports");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                table: "CoachingReports");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "VideoUploads",
                newName: "UploadedAt");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "CoachingReports",
                newName: "GeneratedAt");
        }
    }
}
