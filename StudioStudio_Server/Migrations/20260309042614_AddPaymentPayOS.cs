using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudioStudio_Server.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentPayOS : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SubscriptionId",
                table: "Payments",
                newName: "PlanId");

            migrationBuilder.AddColumn<long>(
                name: "OrderCode",
                table: "Payments",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTime>(
                name: "PaidAt",
                table: "Payments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentUrl",
                table: "Payments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransactionId",
                table: "Payments",
                type: "text",
                nullable: true);

            migrationBuilder.Sql(@"
                WITH numbered_payments AS (
                    SELECT ""PaymentId"", 1000000000000 + ROW_NUMBER() OVER (ORDER BY ""CreatedAt"", ""PaymentId"") AS new_order_code
                    FROM ""Payments""
                    WHERE ""OrderCode"" = 0
                )
                UPDATE ""Payments"" p
                SET ""OrderCode"" = np.new_order_code
                FROM numbered_payments np
                WHERE p.""PaymentId"" = np.""PaymentId"";
            ");

            migrationBuilder.AlterColumn<long>(
                name: "OrderCode",
                table: "Payments",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldDefaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_OrderCode",
                table: "Payments",
                column: "OrderCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_PlanId",
                table: "Payments",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_UserId",
                table: "Payments",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_SubscriptionPlans_PlanId",
                table: "Payments",
                column: "PlanId",
                principalTable: "SubscriptionPlans",
                principalColumn: "PlanId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Users_UserId",
                table: "Payments",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Payments_SubscriptionPlans_PlanId",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Users_UserId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_OrderCode",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_PlanId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_UserId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "OrderCode",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "PaidAt",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "PaymentUrl",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "TransactionId",
                table: "Payments");

            migrationBuilder.RenameColumn(
                name: "PlanId",
                table: "Payments",
                newName: "SubscriptionId");
        }
    }
}
