using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudioStudio_Server.Migrations
{
    /// <inheritdoc />
    public partial class TaskStartDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "StartDate",
                table: "Tasks",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "Tasks");
        }
    }
}
