using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudioStudio_Server.Migrations
{
    /// <inheritdoc />
    public partial class FixEmailVerificationTokenFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EmailVerificationTokens_Users_UserId1",
                table: "EmailVerificationTokens");

            migrationBuilder.DropForeignKey(
                name: "FK_UserSubscriptions_SubscriptionPlans_PlanId1",
                table: "UserSubscriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_UserSubscriptions_Users_UserId1",
                table: "UserSubscriptions");

            migrationBuilder.DropIndex(
                name: "IX_UserSubscriptions_PlanId1",
                table: "UserSubscriptions");

            migrationBuilder.DropIndex(
                name: "IX_UserSubscriptions_UserId1",
                table: "UserSubscriptions");

            migrationBuilder.DropIndex(
                name: "IX_EmailVerificationTokens_UserId1",
                table: "EmailVerificationTokens");

            migrationBuilder.DropColumn(
                name: "PlanId1",
                table: "UserSubscriptions");

            migrationBuilder.DropColumn(
                name: "UserId1",
                table: "UserSubscriptions");

            migrationBuilder.DropColumn(
                name: "UserId1",
                table: "EmailVerificationTokens");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PlanId1",
                table: "UserSubscriptions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "UserId1",
                table: "UserSubscriptions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "UserId1",
                table: "EmailVerificationTokens",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptions_PlanId1",
                table: "UserSubscriptions",
                column: "PlanId1");

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptions_UserId1",
                table: "UserSubscriptions",
                column: "UserId1");

            migrationBuilder.CreateIndex(
                name: "IX_EmailVerificationTokens_UserId1",
                table: "EmailVerificationTokens",
                column: "UserId1");

            migrationBuilder.AddForeignKey(
                name: "FK_EmailVerificationTokens_Users_UserId1",
                table: "EmailVerificationTokens",
                column: "UserId1",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserSubscriptions_SubscriptionPlans_PlanId1",
                table: "UserSubscriptions",
                column: "PlanId1",
                principalTable: "SubscriptionPlans",
                principalColumn: "PlanId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserSubscriptions_Users_UserId1",
                table: "UserSubscriptions",
                column: "UserId1",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
