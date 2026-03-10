using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudioStudio_Server.Migrations
{
    /// <inheritdoc />
    public partial class ConvertPaymentStatusToEnum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Add temporary column for integer values
            migrationBuilder.AddColumn<int>(
                name: "PaymentStatusTemp",
                table: "Payments",
                type: "integer",
                nullable: true);

            // Step 2: Convert existing string values to enum integers
            // PENDING = 0, SUCCESS = 1, CANCELLED = 2, FAILED = 3
            migrationBuilder.Sql(@"
                UPDATE ""Payments""
                SET ""PaymentStatusTemp"" = 
                    CASE ""PaymentStatus""
                        WHEN 'PENDING' THEN 0
                        WHEN 'SUCCESS' THEN 1
                        WHEN 'CANCELLED' THEN 2
                        WHEN 'FAILED' THEN 3
                        ELSE 0
                    END
            ");

            // Step 3: Drop old column
            migrationBuilder.DropColumn(
                name: "PaymentStatus",
                table: "Payments");

            // Step 4: Rename temp column to original name
            migrationBuilder.RenameColumn(
                name: "PaymentStatusTemp",
                table: "Payments",
                newName: "PaymentStatus");

            // Step 5: Make column non-nullable with default value
            migrationBuilder.AlterColumn<int>(
                name: "PaymentStatus",
                table: "Payments",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Step 1: Add temporary text column
            migrationBuilder.AddColumn<string>(
                name: "PaymentStatusTemp",
                table: "Payments",
                type: "text",
                nullable: true);

            // Step 2: Convert enum integers back to strings
            migrationBuilder.Sql(@"
                UPDATE ""Payments""
                SET ""PaymentStatusTemp"" = 
                    CASE ""PaymentStatus""
                        WHEN 0 THEN 'PENDING'
                        WHEN 1 THEN 'SUCCESS'
                        WHEN 2 THEN 'CANCELLED'
                        WHEN 3 THEN 'FAILED'
                        ELSE 'PENDING'
                    END
            ");

            // Step 3: Drop integer column
            migrationBuilder.DropColumn(
                name: "PaymentStatus",
                table: "Payments");

            // Step 4: Rename temp column back
            migrationBuilder.RenameColumn(
                name: "PaymentStatusTemp",
                table: "Payments",
                newName: "PaymentStatus");

            // Step 5: Make column non-nullable
            migrationBuilder.AlterColumn<string>(
                name: "PaymentStatus",
                table: "Payments",
                type: "text",
                nullable: false,
                defaultValue: "PENDING");
        }
    }
}
