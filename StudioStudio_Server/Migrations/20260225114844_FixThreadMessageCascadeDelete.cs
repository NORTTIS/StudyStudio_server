using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudioStudio_Server.Migrations
{
    /// <inheritdoc />
    public partial class FixThreadMessageCascadeDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GroupMessages_GroupMessages_ParentMessageId",
                table: "GroupMessages");

            migrationBuilder.DropForeignKey(
                name: "FK_TaskComments_TaskComments_ParentCommentId",
                table: "TaskComments");

            migrationBuilder.AddForeignKey(
                name: "FK_GroupMessages_GroupMessages_ParentMessageId",
                table: "GroupMessages",
                column: "ParentMessageId",
                principalTable: "GroupMessages",
                principalColumn: "MessageId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TaskComments_TaskComments_ParentCommentId",
                table: "TaskComments",
                column: "ParentCommentId",
                principalTable: "TaskComments",
                principalColumn: "CommentId",
                onDelete: ReferentialAction.Restrict);
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
    }
}
