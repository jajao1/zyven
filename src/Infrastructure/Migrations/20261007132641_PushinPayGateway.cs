using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PushinPayGateway : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CallbackSecretCiphertext",
                table: "MerchantAccounts",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CallbackSecretHash",
                table: "MerchantAccounts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CallbackSecretNonce",
                table: "MerchantAccounts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CallbackSecretTag",
                table: "MerchantAccounts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CredentialCiphertext",
                table: "MerchantAccounts",
                type: "character varying(4096)",
                maxLength: 4096,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CredentialFingerprint",
                table: "MerchantAccounts",
                type: "character varying(12)",
                maxLength: 12,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CredentialNonce",
                table: "MerchantAccounts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CredentialTag",
                table: "MerchantAccounts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Provider",
                table: "MerchantAccounts",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MerchantAccounts_CallbackSecretHash",
                table: "MerchantAccounts",
                column: "CallbackSecretHash",
                unique: true,
                filter: "\"CallbackSecretHash\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MerchantAccounts_PushinPayCredentials",
                table: "MerchantAccounts",
                sql: "(\"Provider\" IS NULL AND \"CredentialCiphertext\" IS NULL AND \"CredentialNonce\" IS NULL AND \"CredentialTag\" IS NULL AND \"CredentialFingerprint\" IS NULL AND \"CallbackSecretCiphertext\" IS NULL AND \"CallbackSecretNonce\" IS NULL AND \"CallbackSecretTag\" IS NULL AND \"CallbackSecretHash\" IS NULL) OR (\"Provider\" = 'PUSHINPAY' AND \"CredentialCiphertext\" IS NOT NULL AND \"CredentialNonce\" IS NOT NULL AND \"CredentialTag\" IS NOT NULL AND length(\"CredentialFingerprint\") = 12 AND \"CallbackSecretCiphertext\" IS NOT NULL AND \"CallbackSecretNonce\" IS NOT NULL AND \"CallbackSecretTag\" IS NOT NULL AND length(\"CallbackSecretHash\") = 64)");

            migrationBuilder.Sql("""
                UPDATE "MerchantAccounts"
                SET "Status" = 'PENDING', "ProviderRecipientId" = NULL, "PixKey" = NULL,
                    "MerchantName" = NULL, "MerchantCity" = NULL, "MerchantPostalCode" = NULL,
                    "UpdatedAt" = NOW();
                UPDATE "LedgerAccounts"
                SET "Code" = 'PAYMENT_PROCESSOR_CLEARING', "Name" = 'Payment processor clearing'
                WHERE "Code" = 'CELCOIN_CLEARING';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "LedgerAccounts"
                SET "Code" = 'CELCOIN_CLEARING', "Name" = 'Celcoin clearing'
                WHERE "Code" = 'PAYMENT_PROCESSOR_CLEARING';
                """);
            migrationBuilder.DropIndex(
                name: "IX_MerchantAccounts_CallbackSecretHash",
                table: "MerchantAccounts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MerchantAccounts_PushinPayCredentials",
                table: "MerchantAccounts");

            migrationBuilder.DropColumn(
                name: "CallbackSecretCiphertext",
                table: "MerchantAccounts");

            migrationBuilder.DropColumn(
                name: "CallbackSecretHash",
                table: "MerchantAccounts");

            migrationBuilder.DropColumn(
                name: "CallbackSecretNonce",
                table: "MerchantAccounts");

            migrationBuilder.DropColumn(
                name: "CallbackSecretTag",
                table: "MerchantAccounts");

            migrationBuilder.DropColumn(
                name: "CredentialCiphertext",
                table: "MerchantAccounts");

            migrationBuilder.DropColumn(
                name: "CredentialFingerprint",
                table: "MerchantAccounts");

            migrationBuilder.DropColumn(
                name: "CredentialNonce",
                table: "MerchantAccounts");

            migrationBuilder.DropColumn(
                name: "CredentialTag",
                table: "MerchantAccounts");

            migrationBuilder.DropColumn(
                name: "Provider",
                table: "MerchantAccounts");
        }
    }
}
