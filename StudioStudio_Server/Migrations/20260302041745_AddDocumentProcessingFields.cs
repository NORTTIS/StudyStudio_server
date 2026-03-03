using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudioStudio_Server.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentProcessingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ChunkCount",
                table: "GroupAttachments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ErrorMessage",
                table: "GroupAttachments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "GroupAttachments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProcessedAt",
                table: "GroupAttachments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProcessingStatus",
                table: "GroupAttachments",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChunkCount",
                table: "GroupAttachments");

            migrationBuilder.DropColumn(
                name: "ErrorMessage",
                table: "GroupAttachments");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "GroupAttachments");

            migrationBuilder.DropColumn(
                name: "ProcessedAt",
                table: "GroupAttachments");

            migrationBuilder.DropColumn(
                name: "ProcessingStatus",
                table: "GroupAttachments");
        }
    }
}
