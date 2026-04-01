using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudioStudio_Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupAndStudioPersonalization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Alias",
                table: "Studios",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BannerUrl",
                table: "Studios",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogoUrl",
                table: "Studios",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tagline",
                table: "Studios",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Alias",
                table: "Groups",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BannerUrl",
                table: "Groups",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tagline",
                table: "Groups",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Alias",
                table: "Studios");

            migrationBuilder.DropColumn(
                name: "BannerUrl",
                table: "Studios");

            migrationBuilder.DropColumn(
                name: "LogoUrl",
                table: "Studios");

            migrationBuilder.DropColumn(
                name: "Tagline",
                table: "Studios");

            migrationBuilder.DropColumn(
                name: "Alias",
                table: "Groups");

            migrationBuilder.DropColumn(
                name: "BannerUrl",
                table: "Groups");

            migrationBuilder.DropColumn(
                name: "Tagline",
                table: "Groups");
        }
    }
}
