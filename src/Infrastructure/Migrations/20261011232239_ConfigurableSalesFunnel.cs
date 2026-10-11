using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConfigurableSalesFunnel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Automations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OfferId = table.Column<Guid>(type: "uuid", nullable: false),
                    PublishedVersion = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Automations", x => x.Id);
                    table.UniqueConstraint("AK_Automations_Id_OrganizationId", x => new { x.Id, x.OrganizationId });
                    table.ForeignKey(
                        name: "FK_Automations_Offers_OfferId_OrganizationId",
                        columns: x => new { x.OfferId, x.OrganizationId },
                        principalTable: "Offers",
                        principalColumns: new[] { "Id", "OrganizationId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AutomationExecutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AutomationId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntitlementId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CurrentPosition = table.Column<int>(type: "integer", nullable: false),
                    TelegramUserId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NextRunAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutomationExecutions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AutomationExecutions_Automations_AutomationId_OrganizationId",
                        columns: x => new { x.AutomationId, x.OrganizationId },
                        principalTable: "Automations",
                        principalColumns: new[] { "Id", "OrganizationId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AutomationExecutions_Entitlements_EntitlementId_Organizatio~",
                        columns: x => new { x.EntitlementId, x.OrganizationId },
                        principalTable: "Entitlements",
                        principalColumns: new[] { "Id", "OrganizationId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AutomationSteps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AutomationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ConfigurationJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutomationSteps", x => x.Id);
                    table.CheckConstraint("CK_AutomationSteps_Position", "\"Position\" >= 0");
                    table.CheckConstraint("CK_AutomationSteps_Version", "\"Version\" >= 0");
                    table.ForeignKey(
                        name: "FK_AutomationSteps_Automations_AutomationId_OrganizationId",
                        columns: x => new { x.AutomationId, x.OrganizationId },
                        principalTable: "Automations",
                        principalColumns: new[] { "Id", "OrganizationId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AutomationExecutions_AutomationId_OrganizationId",
                table: "AutomationExecutions",
                columns: new[] { "AutomationId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_AutomationExecutions_EntitlementId_AutomationId_Version",
                table: "AutomationExecutions",
                columns: new[] { "EntitlementId", "AutomationId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AutomationExecutions_EntitlementId_OrganizationId",
                table: "AutomationExecutions",
                columns: new[] { "EntitlementId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_AutomationExecutions_Status_NextRunAt",
                table: "AutomationExecutions",
                columns: new[] { "Status", "NextRunAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Automations_OfferId_OrganizationId",
                table: "Automations",
                columns: new[] { "OfferId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_Automations_OrganizationId_OfferId",
                table: "Automations",
                columns: new[] { "OrganizationId", "OfferId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AutomationSteps_AutomationId_OrganizationId",
                table: "AutomationSteps",
                columns: new[] { "AutomationId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_AutomationSteps_AutomationId_Version_Position",
                table: "AutomationSteps",
                columns: new[] { "AutomationId", "Version", "Position" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AutomationExecutions");

            migrationBuilder.DropTable(
                name: "AutomationSteps");

            migrationBuilder.DropTable(
                name: "Automations");
        }
    }
}
