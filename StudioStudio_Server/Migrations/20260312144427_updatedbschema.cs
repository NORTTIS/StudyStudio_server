using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudioStudio_Server.Migrations
{
    /// <inheritdoc />
    public partial class updatedbschema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserAnnouncements_Users_MetionedId",
                table: "UserAnnouncements");

            migrationBuilder.RenameColumn(
                name: "MetionedId",
                table: "UserAnnouncements",
                newName: "MentionedId");

            migrationBuilder.RenameIndex(
                name: "IX_UserAnnouncements_MetionedId",
                table: "UserAnnouncements",
                newName: "IX_UserAnnouncements_MentionedId");

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                table: "UserAnnouncements",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserAnnouncements_CreatedBy",
                table: "UserAnnouncements",
                column: "CreatedBy");

            migrationBuilder.AddForeignKey(
                name: "FK_UserAnnouncements_Users_CreatedBy",
                table: "UserAnnouncements",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_UserAnnouncements_Users_MentionedId",
                table: "UserAnnouncements",
                column: "MentionedId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserAnnouncements_Users_CreatedBy",
                table: "UserAnnouncements");

            migrationBuilder.DropForeignKey(
                name: "FK_UserAnnouncements_Users_MentionedId",
                table: "UserAnnouncements");

            migrationBuilder.DropIndex(
                name: "IX_UserAnnouncements_CreatedBy",
                table: "UserAnnouncements");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "UserAnnouncements");

            migrationBuilder.RenameColumn(
                name: "MentionedId",
                table: "UserAnnouncements",
                newName: "MetionedId");

            migrationBuilder.RenameIndex(
                name: "IX_UserAnnouncements_MentionedId",
                table: "UserAnnouncements",
                newName: "IX_UserAnnouncements_MetionedId");

            migrationBuilder.AddForeignKey(
                name: "FK_UserAnnouncements_Users_MetionedId",
                table: "UserAnnouncements",
                column: "MetionedId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
