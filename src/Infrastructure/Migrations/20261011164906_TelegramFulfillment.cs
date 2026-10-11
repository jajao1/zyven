using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TelegramFulfillment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_FulfillmentExecutions_Type",
                table: "FulfillmentExecutions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FulfillmentDefinitions_Type",
                table: "FulfillmentDefinitions");

            migrationBuilder.AddColumn<string>(
                name: "TelegramChatId",
                table: "FulfillmentExecutions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "TelegramIntegrationId",
                table: "FulfillmentExecutions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TelegramChatId",
                table: "FulfillmentDefinitions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "TelegramIntegrationId",
                table: "FulfillmentDefinitions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TelegramAccessGrants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    FulfillmentExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TelegramUserId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    GrantedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelegramAccessGrants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TelegramAccessGrants_FulfillmentExecutions_FulfillmentExecu~",
                        column: x => x.FulfillmentExecutionId,
                        principalTable: "FulfillmentExecutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TelegramIntegrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BotId = table.Column<long>(type: "bigint", nullable: false),
                    BotName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BotUsername = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TokenCiphertext = table.Column<string>(type: "text", nullable: false),
                    TokenNonce = table.Column<string>(type: "text", nullable: false),
                    TokenTag = table.Column<string>(type: "text", nullable: false),
                    TokenFingerprint = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    WebhookSecretHash = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelegramIntegrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TelegramIntegrations_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TelegramLinkTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    FulfillmentExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConsumedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelegramLinkTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TelegramLinkTokens_FulfillmentExecutions_FulfillmentExecuti~",
                        column: x => x.FulfillmentExecutionId,
                        principalTable: "FulfillmentExecutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FulfillmentExecutions_TelegramIntegrationId",
                table: "FulfillmentExecutions",
                column: "TelegramIntegrationId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FulfillmentExecutions_Type",
                table: "FulfillmentExecutions",
                sql: "\"Type\" IN ('EXTERNAL_LINK','DIGITAL_FILE','TELEGRAM_ACCESS')");

            migrationBuilder.CreateIndex(
                name: "IX_FulfillmentDefinitions_TelegramIntegrationId",
                table: "FulfillmentDefinitions",
                column: "TelegramIntegrationId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FulfillmentDefinitions_Type",
                table: "FulfillmentDefinitions",
                sql: "\"Type\" IN ('EXTERNAL_LINK','DIGITAL_FILE','TELEGRAM_ACCESS')");

            migrationBuilder.CreateIndex(
                name: "IX_TelegramAccessGrants_FulfillmentExecutionId",
                table: "TelegramAccessGrants",
                column: "FulfillmentExecutionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TelegramAccessGrants_OrganizationId_TelegramUserId",
                table: "TelegramAccessGrants",
                columns: new[] { "OrganizationId", "TelegramUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_TelegramIntegrations_OrganizationId",
                table: "TelegramIntegrations",
                column: "OrganizationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TelegramLinkTokens_ExpiresAt",
                table: "TelegramLinkTokens",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_TelegramLinkTokens_FulfillmentExecutionId",
                table: "TelegramLinkTokens",
                column: "FulfillmentExecutionId");

            migrationBuilder.CreateIndex(
                name: "IX_TelegramLinkTokens_Hash",
                table: "TelegramLinkTokens",
                column: "Hash",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_FulfillmentDefinitions_TelegramIntegrations_TelegramIntegra~",
                table: "FulfillmentDefinitions",
                column: "TelegramIntegrationId",
                principalTable: "TelegramIntegrations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FulfillmentExecutions_TelegramIntegrations_TelegramIntegrat~",
                table: "FulfillmentExecutions",
                column: "TelegramIntegrationId",
                principalTable: "TelegramIntegrations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FulfillmentDefinitions_TelegramIntegrations_TelegramIntegra~",
                table: "FulfillmentDefinitions");

            migrationBuilder.DropForeignKey(
                name: "FK_FulfillmentExecutions_TelegramIntegrations_TelegramIntegrat~",
                table: "FulfillmentExecutions");

            migrationBuilder.DropTable(
                name: "TelegramAccessGrants");

            migrationBuilder.DropTable(
                name: "TelegramIntegrations");

            migrationBuilder.DropTable(
                name: "TelegramLinkTokens");

            migrationBuilder.DropIndex(
                name: "IX_FulfillmentExecutions_TelegramIntegrationId",
                table: "FulfillmentExecutions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FulfillmentExecutions_Type",
                table: "FulfillmentExecutions");

            migrationBuilder.DropIndex(
                name: "IX_FulfillmentDefinitions_TelegramIntegrationId",
                table: "FulfillmentDefinitions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FulfillmentDefinitions_Type",
                table: "FulfillmentDefinitions");

            migrationBuilder.DropColumn(
                name: "TelegramChatId",
                table: "FulfillmentExecutions");

            migrationBuilder.DropColumn(
                name: "TelegramIntegrationId",
                table: "FulfillmentExecutions");

            migrationBuilder.DropColumn(
                name: "TelegramChatId",
                table: "FulfillmentDefinitions");

            migrationBuilder.DropColumn(
                name: "TelegramIntegrationId",
                table: "FulfillmentDefinitions");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FulfillmentExecutions_Type",
                table: "FulfillmentExecutions",
                sql: "\"Type\" IN ('EXTERNAL_LINK','DIGITAL_FILE')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FulfillmentDefinitions_Type",
                table: "FulfillmentDefinitions",
                sql: "\"Type\" IN ('EXTERNAL_LINK','DIGITAL_FILE')");
        }
    }
}
