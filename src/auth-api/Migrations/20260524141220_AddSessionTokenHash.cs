using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace authapi.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionTokenHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SessionTokenHash",
                schema: "aut",
                table: "UserSessions",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.InsertData(
                schema: "aut",
                table: "Permissions",
                columns: new[] { "Key", "Category", "Description", "DisplayName", "Icon", "Scope", "SortOrder" },
                values: new object[,]
                {
                    { "customer.maestro.billing.manage", null, "Authenticated qrmenu customer can view and top up their personal Maestro token wallet.", "Customer Maestro Wallet", null, 1, 0 },
                    { "customer.maestro.use", null, "Authenticated qrmenu customer can open the Maestro chat assistant for menu / allergen / order help.", "Customer Maestro Chat", null, 1, 0 },
                    { "ops.maestro.tenants.manage", null, "Configure per-tenant Maestro AI policies: usage capacity, daily/monthly USD cap, token cap, allowed providers, role-scoped overrides and personality presets.", "Manage Maestro Tenants", null, 0, 0 },
                    { "platform.maestro.unlimited", null, "Bypass the token-wallet check entirely — internal personnel marker (dev / analyst / infraAdmin / owner). Holders never see a wallet-empty cache-mode reply.", "Maestro Unlimited Quota", null, 0, 0 },
                    { "platform.metronome.manage", null, "Create / edit / disable platform-scope Metronome scheduled jobs. Required to access the continuo-ops-ui Metronome panel and define cross-tenant operational jobs.", "Manage Scheduled Jobs (Metronome)", null, 0, 0 },
                    { "platform.tempo.manage", null, "Author / edit / disable platform-scope Tempo workflow definitions and view all instances across tenants. Required for the continuo-ops-ui Tempo panel.", "Manage Workflows (Tempo)", null, 0, 0 },
                    { "tenant.maestro.context.author", null, "Create reusable Maestro contexts (knowledge sources) scoped to the tenant.", "Author Maestro Contexts", null, 1, 0 },
                    { "tenant.maestro.playbook.author", null, "Create and version Maestro playbooks scoped to the tenant.", "Author Maestro Playbooks", null, 1, 0 },
                    { "tenant.maestro.use", null, "Open the Maestro chat assistant (floating button + drawer) inside tenant applications. Quota and persona enforced server-side per tenant policy.", "Use Maestro AI", null, 1, 0 },
                    { "tenant.metronome.manage", null, "Create / edit / disable tenant-scope Metronome scheduled jobs — EOD reports, nightly summary emails, etc. TenantOwner + TenantAdmin tier.", "Manage Scheduled Jobs (Metronome)", null, 1, 0 },
                    { "tenant.settings.billing.manage", null, "Access the AI Token Cüzdanı purchase screen + initiate token-pack purchases via iyzico. TenantOwner-tier permission.", "Manage Billing Settings", null, 1, 0 },
                    { "tenant.tempo.manage", null, "Author / edit tenant-scope Tempo workflow definitions — multi-step orchestrations (sipariş checkout pipeline, EOD report bundle, vs.).", "Manage Workflows (Tempo)", null, 1, 0 },
                    { "tenant.tempo.start", null, "Manually start an existing tenant-scope Tempo workflow without authoring rights. OperationManager-tier; lets a manager kick off pre-approved workflows.", "Trigger Workflows (Tempo)", null, 1, 0 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserSessions_SessionTokenHash",
                schema: "aut",
                table: "UserSessions",
                column: "SessionTokenHash",
                unique: true,
                filter: "[SessionTokenHash] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserSessions_SessionTokenHash",
                schema: "aut",
                table: "UserSessions");

            migrationBuilder.DeleteData(
                schema: "aut",
                table: "Permissions",
                keyColumn: "Key",
                keyValue: "customer.maestro.billing.manage");

            migrationBuilder.DeleteData(
                schema: "aut",
                table: "Permissions",
                keyColumn: "Key",
                keyValue: "customer.maestro.use");

            migrationBuilder.DeleteData(
                schema: "aut",
                table: "Permissions",
                keyColumn: "Key",
                keyValue: "ops.maestro.tenants.manage");

            migrationBuilder.DeleteData(
                schema: "aut",
                table: "Permissions",
                keyColumn: "Key",
                keyValue: "platform.maestro.unlimited");

            migrationBuilder.DeleteData(
                schema: "aut",
                table: "Permissions",
                keyColumn: "Key",
                keyValue: "platform.metronome.manage");

            migrationBuilder.DeleteData(
                schema: "aut",
                table: "Permissions",
                keyColumn: "Key",
                keyValue: "platform.tempo.manage");

            migrationBuilder.DeleteData(
                schema: "aut",
                table: "Permissions",
                keyColumn: "Key",
                keyValue: "tenant.maestro.context.author");

            migrationBuilder.DeleteData(
                schema: "aut",
                table: "Permissions",
                keyColumn: "Key",
                keyValue: "tenant.maestro.playbook.author");

            migrationBuilder.DeleteData(
                schema: "aut",
                table: "Permissions",
                keyColumn: "Key",
                keyValue: "tenant.maestro.use");

            migrationBuilder.DeleteData(
                schema: "aut",
                table: "Permissions",
                keyColumn: "Key",
                keyValue: "tenant.metronome.manage");

            migrationBuilder.DeleteData(
                schema: "aut",
                table: "Permissions",
                keyColumn: "Key",
                keyValue: "tenant.settings.billing.manage");

            migrationBuilder.DeleteData(
                schema: "aut",
                table: "Permissions",
                keyColumn: "Key",
                keyValue: "tenant.tempo.manage");

            migrationBuilder.DeleteData(
                schema: "aut",
                table: "Permissions",
                keyColumn: "Key",
                keyValue: "tenant.tempo.start");

            migrationBuilder.DropColumn(
                name: "SessionTokenHash",
                schema: "aut",
                table: "UserSessions");
        }
    }
}
