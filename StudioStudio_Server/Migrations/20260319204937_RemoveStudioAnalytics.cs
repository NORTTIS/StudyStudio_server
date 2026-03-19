using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudioStudio_Server.Migrations
{
    /// <inheritdoc />
    public partial class RemoveStudioAnalytics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StudioAnalytics");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StudioAnalytics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudioId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActiveGroups = table.Column<int>(type: "integer", nullable: false),
                    ActiveMembers = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    EngagementScore = table.Column<double>(type: "double precision", nullable: false),
                    OverallCompletionRate = table.Column<double>(type: "double precision", nullable: false),
                    TasksCompleted = table.Column<int>(type: "integer", nullable: false),
                    TotalGroups = table.Column<int>(type: "integer", nullable: false),
                    TotalMembers = table.Column<int>(type: "integer", nullable: false),
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

            migrationBuilder.CreateIndex(
                name: "IX_StudioAnalytics_StudioId_Date",
                table: "StudioAnalytics",
                columns: new[] { "StudioId", "Date" },
                unique: true);
        }
    }
}
