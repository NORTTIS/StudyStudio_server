using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudioStudio_Server.Migrations
{
    /// <inheritdoc />
    public partial class TaskProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Progress",
                table: "Tasks",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Progress",
                table: "Tasks");
        }
    }
}
