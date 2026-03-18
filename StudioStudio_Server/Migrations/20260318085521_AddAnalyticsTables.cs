using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudioStudio_Server.Migrations
{
    /// <inheritdoc />
    public partial class AddAnalyticsTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ActualHours",
                table: "Tasks",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "Tasks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedHours",
                table: "Tasks",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AssignedToUserUserId",
                table: "TaskAssignments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "TaskAssignments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCompleted",
                table: "TaskAssignments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "GroupId",
                table: "ActivityLogs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Metadata",
                table: "ActivityLogs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StudioId",
                table: "ActivityLogs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TargetId",
                table: "ActivityLogs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GroupAnalytics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    TotalTasks = table.Column<int>(type: "integer", nullable: false),
                    CompletedTasks = table.Column<int>(type: "integer", nullable: false),
                    OverdueTasks = table.Column<int>(type: "integer", nullable: false),
                    ActiveMembers = table.Column<int>(type: "integer", nullable: false),
                    MessagesCount = table.Column<int>(type: "integer", nullable: false),
                    CommentsCount = table.Column<int>(type: "integer", nullable: false),
                    CompletionRate = table.Column<double>(type: "double precision", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupAnalytics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupAnalytics_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "GroupId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudioAnalytics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    TotalGroups = table.Column<int>(type: "integer", nullable: false),
                    ActiveGroups = table.Column<int>(type: "integer", nullable: false),
                    TotalMembers = table.Column<int>(type: "integer", nullable: false),
                    ActiveMembers = table.Column<int>(type: "integer", nullable: false),
                    TasksCompleted = table.Column<int>(type: "integer", nullable: false),
                    OverallCompletionRate = table.Column<double>(type: "double precision", nullable: false),
                    EngagementScore = table.Column<double>(type: "double precision", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudioAnalytics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudioAnalytics_Studios_StudioId",
                        column: x => x.StudioId,
                        principalTable: "Studios",
                        principalColumn: "StudioId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaskPerformanceMetrics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    EstimatedHours = table.Column<decimal>(type: "numeric", nullable: true),
                    ActualHours = table.Column<decimal>(type: "numeric", nullable: true),
                    HourVariance = table.Column<double>(type: "double precision", nullable: false),
                    CompletedOnTime = table.Column<bool>(type: "boolean", nullable: false),
                    DaysEarlyOrLate = table.Column<int>(type: "integer", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskPerformanceMetrics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskPerformanceMetrics_Tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "Tasks",
                        principalColumn: "TaskId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TaskPerformanceMetrics_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserActivityMetrics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    TasksCreated = table.Column<int>(type: "integer", nullable: false),
                    TasksCompleted = table.Column<int>(type: "integer", nullable: false),
                    CommentsPosted = table.Column<int>(type: "integer", nullable: false),
                    MessagesSent = table.Column<int>(type: "integer", nullable: false),
                    TotalActivityCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserActivityMetrics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserActivityMetrics_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserProductivityScores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    WeekStart = table.Column<DateOnly>(type: "date", nullable: false),
                    ProductivityScore = table.Column<double>(type: "double precision", nullable: false),
                    TasksCompleted = table.Column<int>(type: "integer", nullable: false),
                    TasksCreated = table.Column<int>(type: "integer", nullable: false),
                    OnTimeCompletionRate = table.Column<double>(type: "double precision", nullable: false),
                    AverageTaskCompletionHours = table.Column<double>(type: "double precision", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserProductivityScores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserProductivityScores_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "GroupId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserProductivityScores_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TaskAssignments_AssignedToUserUserId",
                table: "TaskAssignments",
                column: "AssignedToUserUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogs_ActionType",
                table: "ActivityLogs",
                column: "ActionType");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogs_GroupId_CreatedAt",
                table: "ActivityLogs",
                columns: new[] { "GroupId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogs_StudioId_CreatedAt",
                table: "ActivityLogs",
                columns: new[] { "StudioId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogs_UserId_CreatedAt",
                table: "ActivityLogs",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GroupAnalytics_GroupId_Date",
                table: "GroupAnalytics",
                columns: new[] { "GroupId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudioAnalytics_StudioId_Date",
                table: "StudioAnalytics",
                columns: new[] { "StudioId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaskPerformanceMetrics_TaskId",
                table: "TaskPerformanceMetrics",
                column: "TaskId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaskPerformanceMetrics_UserId",
                table: "TaskPerformanceMetrics",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserActivityMetrics_UserId_Date",
                table: "UserActivityMetrics",
                columns: new[] { "UserId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserProductivityScores_GroupId",
                table: "UserProductivityScores",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_UserProductivityScores_UserId_GroupId_WeekStart",
                table: "UserProductivityScores",
                columns: new[] { "UserId", "GroupId", "WeekStart" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_TaskAssignments_Tasks_TaskId",
                table: "TaskAssignments",
                column: "TaskId",
                principalTable: "Tasks",
                principalColumn: "TaskId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TaskAssignments_Users_AssignedToUserUserId",
                table: "TaskAssignments",
                column: "AssignedToUserUserId",
                principalTable: "Users",
                principalColumn: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TaskAssignments_Tasks_TaskId",
                table: "TaskAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_TaskAssignments_Users_AssignedToUserUserId",
                table: "TaskAssignments");

            migrationBuilder.DropTable(
                name: "GroupAnalytics");

            migrationBuilder.DropTable(
                name: "StudioAnalytics");

            migrationBuilder.DropTable(
                name: "TaskPerformanceMetrics");

            migrationBuilder.DropTable(
                name: "UserActivityMetrics");

            migrationBuilder.DropTable(
                name: "UserProductivityScores");

            migrationBuilder.DropIndex(
                name: "IX_TaskAssignments_AssignedToUserUserId",
                table: "TaskAssignments");

            migrationBuilder.DropIndex(
                name: "IX_ActivityLogs_ActionType",
                table: "ActivityLogs");

            migrationBuilder.DropIndex(
                name: "IX_ActivityLogs_GroupId_CreatedAt",
                table: "ActivityLogs");

            migrationBuilder.DropIndex(
                name: "IX_ActivityLogs_StudioId_CreatedAt",
                table: "ActivityLogs");

            migrationBuilder.DropIndex(
                name: "IX_ActivityLogs_UserId_CreatedAt",
                table: "ActivityLogs");

            migrationBuilder.DropColumn(
                name: "ActualHours",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "EstimatedHours",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "AssignedToUserUserId",
                table: "TaskAssignments");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "TaskAssignments");

            migrationBuilder.DropColumn(
                name: "IsCompleted",
                table: "TaskAssignments");

            migrationBuilder.DropColumn(
                name: "GroupId",
                table: "ActivityLogs");

            migrationBuilder.DropColumn(
                name: "Metadata",
                table: "ActivityLogs");

            migrationBuilder.DropColumn(
                name: "StudioId",
                table: "ActivityLogs");

            migrationBuilder.DropColumn(
                name: "TargetId",
                table: "ActivityLogs");
        }
    }
}
