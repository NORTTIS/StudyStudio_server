using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudioStudio_Server.Migrations
{
    /// <inheritdoc />
    public partial class AddForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Delete orphaned records before adding FK constraints

            // Delete ActivityLogs with non-existent UserId
            migrationBuilder.Sql(@"
                DELETE FROM ""ActivityLogs""
                WHERE ""UserId"" IS NOT NULL
                AND NOT EXISTS (SELECT 1 FROM ""Users"" WHERE ""UserId"" = ""ActivityLogs"".""UserId"")
            ");

            // Delete ActivityLogs with non-existent GroupId
            migrationBuilder.Sql(@"
                DELETE FROM ""ActivityLogs""
                WHERE ""GroupId"" IS NOT NULL
                AND NOT EXISTS (SELECT 1 FROM ""Groups"" WHERE ""GroupId"" = ""ActivityLogs"".""GroupId"")
            ");

            // Delete ActivityLogs with non-existent StudioId
            migrationBuilder.Sql(@"
                DELETE FROM ""ActivityLogs""
                WHERE ""StudioId"" IS NOT NULL
                AND NOT EXISTS (SELECT 1 FROM ""Studios"" WHERE ""StudioId"" = ""ActivityLogs"".""StudioId"")
            ");

            // Delete AIRequestLogs with non-existent UserId
            migrationBuilder.Sql(@"
                DELETE FROM ""AIRequestLogs""
                WHERE NOT EXISTS (SELECT 1 FROM ""Users"" WHERE ""UserId"" = ""AIRequestLogs"".""UserId"")
            ");

            // Delete GroupAttachments with non-existent GroupId
            migrationBuilder.Sql(@"
                DELETE FROM ""GroupAttachments""
                WHERE NOT EXISTS (SELECT 1 FROM ""Groups"" WHERE ""GroupId"" = ""GroupAttachments"".""GroupId"")
            ");

            // Delete GroupAttachments with non-existent UploadedBy
            migrationBuilder.Sql(@"
                DELETE FROM ""GroupAttachments""
                WHERE NOT EXISTS (SELECT 1 FROM ""Users"" WHERE ""UserId"" = ""GroupAttachments"".""UploadedBy"")
            ");

            // Set Reports.UserId to NULL where UserId doesn't exist (since UserId is nullable)
            migrationBuilder.Sql(@"
                UPDATE ""Reports""
                SET ""UserId"" = NULL
                WHERE ""UserId"" IS NOT NULL
                AND NOT EXISTS (SELECT 1 FROM ""Users"" WHERE ""UserId"" = ""Reports"".""UserId"")
            ");

            migrationBuilder.DropTable(
                name: "PersonalAttachments");

            migrationBuilder.DropTable(
                name: "TaskHistories");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_UserId",
                table: "Reports",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupAttachments_GroupId",
                table: "GroupAttachments",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupAttachments_UploadedBy",
                table: "GroupAttachments",
                column: "UploadedBy");

            migrationBuilder.CreateIndex(
                name: "IX_AIRequestLogs_UserId",
                table: "AIRequestLogs",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ActivityLogs_Groups_GroupId",
                table: "ActivityLogs",
                column: "GroupId",
                principalTable: "Groups",
                principalColumn: "GroupId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ActivityLogs_Studios_StudioId",
                table: "ActivityLogs",
                column: "StudioId",
                principalTable: "Studios",
                principalColumn: "StudioId",
                onDelete: ReferentialAction.SetNull);

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
                name: "FK_GroupAttachments_Groups_GroupId",
                table: "GroupAttachments",
                column: "GroupId",
                principalTable: "Groups",
                principalColumn: "GroupId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GroupAttachments_Users_UploadedBy",
                table: "GroupAttachments",
                column: "UploadedBy",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Reports_Users_UserId",
                table: "Reports",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ActivityLogs_Groups_GroupId",
                table: "ActivityLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_ActivityLogs_Studios_StudioId",
                table: "ActivityLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_ActivityLogs_Users_UserId",
                table: "ActivityLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_AIRequestLogs_Users_UserId",
                table: "AIRequestLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_GroupAttachments_Groups_GroupId",
                table: "GroupAttachments");

            migrationBuilder.DropForeignKey(
                name: "FK_GroupAttachments_Users_UploadedBy",
                table: "GroupAttachments");

            migrationBuilder.DropForeignKey(
                name: "FK_Reports_Users_UserId",
                table: "Reports");

            migrationBuilder.DropIndex(
                name: "IX_Reports_UserId",
                table: "Reports");

            migrationBuilder.DropIndex(
                name: "IX_GroupAttachments_GroupId",
                table: "GroupAttachments");

            migrationBuilder.DropIndex(
                name: "IX_GroupAttachments_UploadedBy",
                table: "GroupAttachments");

            migrationBuilder.DropIndex(
                name: "IX_AIRequestLogs_UserId",
                table: "AIRequestLogs");

            migrationBuilder.CreateTable(
                name: "PersonalAttachments",
                columns: table => new
                {
                    AttachmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    FileType = table.Column<string>(type: "text", nullable: false),
                    FileUrl = table.Column<string>(type: "text", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonalAttachments", x => x.AttachmentId);
                });

            migrationBuilder.CreateTable(
                name: "TaskHistories",
                columns: table => new
                {
                    HistoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ChangedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ChangedContent = table.Column<string>(type: "text", nullable: true),
                    StatusId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskHistories", x => x.HistoryId);
                });
        }
    }
}
