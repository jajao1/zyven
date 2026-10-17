using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConfigurablePaymentFees : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Payments_Amounts",
                table: "Payments");

            migrationBuilder.AddColumn<decimal>(
                name: "ProviderFee",
                table: "Payments",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Payments_Amounts",
                table: "Payments",
                sql: "\"GrossAmount\" > 0 AND \"DiscountAmount\" >= 0 AND \"OrderBumpAmount\" >= 0 AND \"PlatformFee\" >= 0 AND \"ProviderFee\" >= 0 AND \"NetAmount\" >= 0 AND \"NetAmount\" = \"GrossAmount\" - \"PlatformFee\" - \"ProviderFee\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Payments_Amounts",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ProviderFee",
                table: "Payments");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Payments_Amounts",
                table: "Payments",
                sql: "\"GrossAmount\" > 0 AND \"DiscountAmount\" >= 0 AND \"OrderBumpAmount\" >= 0 AND \"PlatformFee\" >= 0 AND \"NetAmount\" >= 0 AND \"NetAmount\" = \"GrossAmount\" - \"PlatformFee\"");
        }
    }
}
