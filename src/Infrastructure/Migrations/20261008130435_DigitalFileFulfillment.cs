using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DigitalFileFulfillment : Migration
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

            migrationBuilder.AddColumn<Guid>(
                name: "DigitalAssetId",
                table: "FulfillmentExecutions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DigitalAssetId",
                table: "FulfillmentDefinitions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DigitalAssets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DigitalAssets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DigitalAssets_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FulfillmentExecutions_DigitalAssetId",
                table: "FulfillmentExecutions",
                column: "DigitalAssetId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FulfillmentExecutions_Type",
                table: "FulfillmentExecutions",
                sql: "\"Type\" IN ('EXTERNAL_LINK','DIGITAL_FILE')");

            migrationBuilder.CreateIndex(
                name: "IX_FulfillmentDefinitions_DigitalAssetId",
                table: "FulfillmentDefinitions",
                column: "DigitalAssetId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FulfillmentDefinitions_Type",
                table: "FulfillmentDefinitions",
                sql: "\"Type\" IN ('EXTERNAL_LINK','DIGITAL_FILE')");

            migrationBuilder.CreateIndex(
                name: "IX_DigitalAssets_OrganizationId_CreatedAt",
                table: "DigitalAssets",
                columns: new[] { "OrganizationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DigitalAssets_StorageKey",
                table: "DigitalAssets",
                column: "StorageKey",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_FulfillmentDefinitions_DigitalAssets_DigitalAssetId",
                table: "FulfillmentDefinitions",
                column: "DigitalAssetId",
                principalTable: "DigitalAssets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FulfillmentExecutions_DigitalAssets_DigitalAssetId",
                table: "FulfillmentExecutions",
                column: "DigitalAssetId",
                principalTable: "DigitalAssets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FulfillmentDefinitions_DigitalAssets_DigitalAssetId",
                table: "FulfillmentDefinitions");

            migrationBuilder.DropForeignKey(
                name: "FK_FulfillmentExecutions_DigitalAssets_DigitalAssetId",
                table: "FulfillmentExecutions");

            migrationBuilder.DropTable(
                name: "DigitalAssets");

            migrationBuilder.DropIndex(
                name: "IX_FulfillmentExecutions_DigitalAssetId",
                table: "FulfillmentExecutions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FulfillmentExecutions_Type",
                table: "FulfillmentExecutions");

            migrationBuilder.DropIndex(
                name: "IX_FulfillmentDefinitions_DigitalAssetId",
                table: "FulfillmentDefinitions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FulfillmentDefinitions_Type",
                table: "FulfillmentDefinitions");

            migrationBuilder.DropColumn(
                name: "DigitalAssetId",
                table: "FulfillmentExecutions");

            migrationBuilder.DropColumn(
                name: "DigitalAssetId",
                table: "FulfillmentDefinitions");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FulfillmentExecutions_Type",
                table: "FulfillmentExecutions",
                sql: "\"Type\" = 'EXTERNAL_LINK'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FulfillmentDefinitions_Type",
                table: "FulfillmentDefinitions",
                sql: "\"Type\" = 'EXTERNAL_LINK'");
        }
    }
}
