using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudioStudio_Server.Migrations
{
    /// <inheritdoc />
    public partial class AddDeletedFlagToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DeletedFlag",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletedFlag",
                table: "Users");
        }
    }
}
