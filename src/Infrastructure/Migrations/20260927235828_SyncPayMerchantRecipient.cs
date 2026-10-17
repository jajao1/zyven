using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SyncPayMerchantRecipient : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProviderRecipientId",
                table: "MerchantAccounts",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MerchantAccounts_ProviderRecipientId",
                table: "MerchantAccounts",
                column: "ProviderRecipientId",
                unique: true,
                filter: "\"ProviderRecipientId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MerchantAccounts_ProviderRecipientId",
                table: "MerchantAccounts");

            migrationBuilder.DropColumn(
                name: "ProviderRecipientId",
                table: "MerchantAccounts");
        }
    }
}
