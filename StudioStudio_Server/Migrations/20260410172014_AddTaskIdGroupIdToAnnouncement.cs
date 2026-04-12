using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudioStudio_Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskIdGroupIdToAnnouncement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "GroupId",
                table: "Announcements",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_GroupId",
                table: "Announcements",
                column: "GroupId");

            migrationBuilder.AddColumn<string>(
                name: "SourceType",
                table: "Announcements",
                type: "text",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Announcements_SourceType",
                table: "Announcements",
                sql: "\"SourceType\" IS NULL OR \"SourceType\" IN ('announcement', 'task', 'discuss', 'comment')");

            migrationBuilder.AddColumn<Guid>(
                name: "TaskId",
                table: "Announcements",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_TaskId",
                table: "Announcements",
                column: "TaskId");

            migrationBuilder.AddForeignKey(
                name: "FK_Announcements_Groups_GroupId",
                table: "Announcements",
                column: "GroupId",
                principalTable: "Groups",
                principalColumn: "GroupId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Announcements_Tasks_TaskId",
                table: "Announcements",
                column: "TaskId",
                principalTable: "Tasks",
                principalColumn: "TaskId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Announcements_Groups_GroupId",
                table: "Announcements");

            migrationBuilder.DropForeignKey(
                name: "FK_Announcements_Tasks_TaskId",
                table: "Announcements");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Announcements_SourceType",
                table: "Announcements");

            migrationBuilder.DropIndex(
                name: "IX_Announcements_GroupId",
                table: "Announcements");

            migrationBuilder.DropIndex(
                name: "IX_Announcements_TaskId",
                table: "Announcements");

            migrationBuilder.DropColumn(
                name: "GroupId",
                table: "Announcements");

            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "Announcements");

            migrationBuilder.DropColumn(
                name: "TaskId",
                table: "Announcements");
        }
    }
}
