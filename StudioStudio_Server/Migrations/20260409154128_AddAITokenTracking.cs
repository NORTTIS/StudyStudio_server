using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudioStudio_Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAITokenTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AILayer",
                table: "AIRequestLogs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CachedTokens",
                table: "AIRequestLogs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "ContextId",
                table: "AIRequestLogs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InputTokens",
                table: "AIRequestLogs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OutputTokens",
                table: "AIRequestLogs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "ProcessingTimeMs",
                table: "AIRequestLogs",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "ThinkingTokens",
                table: "AIRequestLogs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ToolCallCount",
                table: "AIRequestLogs",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AILayer",
                table: "AIRequestLogs");

            migrationBuilder.DropColumn(
                name: "CachedTokens",
                table: "AIRequestLogs");

            migrationBuilder.DropColumn(
                name: "ContextId",
                table: "AIRequestLogs");

            migrationBuilder.DropColumn(
                name: "InputTokens",
                table: "AIRequestLogs");

            migrationBuilder.DropColumn(
                name: "OutputTokens",
                table: "AIRequestLogs");

            migrationBuilder.DropColumn(
                name: "ProcessingTimeMs",
                table: "AIRequestLogs");

            migrationBuilder.DropColumn(
                name: "ThinkingTokens",
                table: "AIRequestLogs");

            migrationBuilder.DropColumn(
                name: "ToolCallCount",
                table: "AIRequestLogs");
        }
    }
}
