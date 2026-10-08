using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EntitlementsAndExternalLinkFulfillment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Entitlements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OfferId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Entitlements", x => x.Id);
                    table.UniqueConstraint("AK_Entitlements_Id_OrganizationId", x => new { x.Id, x.OrganizationId });
                    table.CheckConstraint("CK_Entitlements_Status", "\"Status\" IN ('PENDING','ACTIVE','EXPIRED','REVOKED','FAILED')");
                    table.CheckConstraint("CK_Entitlements_Type", "\"Type\" = 'PURCHASE'");
                    table.ForeignKey(
                        name: "FK_Entitlements_Customers_CustomerId_OrganizationId",
                        columns: x => new { x.CustomerId, x.OrganizationId },
                        principalTable: "Customers",
                        principalColumns: new[] { "Id", "OrganizationId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Entitlements_Offers_OfferId_OrganizationId",
                        columns: x => new { x.OfferId, x.OrganizationId },
                        principalTable: "Offers",
                        principalColumns: new[] { "Id", "OrganizationId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Entitlements_Payments_PaymentId_OrganizationId",
                        columns: x => new { x.PaymentId, x.OrganizationId },
                        principalTable: "Payments",
                        principalColumns: new[] { "Id", "OrganizationId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FulfillmentDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OfferId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ExternalUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FulfillmentDefinitions", x => x.Id);
                    table.UniqueConstraint("AK_FulfillmentDefinitions_Id_OrganizationId", x => new { x.Id, x.OrganizationId });
                    table.CheckConstraint("CK_FulfillmentDefinitions_Status", "\"Status\" IN ('ACTIVE','INACTIVE')");
                    table.CheckConstraint("CK_FulfillmentDefinitions_Type", "\"Type\" = 'EXTERNAL_LINK'");
                    table.ForeignKey(
                        name: "FK_FulfillmentDefinitions_Offers_OfferId_OrganizationId",
                        columns: x => new { x.OfferId, x.OrganizationId },
                        principalTable: "Offers",
                        principalColumns: new[] { "Id", "OrganizationId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FulfillmentExecutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntitlementId = table.Column<Guid>(type: "uuid", nullable: false),
                    FulfillmentDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ExternalUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FulfillmentExecutions", x => x.Id);
                    table.CheckConstraint("CK_FulfillmentExecutions_Status", "\"Status\" IN ('PENDING','PROCESSING','COMPLETED','FAILED')");
                    table.CheckConstraint("CK_FulfillmentExecutions_Type", "\"Type\" = 'EXTERNAL_LINK'");
                    table.ForeignKey(
                        name: "FK_FulfillmentExecutions_Entitlements_EntitlementId_Organizati~",
                        columns: x => new { x.EntitlementId, x.OrganizationId },
                        principalTable: "Entitlements",
                        principalColumns: new[] { "Id", "OrganizationId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FulfillmentExecutions_FulfillmentDefinitions_FulfillmentDef~",
                        columns: x => new { x.FulfillmentDefinitionId, x.OrganizationId },
                        principalTable: "FulfillmentDefinitions",
                        principalColumns: new[] { "Id", "OrganizationId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Entitlements_CustomerId_OrganizationId",
                table: "Entitlements",
                columns: new[] { "CustomerId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_Entitlements_OfferId_OrganizationId",
                table: "Entitlements",
                columns: new[] { "OfferId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_Entitlements_OrganizationId_CustomerId_CreatedAt",
                table: "Entitlements",
                columns: new[] { "OrganizationId", "CustomerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Entitlements_PaymentId",
                table: "Entitlements",
                column: "PaymentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Entitlements_PaymentId_OrganizationId",
                table: "Entitlements",
                columns: new[] { "PaymentId", "OrganizationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FulfillmentDefinitions_OfferId_OrganizationId",
                table: "FulfillmentDefinitions",
                columns: new[] { "OfferId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_FulfillmentDefinitions_OrganizationId_OfferId_Type",
                table: "FulfillmentDefinitions",
                columns: new[] { "OrganizationId", "OfferId", "Type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FulfillmentExecutions_EntitlementId_FulfillmentDefinitionId",
                table: "FulfillmentExecutions",
                columns: new[] { "EntitlementId", "FulfillmentDefinitionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FulfillmentExecutions_EntitlementId_OrganizationId",
                table: "FulfillmentExecutions",
                columns: new[] { "EntitlementId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_FulfillmentExecutions_FulfillmentDefinitionId_OrganizationId",
                table: "FulfillmentExecutions",
                columns: new[] { "FulfillmentDefinitionId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_FulfillmentExecutions_OrganizationId_CreatedAt",
                table: "FulfillmentExecutions",
                columns: new[] { "OrganizationId", "CreatedAt" });

            migrationBuilder.Sql("""
                INSERT INTO "Entitlements" ("Id", "OrganizationId", "CustomerId", "OfferId", "PaymentId", "Type", "Status", "StartsAt", "ExpiresAt", "RevokedAt", "CreatedAt")
                SELECT gen_random_uuid(), p."OrganizationId", p."CustomerId", p."OfferId", p."Id", 'PURCHASE', 'ACTIVE', COALESCE(p."PaidAt", p."CreatedAt"), NULL, NULL, NOW()
                FROM "Payments" p
                WHERE p."Status" = 'PAID';

                CREATE FUNCTION zyven_reject_fulfillment_execution_mutation() RETURNS trigger AS $$
                BEGIN
                    RAISE EXCEPTION 'completed fulfillment executions are immutable' USING ERRCODE = '55000';
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER "TR_FulfillmentExecutions_Immutable"
                BEFORE UPDATE OR DELETE ON "FulfillmentExecutions"
                FOR EACH ROW EXECUTE FUNCTION zyven_reject_fulfillment_execution_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS zyven_reject_fulfillment_execution_mutation() CASCADE;");

            migrationBuilder.DropTable(
                name: "FulfillmentExecutions");

            migrationBuilder.DropTable(
                name: "Entitlements");

            migrationBuilder.DropTable(
                name: "FulfillmentDefinitions");
        }
    }
}
