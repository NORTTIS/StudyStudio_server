using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudioStudio_Server.Migrations
{
    /// <inheritdoc />
    public partial class SystemAnnoucementMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AnnouncementId1",
                table: "UserAnnouncements",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserAnnouncements_AnnouncementId1",
                table: "UserAnnouncements",
                column: "AnnouncementId1");

            migrationBuilder.AddForeignKey(
                name: "FK_UserAnnouncements_Announcements_AnnouncementId1",
                table: "UserAnnouncements",
                column: "AnnouncementId1",
                principalTable: "Announcements",
                principalColumn: "AnnouncementId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserAnnouncements_Announcements_AnnouncementId1",
                table: "UserAnnouncements");

            migrationBuilder.DropIndex(
                name: "IX_UserAnnouncements_AnnouncementId1",
                table: "UserAnnouncements");

            migrationBuilder.DropColumn(
                name: "AnnouncementId1",
                table: "UserAnnouncements");
        }
    }
}
