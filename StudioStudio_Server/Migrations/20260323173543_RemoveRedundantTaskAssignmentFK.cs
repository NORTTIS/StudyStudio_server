using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudioStudio_Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveRedundantTaskAssignmentFK : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TaskAssignments_Users_AssignedToUserUserId",
                table: "TaskAssignments");

            migrationBuilder.DropIndex(
                name: "IX_TaskAssignments_AssignedToUserUserId",
                table: "TaskAssignments");

            migrationBuilder.DropColumn(
                name: "AssignedToUserUserId",
                table: "TaskAssignments");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AssignedToUserUserId",
                table: "TaskAssignments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaskAssignments_AssignedToUserUserId",
                table: "TaskAssignments",
                column: "AssignedToUserUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_TaskAssignments_Users_AssignedToUserUserId",
                table: "TaskAssignments",
                column: "AssignedToUserUserId",
                principalTable: "Users",
                principalColumn: "UserId");
        }
    }
}
