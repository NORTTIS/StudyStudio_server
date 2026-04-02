using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudioStudio_Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIsOpenAndIsApproved : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StudioParticipants_StudioId_UserId",
                table: "StudioParticipants");

            migrationBuilder.DropIndex(
                name: "IX_GroupParticipants_GroupId_UserId",
                table: "GroupParticipants");

            // 🔹 ADDED: IsOpen columns
            migrationBuilder.AddColumn<bool>(
                name: "IsOpen",
                table: "Studios",
                type: "boolean",
                nullable: false,
                defaultValue: true); // New studios default to open

            migrationBuilder.AddColumn<bool>(
                name: "IsOpen",
                table: "Groups",
                type: "boolean",
                nullable: false,
                defaultValue: true); // New groups default to open

            // 🔹 ADDED: IsApproved columns
            migrationBuilder.AddColumn<bool>(
                name: "IsApproved",
                table: "StudioParticipants",
                type: "boolean",
                nullable: false,
                defaultValue: true); // Existing members are already approved

            migrationBuilder.AddColumn<bool>(
                name: "IsApproved",
                table: "GroupParticipants",
                type: "boolean",
                nullable: false,
                defaultValue: true); // Existing members are already approved

            // Create filtered unique indexes (only approved participants)
            migrationBuilder.CreateIndex(
                name: "IX_StudioParticipants_StudioId_UserId",
                table: "StudioParticipants",
                columns: new[] { "StudioId", "UserId" },
                unique: true,
                filter: "\"IsApproved\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_GroupParticipants_GroupId_UserId",
                table: "GroupParticipants",
                columns: new[] { "GroupId", "UserId" },
                unique: true,
                filter: "\"IsApproved\" = true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StudioParticipants_StudioId_UserId",
                table: "StudioParticipants");

            migrationBuilder.DropIndex(
                name: "IX_GroupParticipants_GroupId_UserId",
                table: "GroupParticipants");

            migrationBuilder.DropColumn(
                name: "IsOpen",
                table: "Studios");

            migrationBuilder.DropColumn(
                name: "IsApproved",
                table: "StudioParticipants");

            migrationBuilder.DropColumn(
                name: "IsOpen",
                table: "Groups");

            migrationBuilder.DropColumn(
                name: "IsApproved",
                table: "GroupParticipants");

            migrationBuilder.CreateIndex(
                name: "IX_StudioParticipants_StudioId_UserId",
                table: "StudioParticipants",
                columns: new[] { "StudioId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupParticipants_GroupId_UserId",
                table: "GroupParticipants",
                columns: new[] { "GroupId", "UserId" },
                unique: true);
        }
    }
}
