using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudioStudio_Server.Migrations
{
    /// <inheritdoc />
    public partial class AddDiscussionThreadingSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Comments");

            migrationBuilder.AddColumn<Guid>(
                name: "ParentCommentId",
                table: "TaskComments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentMessageId",
                table: "GroupMessages",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaskComments_ParentCommentId",
                table: "TaskComments",
                column: "ParentCommentId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupMessages_ParentMessageId",
                table: "GroupMessages",
                column: "ParentMessageId");

            migrationBuilder.AddForeignKey(
                name: "FK_GroupMessages_GroupMessages_ParentMessageId",
                table: "GroupMessages",
                column: "ParentMessageId",
                principalTable: "GroupMessages",
                principalColumn: "MessageId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TaskComments_TaskComments_ParentCommentId",
                table: "TaskComments",
                column: "ParentCommentId",
                principalTable: "TaskComments",
                principalColumn: "CommentId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GroupMessages_GroupMessages_ParentMessageId",
                table: "GroupMessages");

            migrationBuilder.DropForeignKey(
                name: "FK_TaskComments_TaskComments_ParentCommentId",
                table: "TaskComments");

            migrationBuilder.DropIndex(
                name: "IX_TaskComments_ParentCommentId",
                table: "TaskComments");

            migrationBuilder.DropIndex(
                name: "IX_GroupMessages_ParentMessageId",
                table: "GroupMessages");

            migrationBuilder.DropColumn(
                name: "ParentCommentId",
                table: "TaskComments");

            migrationBuilder.DropColumn(
                name: "ParentMessageId",
                table: "GroupMessages");

            migrationBuilder.CreateTable(
                name: "Comments",
                columns: table => new
                {
                    CommentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Comments", x => x.CommentId);
                });
        }
    }
}
