using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PaymentFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_Checkouts_Id_OrganizationId",
                table: "Checkouts",
                columns: new[] { "Id", "OrganizationId" });

            migrationBuilder.CreateTable(
                name: "MerchantAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "PENDING"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MerchantAccounts", x => x.Id);
                    table.UniqueConstraint("AK_MerchantAccounts_Id_OrganizationId", x => new { x.Id, x.OrganizationId });
                    table.CheckConstraint("CK_MerchantAccounts_Status", "\"Status\" IN ('PENDING','ACTIVE','SUSPENDED','BLOCKED')");
                    table.ForeignKey(
                        name: "FK_MerchantAccounts_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    MerchantAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CheckoutSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OfferId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrossAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OrderBumpAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PlatformFee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    NetAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    PaymentMethod = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Provider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ProviderTransactionId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ExternalReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EndToEndId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PixCode = table.Column<string>(type: "text", nullable: true),
                    QrCodeData = table.Column<string>(type: "text", nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PaidAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.CheckConstraint("CK_Payments_Amounts", "\"GrossAmount\" > 0 AND \"DiscountAmount\" >= 0 AND \"OrderBumpAmount\" >= 0 AND \"PlatformFee\" >= 0 AND \"NetAmount\" >= 0 AND \"NetAmount\" = \"GrossAmount\" - \"PlatformFee\"");
                    table.CheckConstraint("CK_Payments_Currency", "\"Currency\" ~ '^[A-Z]{3}$'");
                    table.CheckConstraint("CK_Payments_Expiry", "\"ExpiresAt\" > \"CreatedAt\"");
                    table.CheckConstraint("CK_Payments_Method", "\"PaymentMethod\" IN ('PIX','CARD')");
                    table.CheckConstraint("CK_Payments_ProviderReference", "(\"ProviderTransactionId\" IS NULL OR (length(btrim(\"ProviderTransactionId\")) > 0 AND \"Provider\" IS NOT NULL AND length(btrim(\"Provider\")) > 0)) AND length(btrim(\"ExternalReference\")) > 0");
                    table.CheckConstraint("CK_Payments_Status", "\"Status\" IN ('PENDING','PROCESSING','PAID','EXPIRED','FAILED','CANCELLED','REFUNDED','CHARGEBACK')");
                    table.ForeignKey(
                        name: "FK_Payments_Checkouts_CheckoutSessionId_OrganizationId",
                        columns: x => new { x.CheckoutSessionId, x.OrganizationId },
                        principalTable: "Checkouts",
                        principalColumns: new[] { "Id", "OrganizationId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Payments_Customers_CustomerId_OrganizationId",
                        columns: x => new { x.CustomerId, x.OrganizationId },
                        principalTable: "Customers",
                        principalColumns: new[] { "Id", "OrganizationId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Payments_MerchantAccounts_MerchantAccountId_OrganizationId",
                        columns: x => new { x.MerchantAccountId, x.OrganizationId },
                        principalTable: "MerchantAccounts",
                        principalColumns: new[] { "Id", "OrganizationId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Payments_Offers_OfferId_OrganizationId",
                        columns: x => new { x.OfferId, x.OrganizationId },
                        principalTable: "Offers",
                        principalColumns: new[] { "Id", "OrganizationId" },
                        onDelete: ReferentialAction.Restrict);
                });

            // Database-only key: EF alternate keys would make checkout CustomerId immutable even before payment.
            migrationBuilder.AddUniqueConstraint("AK_Checkouts_PaymentSnapshot", "Checkouts", new[] { "Id", "OrganizationId", "CustomerId", "OfferId" });
            migrationBuilder.AddForeignKey("FK_Payments_CheckoutSnapshot", "Payments", new[] { "CheckoutSessionId", "OrganizationId", "CustomerId", "OfferId" }, "Checkouts", principalColumns: new[] { "Id", "OrganizationId", "CustomerId", "OfferId" }, onDelete: ReferentialAction.Restrict);
            migrationBuilder.Sql("""
                INSERT INTO "MerchantAccounts" ("Id", "OrganizationId", "Status", "CreatedAt", "UpdatedAt")
                SELECT gen_random_uuid(), "Id", 'PENDING', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP FROM "Organizations";
                """);
            migrationBuilder.CreateIndex(
                name: "IX_MerchantAccounts_OrganizationId",
                table: "MerchantAccounts",
                column: "OrganizationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_CheckoutSessionId_OrganizationId",
                table: "Payments",
                columns: new[] { "CheckoutSessionId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_CustomerId_OrganizationId",
                table: "Payments",
                columns: new[] { "CustomerId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_MerchantAccountId_ExternalReference",
                table: "Payments",
                columns: new[] { "MerchantAccountId", "ExternalReference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_MerchantAccountId_OrganizationId",
                table: "Payments",
                columns: new[] { "MerchantAccountId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_MerchantAccountId_Provider_ProviderTransactionId",
                table: "Payments",
                columns: new[] { "MerchantAccountId", "Provider", "ProviderTransactionId" },
                unique: true,
                filter: "\"ProviderTransactionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_OfferId_OrganizationId",
                table: "Payments",
                columns: new[] { "OfferId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_OrganizationId_CreatedAt",
                table: "Payments",
                columns: new[] { "OrganizationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_Status_ExpiresAt",
                table: "Payments",
                columns: new[] { "Status", "ExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "MerchantAccounts");

            migrationBuilder.DropUniqueConstraint("AK_Checkouts_PaymentSnapshot", "Checkouts");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Checkouts_Id_OrganizationId",
                table: "Checkouts");
        }
    }
}

