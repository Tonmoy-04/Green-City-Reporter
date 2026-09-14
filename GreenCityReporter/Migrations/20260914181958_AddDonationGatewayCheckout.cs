using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GreenCityReporter.Migrations
{
    /// <inheritdoc />
    public partial class AddDonationGatewayCheckout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "Donations",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450);

            migrationBuilder.AddColumn<string>(
                name: "BankTransactionId",
                table: "Donations",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CheckoutKey",
                table: "Donations",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CheckoutUrl",
                table: "Donations",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DonorName",
                table: "Donations",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Donations",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmailAttempts",
                table: "Donations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "EmailLeaseUntil",
                table: "Donations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EmailNextAttemptAt",
                table: "Donations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GatewayPaymentType",
                table: "Donations",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GatewayStoreId",
                table: "Donations",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSandbox",
                table: "Donations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastCheckedAt",
                table: "Donations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PaidAt",
                table: "Donations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "Donations",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Provider",
                table: "Donations",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ReceiptEmailSentAt",
                table: "Donations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceiptToken",
                table: "Donations",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValidationId",
                table: "Donations",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Donations_CheckoutKey",
                table: "Donations",
                column: "CheckoutKey",
                unique: true,
                filter: "[CheckoutKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Donations_Provider_IsSandbox_BankTransactionId",
                table: "Donations",
                columns: new[] { "Provider", "IsSandbox", "BankTransactionId" },
                unique: true,
                filter: "[BankTransactionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Donations_Provider_Status_LastCheckedAt",
                table: "Donations",
                columns: new[] { "Provider", "Status", "LastCheckedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Donations_Provider_TransactionId",
                table: "Donations",
                columns: new[] { "Provider", "TransactionId" },
                unique: true,
                filter: "[Provider] = 'SSLCommerz'");

            migrationBuilder.CreateIndex(
                name: "IX_Donations_ReceiptToken",
                table: "Donations",
                column: "ReceiptToken",
                unique: true,
                filter: "[ReceiptToken] IS NOT NULL");
            // Preserve donor identity from the original manual payment records.
            migrationBuilder.Sql(@"
                UPDATE d SET DonorName = LEFT(u.FullName, 50),
                    Email = CASE WHEN LEN(u.Email) <= 50 THEN u.Email ELSE NULL END,
                    Phone = NULLIF(d.SenderPhone, '')
                FROM Donations d INNER JOIN AspNetUsers u ON d.UserId = u.Id
                WHERE d.Provider = 'Manual';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Donations_CheckoutKey",
                table: "Donations");

            migrationBuilder.DropIndex(
                name: "IX_Donations_Provider_IsSandbox_BankTransactionId",
                table: "Donations");

            migrationBuilder.DropIndex(
                name: "IX_Donations_Provider_Status_LastCheckedAt",
                table: "Donations");

            migrationBuilder.DropIndex(
                name: "IX_Donations_Provider_TransactionId",
                table: "Donations");

            migrationBuilder.DropIndex(
                name: "IX_Donations_ReceiptToken",
                table: "Donations");

            migrationBuilder.DropColumn(
                name: "BankTransactionId",
                table: "Donations");

            migrationBuilder.DropColumn(
                name: "CheckoutKey",
                table: "Donations");

            migrationBuilder.DropColumn(
                name: "CheckoutUrl",
                table: "Donations");

            migrationBuilder.DropColumn(
                name: "DonorName",
                table: "Donations");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Donations");

            migrationBuilder.DropColumn(
                name: "EmailAttempts",
                table: "Donations");

            migrationBuilder.DropColumn(
                name: "EmailLeaseUntil",
                table: "Donations");

            migrationBuilder.DropColumn(
                name: "EmailNextAttemptAt",
                table: "Donations");

            migrationBuilder.DropColumn(
                name: "GatewayPaymentType",
                table: "Donations");

            migrationBuilder.DropColumn(
                name: "GatewayStoreId",
                table: "Donations");

            migrationBuilder.DropColumn(
                name: "IsSandbox",
                table: "Donations");

            migrationBuilder.DropColumn(
                name: "LastCheckedAt",
                table: "Donations");

            migrationBuilder.DropColumn(
                name: "PaidAt",
                table: "Donations");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "Donations");

            migrationBuilder.DropColumn(
                name: "Provider",
                table: "Donations");

            migrationBuilder.DropColumn(
                name: "ReceiptEmailSentAt",
                table: "Donations");

            migrationBuilder.DropColumn(
                name: "ReceiptToken",
                table: "Donations");

            migrationBuilder.DropColumn(
                name: "ValidationId",
                table: "Donations");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "Donations",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450,
                oldNullable: true);
        }
    }
}
