using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudioStudio_Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUserAnalyticsTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TaskPerformanceMetrics");

            migrationBuilder.DropTable(
                name: "UserActivityMetrics");

            migrationBuilder.DropTable(
                name: "UserProductivityScores");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TaskPerformanceMetrics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActualHours = table.Column<decimal>(type: "numeric", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedOnTime = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DaysEarlyOrLate = table.Column<int>(type: "integer", nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EstimatedHours = table.Column<decimal>(type: "numeric", nullable: true),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    HourVariance = table.Column<double>(type: "double precision", nullable: false),
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
                    CommentsPosted = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    MessagesSent = table.Column<int>(type: "integer", nullable: false),
                    TasksCompleted = table.Column<int>(type: "integer", nullable: false),
                    TasksCreated = table.Column<int>(type: "integer", nullable: false),
                    TotalActivityCount = table.Column<int>(type: "integer", nullable: false),
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
                    GroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AverageTaskCompletionHours = table.Column<double>(type: "double precision", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OnTimeCompletionRate = table.Column<double>(type: "double precision", nullable: false),
                    ProductivityScore = table.Column<double>(type: "double precision", nullable: false),
                    TasksCompleted = table.Column<int>(type: "integer", nullable: false),
                    TasksCreated = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    WeekStart = table.Column<DateOnly>(type: "date", nullable: false)
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
        }
    }
}
