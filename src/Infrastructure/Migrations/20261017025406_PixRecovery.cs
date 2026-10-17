using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PixRecovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BotCheckoutBindings",
                columns: table => new
                {
                    CheckoutId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TelegramIntegrationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TelegramUserId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BotCheckoutBindings", x => x.CheckoutId);
                    table.ForeignKey(
                        name: "FK_BotCheckoutBindings_Checkouts_CheckoutId",
                        column: x => x.CheckoutId,
                        principalTable: "Checkouts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BotCheckoutBindings_TelegramIntegrations_TelegramIntegratio~",
                        column: x => x.TelegramIntegrationId,
                        principalTable: "TelegramIntegrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BotCheckoutReferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OfferId = table.Column<Guid>(type: "uuid", nullable: false),
                    TelegramIntegrationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TelegramUserId = table.Column<long>(type: "bigint", nullable: false),
                    Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConsumedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CheckoutId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BotCheckoutReferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BotCheckoutReferences_Offers_OfferId_OrganizationId",
                        columns: x => new { x.OfferId, x.OrganizationId },
                        principalTable: "Offers",
                        principalColumns: new[] { "Id", "OrganizationId" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BotCheckoutReferences_TelegramIntegrations_TelegramIntegrat~",
                        column: x => x.TelegramIntegrationId,
                        principalTable: "TelegramIntegrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PixRecoveryConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OfferId = table.Column<Guid>(type: "uuid", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    PublishedVersion = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PixRecoveryConfigurations", x => x.Id);
                    table.UniqueConstraint("AK_PixRecoveryConfigurations_Id_OrganizationId", x => new { x.Id, x.OrganizationId });
                    table.ForeignKey(
                        name: "FK_PixRecoveryConfigurations_Offers_OfferId_OrganizationId",
                        columns: x => new { x.OfferId, x.OrganizationId },
                        principalTable: "Offers",
                        principalColumns: new[] { "Id", "OrganizationId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PixRecoveryExecutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConfigurationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CurrentPosition = table.Column<int>(type: "integer", nullable: false),
                    Channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    NextRunAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PixRecoveryExecutions", x => x.Id);
                    table.UniqueConstraint("AK_PixRecoveryExecutions_Id_OrganizationId", x => new { x.Id, x.OrganizationId });
                    table.ForeignKey(
                        name: "FK_PixRecoveryExecutions_Payments_PaymentId_OrganizationId",
                        columns: x => new { x.PaymentId, x.OrganizationId },
                        principalTable: "Payments",
                        principalColumns: new[] { "Id", "OrganizationId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PixRecoveryExecutions_PixRecoveryConfigurations_Configurati~",
                        columns: x => new { x.ConfigurationId, x.OrganizationId },
                        principalTable: "PixRecoveryConfigurations",
                        principalColumns: new[] { "Id", "OrganizationId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PixRecoveryMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConfigurationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    DelayMinutes = table.Column<int>(type: "integer", nullable: false),
                    EmailText = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    TelegramText = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PixRecoveryMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PixRecoveryMessages_PixRecoveryConfigurations_Configuration~",
                        columns: x => new { x.ConfigurationId, x.OrganizationId },
                        principalTable: "PixRecoveryConfigurations",
                        principalColumns: new[] { "Id", "OrganizationId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PixRecoveryAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    TryCount = table.Column<int>(type: "integer", nullable: false),
                    Channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PixRecoveryAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PixRecoveryAttempts_PixRecoveryExecutions_ExecutionId_Organ~",
                        columns: x => new { x.ExecutionId, x.OrganizationId },
                        principalTable: "PixRecoveryExecutions",
                        principalColumns: new[] { "Id", "OrganizationId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BotCheckoutBindings_TelegramIntegrationId",
                table: "BotCheckoutBindings",
                column: "TelegramIntegrationId");

            migrationBuilder.CreateIndex(
                name: "IX_BotCheckoutReferences_ExpiresAt",
                table: "BotCheckoutReferences",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_BotCheckoutReferences_Hash",
                table: "BotCheckoutReferences",
                column: "Hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BotCheckoutReferences_OfferId_OrganizationId",
                table: "BotCheckoutReferences",
                columns: new[] { "OfferId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_BotCheckoutReferences_TelegramIntegrationId",
                table: "BotCheckoutReferences",
                column: "TelegramIntegrationId");

            migrationBuilder.CreateIndex(
                name: "IX_PixRecoveryAttempts_ExecutionId_OrganizationId",
                table: "PixRecoveryAttempts",
                columns: new[] { "ExecutionId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_PixRecoveryAttempts_ExecutionId_Position",
                table: "PixRecoveryAttempts",
                columns: new[] { "ExecutionId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PixRecoveryConfigurations_OfferId_OrganizationId",
                table: "PixRecoveryConfigurations",
                columns: new[] { "OfferId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_PixRecoveryConfigurations_OrganizationId_OfferId",
                table: "PixRecoveryConfigurations",
                columns: new[] { "OrganizationId", "OfferId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PixRecoveryExecutions_ConfigurationId_OrganizationId",
                table: "PixRecoveryExecutions",
                columns: new[] { "ConfigurationId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_PixRecoveryExecutions_PaymentId",
                table: "PixRecoveryExecutions",
                column: "PaymentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PixRecoveryExecutions_PaymentId_OrganizationId",
                table: "PixRecoveryExecutions",
                columns: new[] { "PaymentId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_PixRecoveryExecutions_Status_NextRunAt",
                table: "PixRecoveryExecutions",
                columns: new[] { "Status", "NextRunAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PixRecoveryMessages_ConfigurationId_OrganizationId",
                table: "PixRecoveryMessages",
                columns: new[] { "ConfigurationId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_PixRecoveryMessages_ConfigurationId_Version_Position",
                table: "PixRecoveryMessages",
                columns: new[] { "ConfigurationId", "Version", "Position" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BotCheckoutBindings");

            migrationBuilder.DropTable(
                name: "BotCheckoutReferences");

            migrationBuilder.DropTable(
                name: "PixRecoveryAttempts");

            migrationBuilder.DropTable(
                name: "PixRecoveryMessages");

            migrationBuilder.DropTable(
                name: "PixRecoveryExecutions");

            migrationBuilder.DropTable(
                name: "PixRecoveryConfigurations");
        }
    }
}
