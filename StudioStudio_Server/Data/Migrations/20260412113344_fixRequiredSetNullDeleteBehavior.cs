using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudioStudio_Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class fixRequiredSetNullDeleteBehavior : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ActivityLogs_Users_UserId",
                table: "ActivityLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_AIRequestLogs_Users_UserId",
                table: "AIRequestLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_GroupAttachments_Users_UploadedBy",
                table: "GroupAttachments");

            migrationBuilder.AddForeignKey(
                name: "FK_ActivityLogs_Users_UserId",
                table: "ActivityLogs",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AIRequestLogs_Users_UserId",
                table: "AIRequestLogs",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GroupAttachments_Users_UploadedBy",
                table: "GroupAttachments",
                column: "UploadedBy",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ActivityLogs_Users_UserId",
                table: "ActivityLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_AIRequestLogs_Users_UserId",
                table: "AIRequestLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_GroupAttachments_Users_UploadedBy",
                table: "GroupAttachments");

            migrationBuilder.AddForeignKey(
                name: "FK_ActivityLogs_Users_UserId",
                table: "ActivityLogs",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_AIRequestLogs_Users_UserId",
                table: "AIRequestLogs",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_GroupAttachments_Users_UploadedBy",
                table: "GroupAttachments",
                column: "UploadedBy",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
