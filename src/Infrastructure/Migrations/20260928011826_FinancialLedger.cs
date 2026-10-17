using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FinancialLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_Payments_Id_OrganizationId",
                table: "Payments",
                columns: new[] { "Id", "OrganizationId" });

            migrationBuilder.CreateTable(
                name: "LedgerAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NormalSide = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LedgerAccounts", x => x.Id);
                    table.UniqueConstraint("AK_LedgerAccounts_Id_OrganizationId", x => new { x.Id, x.OrganizationId });
                    table.CheckConstraint("CK_LedgerAccounts_Currency", "\"Currency\" ~ '^[A-Z]{3}$'");
                    table.CheckConstraint("CK_LedgerAccounts_NormalSide", "\"NormalSide\" IN ('DEBIT','CREDIT')");
                    table.CheckConstraint("CK_LedgerAccounts_Type", "\"Type\" IN ('ASSET','LIABILITY','REVENUE','EXPENSE','EQUITY')");
                    table.ForeignKey(
                        name: "FK_LedgerAccounts_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LedgerTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LedgerTransactions", x => x.Id);
                    table.UniqueConstraint("AK_LedgerTransactions_Id_OrganizationId", x => new { x.Id, x.OrganizationId });
                    table.ForeignKey(
                        name: "FK_LedgerTransactions_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LedgerTransactions_Payments_PaymentId_OrganizationId",
                        columns: x => new { x.PaymentId, x.OrganizationId },
                        principalTable: "Payments",
                        principalColumns: new[] { "Id", "OrganizationId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LedgerEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    LedgerTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    LedgerAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Debit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Credit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LedgerEntries", x => x.Id);
                    table.CheckConstraint("CK_LedgerEntries_Currency", "\"Currency\" ~ '^[A-Z]{3}$'");
                    table.CheckConstraint("CK_LedgerEntries_OneSide", "(\"Debit\" > 0 AND \"Credit\" = 0) OR (\"Credit\" > 0 AND \"Debit\" = 0)");
                    table.ForeignKey(
                        name: "FK_LedgerEntries_LedgerAccounts_LedgerAccountId_OrganizationId",
                        columns: x => new { x.LedgerAccountId, x.OrganizationId },
                        principalTable: "LedgerAccounts",
                        principalColumns: new[] { "Id", "OrganizationId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LedgerEntries_LedgerTransactions_LedgerTransactionId_Organi~",
                        columns: x => new { x.LedgerTransactionId, x.OrganizationId },
                        principalTable: "LedgerTransactions",
                        principalColumns: new[] { "Id", "OrganizationId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LedgerAccounts_OrganizationId_Code",
                table: "LedgerAccounts",
                columns: new[] { "OrganizationId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_LedgerAccountId",
                table: "LedgerEntries",
                column: "LedgerAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_LedgerAccountId_OrganizationId",
                table: "LedgerEntries",
                columns: new[] { "LedgerAccountId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_LedgerTransactionId_OrganizationId",
                table: "LedgerEntries",
                columns: new[] { "LedgerTransactionId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_OrganizationId_CreatedAt_Id",
                table: "LedgerEntries",
                columns: new[] { "OrganizationId", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_LedgerTransactions_OrganizationId_OccurredAt_Id",
                table: "LedgerTransactions",
                columns: new[] { "OrganizationId", "OccurredAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_LedgerTransactions_PaymentId",
                table: "LedgerTransactions",
                column: "PaymentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LedgerTransactions_PaymentId_OrganizationId",
                table: "LedgerTransactions",
                columns: new[] { "PaymentId", "OrganizationId" },
                unique: true);

            migrationBuilder.Sql("""
                INSERT INTO "LedgerAccounts" ("Id", "OrganizationId", "Code", "Name", "Type", "NormalSide", "Currency", "CreatedAt")
                SELECT gen_random_uuid(), o."Id", v."Code", v."Name", v."Type", v."NormalSide", 'BRL', NOW()
                FROM "Organizations" o
                CROSS JOIN (VALUES
                    ('CELCOIN_CLEARING', 'Celcoin clearing', 'ASSET', 'DEBIT'),
                    ('MERCHANT_AVAILABLE', 'Merchant available balance', 'LIABILITY', 'CREDIT'),
                    ('PLATFORM_FEE_REVENUE', 'Platform fee revenue', 'REVENUE', 'CREDIT'),
                    ('PROVIDER_FEE_PAYABLE', 'Provider fee payable', 'LIABILITY', 'CREDIT')
                ) AS v("Code", "Name", "Type", "NormalSide");

                INSERT INTO "LedgerTransactions" ("Id", "OrganizationId", "PaymentId", "Type", "Reference", "Currency", "OccurredAt", "CreatedAt")
                SELECT gen_random_uuid(), p."OrganizationId", p."Id", 'PAYMENT_CAPTURED', p."ExternalReference", p."Currency", COALESCE(p."PaidAt", p."CreatedAt"), NOW()
                FROM "Payments" p
                WHERE p."Status" = 'PAID';

                INSERT INTO "LedgerEntries" ("Id", "OrganizationId", "LedgerTransactionId", "LedgerAccountId", "Debit", "Credit", "Currency", "CreatedAt")
                SELECT gen_random_uuid(), t."OrganizationId", t."Id", a."Id", values."Debit", values."Credit", t."Currency", NOW()
                FROM "LedgerTransactions" t
                JOIN "Payments" p ON p."Id" = t."PaymentId" AND p."OrganizationId" = t."OrganizationId"
                CROSS JOIN LATERAL (VALUES
                    ('CELCOIN_CLEARING', p."GrossAmount", 0::numeric),
                    ('MERCHANT_AVAILABLE', 0::numeric, p."NetAmount"),
                    ('PLATFORM_FEE_REVENUE', 0::numeric, p."PlatformFee"),
                    ('PROVIDER_FEE_PAYABLE', 0::numeric, p."ProviderFee")
                ) AS values("Code", "Debit", "Credit")
                JOIN "LedgerAccounts" a ON a."OrganizationId" = t."OrganizationId" AND a."Code" = values."Code"
                WHERE values."Debit" > 0 OR values."Credit" > 0;

                CREATE FUNCTION zyven_reject_ledger_mutation() RETURNS trigger AS $$
                BEGIN
                    RAISE EXCEPTION 'financial ledger records are immutable' USING ERRCODE = '55000';
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER "TR_LedgerEntries_Immutable"
                BEFORE UPDATE OR DELETE ON "LedgerEntries"
                FOR EACH ROW EXECUTE FUNCTION zyven_reject_ledger_mutation();

                CREATE TRIGGER "TR_LedgerTransactions_Immutable"
                BEFORE UPDATE OR DELETE ON "LedgerTransactions"
                FOR EACH ROW EXECUTE FUNCTION zyven_reject_ledger_mutation();

                CREATE FUNCTION zyven_check_ledger_balance() RETURNS trigger AS $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM "LedgerEntries"
                        WHERE "LedgerTransactionId" = NEW."LedgerTransactionId"
                        GROUP BY "LedgerTransactionId"
                        HAVING COUNT(*) < 2 OR SUM("Debit") <> SUM("Credit")
                    ) THEN
                        RAISE EXCEPTION 'ledger transaction must balance' USING ERRCODE = '23514';
                    END IF;
                    RETURN NULL;
                END;
                $$ LANGUAGE plpgsql;

                CREATE CONSTRAINT TRIGGER "TR_LedgerEntries_Balanced"
                AFTER INSERT ON "LedgerEntries"
                DEFERRABLE INITIALLY DEFERRED
                FOR EACH ROW EXECUTE FUNCTION zyven_check_ledger_balance();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP FUNCTION IF EXISTS zyven_check_ledger_balance() CASCADE;
                DROP FUNCTION IF EXISTS zyven_reject_ledger_mutation() CASCADE;
                """);

            migrationBuilder.DropTable(
                name: "LedgerEntries");

            migrationBuilder.DropTable(
                name: "LedgerAccounts");

            migrationBuilder.DropTable(
                name: "LedgerTransactions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Payments_Id_OrganizationId",
                table: "Payments");
        }
    }
}
