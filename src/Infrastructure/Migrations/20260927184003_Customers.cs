using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Customers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                table: "Checkouts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    Phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    NormalizedPhone = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    Document = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                    table.UniqueConstraint("AK_Customers_Id_OrganizationId", x => new { x.Id, x.OrganizationId });
                    table.ForeignKey(
                        name: "FK_Customers_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Oldest checkout wins deterministically; later snapshots remain untouched.
            // Reusing its UUID is safe because Customers is a new table with its own key space.
            migrationBuilder.Sql("""
                INSERT INTO "Customers" ("Id", "OrganizationId", "Name", "Email", "NormalizedEmail", "Phone", "NormalizedPhone", "Document", "CreatedAt", "UpdatedAt")
                SELECT DISTINCT ON ("OrganizationId", translate(btrim("Email", E' \t\r\n'), 'abcdefghijklmnopqrstuvwxyz', 'ABCDEFGHIJKLMNOPQRSTUVWXYZ'))
                    "Id", "OrganizationId", "Name", btrim("Email", E' \t\r\n'),
                    translate(btrim("Email", E' \t\r\n'), 'abcdefghijklmnopqrstuvwxyz', 'ABCDEFGHIJKLMNOPQRSTUVWXYZ'),
                    "Phone",
                    CASE WHEN regexp_replace(btrim("Phone"), '[ ()-]', '', 'g') ~ '^\+[1-9][0-9]{6,14}$'
                         THEN regexp_replace(btrim("Phone"), '[ ()-]', '', 'g') ELSE NULL END,
                    "Document", "CreatedAt", "CreatedAt"
                FROM "Checkouts"
                ORDER BY "OrganizationId", translate(btrim("Email", E' \t\r\n'), 'abcdefghijklmnopqrstuvwxyz', 'ABCDEFGHIJKLMNOPQRSTUVWXYZ'), "CreatedAt", "Id";
                UPDATE "Checkouts" AS checkout SET "CustomerId" = customer."Id"
                FROM "Customers" AS customer
                WHERE checkout."OrganizationId" = customer."OrganizationId"
                  AND translate(btrim(checkout."Email", E' \t\r\n'), 'abcdefghijklmnopqrstuvwxyz', 'ABCDEFGHIJKLMNOPQRSTUVWXYZ') = customer."NormalizedEmail";
                """);
            migrationBuilder.AlterColumn<Guid>(name: "CustomerId", table: "Checkouts", type: "uuid", nullable: false, oldClrType: typeof(Guid), oldType: "uuid", oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Checkouts_CustomerId_OrganizationId",
                table: "Checkouts",
                columns: new[] { "CustomerId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_OrganizationId_CreatedAt",
                table: "Customers",
                columns: new[] { "OrganizationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_OrganizationId_NormalizedEmail",
                table: "Customers",
                columns: new[] { "OrganizationId", "NormalizedEmail" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_OrganizationId_NormalizedPhone",
                table: "Customers",
                columns: new[] { "OrganizationId", "NormalizedPhone" });

            migrationBuilder.AddForeignKey(
                name: "FK_Checkouts_Customers_CustomerId_OrganizationId",
                table: "Checkouts",
                columns: new[] { "CustomerId", "OrganizationId" },
                principalTable: "Customers",
                principalColumns: new[] { "Id", "OrganizationId" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Checkouts_Customers_CustomerId_OrganizationId",
                table: "Checkouts");

            migrationBuilder.DropTable(
                name: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Checkouts_CustomerId_OrganizationId",
                table: "Checkouts");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "Checkouts");
        }
    }
}
