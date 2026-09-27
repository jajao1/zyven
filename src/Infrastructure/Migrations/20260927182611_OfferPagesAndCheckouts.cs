using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OfferPagesAndCheckouts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_Offers_Id_OrganizationId",
                table: "Offers",
                columns: new[] { "Id", "OrganizationId" });

            migrationBuilder.CreateTable(
                name: "Checkouts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OfferId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccessHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    Phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Document = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    FieldsJson = table.Column<string>(type: "jsonb", nullable: false),
                    FormJson = table.Column<string>(type: "jsonb", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Checkouts", x => x.Id);
                    table.CheckConstraint("CK_Checkouts_Price", "\"Price\" > 0");
                    table.CheckConstraint("CK_Checkouts_Status", "\"Status\" IN ('CREATED','PENDING_PAYMENT','COMPLETED','EXPIRED','ABANDONED')");
                    table.ForeignKey(
                        name: "FK_Checkouts_Offers_OfferId_OrganizationId",
                        columns: x => new { x.OfferId, x.OrganizationId },
                        principalTable: "Offers",
                        principalColumns: new[] { "Id", "OrganizationId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OfferPages",
                columns: table => new
                {
                    OfferId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentJson = table.Column<string>(type: "jsonb", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfferPages", x => x.OfferId);
                    table.ForeignKey(
                        name: "FK_OfferPages_Offers_OfferId_OrganizationId",
                        columns: x => new { x.OfferId, x.OrganizationId },
                        principalTable: "Offers",
                        principalColumns: new[] { "Id", "OrganizationId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Checkouts_OfferId_OrganizationId",
                table: "Checkouts",
                columns: new[] { "OfferId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_Checkouts_OrganizationId_CreatedAt",
                table: "Checkouts",
                columns: new[] { "OrganizationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Checkouts_Status_ExpiresAt",
                table: "Checkouts",
                columns: new[] { "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OfferPages_OfferId_OrganizationId",
                table: "OfferPages",
                columns: new[] { "OfferId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_OfferPages_OrganizationId",
                table: "OfferPages",
                column: "OrganizationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Checkouts");

            migrationBuilder.DropTable(
                name: "OfferPages");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Offers_Id_OrganizationId",
                table: "Offers");
        }
    }
}
