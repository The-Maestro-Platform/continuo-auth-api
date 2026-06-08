using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace authapi.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthInboxRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InboxRecords",
                schema: "aut",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(26)", maxLength: 26, nullable: false),
                    ConsumerKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    MessageId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboxRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlatformAgreements",
                schema: "aut",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(26)", maxLength: 26, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BodyMd = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Version = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    EffectiveFromUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedBy = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformAgreements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlatformIdentities",
                schema: "aut",
                columns: table => new
                {
                    RowKey = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CompanyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CompanyLegalName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    CompanyAddress = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CompanyEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CompanyKep = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CompanyPhone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CompanyWebsite = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    JurisdictionCity = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedBy = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformIdentities", x => x.RowKey);
                    table.CheckConstraint("CK_PlatformIdentities_SingleRow", "[RowKey] = 'current'");
                });

            migrationBuilder.InsertData(
                schema: "aut",
                table: "Permissions",
                columns: new[] { "Key", "Category", "Description", "DisplayName", "Icon", "Scope", "SortOrder" },
                values: new object[,]
                {
                    { "ops.billing.manage", null, "Manage subscription invoices, IBAN reconciliation and tenant payment provider settings (IBAN / Iyzico) in Ops UI.", "Manage Platform Billing", null, 0, 0 },
                    { "ops.catalog.manage", null, "Manage the modules / packages / discounts catalog, tenant entitlements and the provision-request approval queue in Ops UI.", "Manage Tenant Catalog", null, 0, 0 },
                    { "ops.dashboard.platform.view", null, "View the cross-tenant Platform Ops dashboard — fleet watchlist, MRR trend, module adoption, conversion funnel.", "View Platform Ops Dashboard", null, 0, 0 },
                    { "platform.agreements.manage", null, "Edit and version the platform-level legal agreements (KVKK Aydınlatma, Kullanım Koşulları, Pazarlama İzni) shown to customers at signup/login. continuo-ops-ui Agreements panel + auth-api admin CRUD endpoints.", "Manage Legal Agreements", null, 0, 0 },
                    { "platform.forex.manage", null, "Adjust platform-wide FX margin (`forex.margin.default.pips`) and view TCMB rate history + change audit. Required for the console-admin `/admin/forex` Margin Settings tab.", "Manage Forex Margin", null, 0, 0 },
                    { "platform.forex.refresh", null, "Trigger an on-demand TCMB FX feed refresh (POST /internal/refresh on exchange-rate-api). Used by Tempo workflow + ops debug; service-internal callers use M2M instead.", "Trigger Forex Refresh", null, 0, 0 },
                    { "platform.portal.access", null, "Open the dev-support-console / developer portal landing and pick a target environment.", "Access Developer Portal", null, 0, 0 },
                    { "platform.portal.env.dev", null, "Enter the Dev environment from the developer portal.", "Portal: Dev Environment", null, 0, 0 },
                    { "platform.portal.env.prod", null, "Enter the Production environment from the developer portal — production access (restricted).", "Portal: Production Environment", null, 0, 0 },
                    { "platform.portal.env.staging", null, "Enter the Staging environment from the developer portal.", "Portal: Staging Environment", null, 0, 0 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_InboxRecords_ConsumerKey_MessageId",
                schema: "aut",
                table: "InboxRecords",
                columns: new[] { "ConsumerKey", "MessageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformAgreements_Code",
                schema: "aut",
                table: "PlatformAgreements",
                column: "Code",
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformAgreements_Code_Version",
                schema: "aut",
                table: "PlatformAgreements",
                columns: new[] { "Code", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InboxRecords",
                schema: "aut");

            migrationBuilder.DropTable(
                name: "PlatformAgreements",
                schema: "aut");

            migrationBuilder.DropTable(
                name: "PlatformIdentities",
                schema: "aut");

            migrationBuilder.DeleteData(
                schema: "aut",
                table: "Permissions",
                keyColumn: "Key",
                keyValue: "ops.billing.manage");

            migrationBuilder.DeleteData(
                schema: "aut",
                table: "Permissions",
                keyColumn: "Key",
                keyValue: "ops.catalog.manage");

            migrationBuilder.DeleteData(
                schema: "aut",
                table: "Permissions",
                keyColumn: "Key",
                keyValue: "ops.dashboard.platform.view");

            migrationBuilder.DeleteData(
                schema: "aut",
                table: "Permissions",
                keyColumn: "Key",
                keyValue: "platform.agreements.manage");

            migrationBuilder.DeleteData(
                schema: "aut",
                table: "Permissions",
                keyColumn: "Key",
                keyValue: "platform.forex.manage");

            migrationBuilder.DeleteData(
                schema: "aut",
                table: "Permissions",
                keyColumn: "Key",
                keyValue: "platform.forex.refresh");

            migrationBuilder.DeleteData(
                schema: "aut",
                table: "Permissions",
                keyColumn: "Key",
                keyValue: "platform.portal.access");

            migrationBuilder.DeleteData(
                schema: "aut",
                table: "Permissions",
                keyColumn: "Key",
                keyValue: "platform.portal.env.dev");

            migrationBuilder.DeleteData(
                schema: "aut",
                table: "Permissions",
                keyColumn: "Key",
                keyValue: "platform.portal.env.prod");

            migrationBuilder.DeleteData(
                schema: "aut",
                table: "Permissions",
                keyColumn: "Key",
                keyValue: "platform.portal.env.staging");
        }
    }
}
