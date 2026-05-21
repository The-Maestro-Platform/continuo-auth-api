using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace authapi.Migrations
{
    /// <inheritdoc />
    public partial class AddSeleniumRunPermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotent insert: bu permission key'leri PermissionCatalog seeder'ı da
            // önceden oluşturmuş olabilir. `InsertData` ham INSERT ürettiği için PK violation
            // veriyor. Bu yüzden `WHERE NOT EXISTS` ile koruma altında raw SQL kullanıyoruz.
            migrationBuilder.Sql(@"
                INSERT INTO [aut].[Permissions] ([Key], [Category], [Description], [DisplayName], [Icon], [Scope], [SortOrder])
                SELECT v.[Key], v.[Category], v.[Description], v.[DisplayName], v.[Icon], v.[Scope], v.[SortOrder]
                FROM (VALUES
                    (N'platform.support.selenium.run',       CAST(NULL AS nvarchar(max)), N'Queue, run and manage Selenium runner test catalog, scenarios and flows from the support console.', N'Run Selenium Tests',           CAST(NULL AS nvarchar(max)), 0, 0),
                    (N'tenant.notifications.templates.manage',       CAST(NULL AS nvarchar(max)), N'Create and update tenant-scoped content and notification templates.',                 N'Manage Content Templates',    CAST(NULL AS nvarchar(max)), 1, 0),
                    (N'tenant.notifications.templates.media.manage', CAST(NULL AS nvarchar(max)), N'Upload and manage template media assets such as email images.',                       N'Manage Template Media',       CAST(NULL AS nvarchar(max)), 1, 0),
                    (N'tenant.notifications.templates.publish',      CAST(NULL AS nvarchar(max)), N'Publish or archive tenant-scoped content and notification templates.',                N'Publish Content Templates',   CAST(NULL AS nvarchar(max)), 1, 0),
                    (N'tenant.notifications.templates.view',         CAST(NULL AS nvarchar(max)), N'View tenant-scoped content and notification template definitions.',                   N'View Content Templates',      CAST(NULL AS nvarchar(max)), 1, 0),
                    (N'tenant.setup.track',                          CAST(NULL AS nvarchar(max)), N'Track tenant onboarding and setup checklist progress.',                               N'Track Setup Progress',        CAST(NULL AS nvarchar(max)), 1, 0),
                    (N'tenant.vision.edge.manage',                   CAST(NULL AS nvarchar(max)), N'Configure edge connection, camera list and stream method.',                           N'Manage Vision Edge',          CAST(NULL AS nvarchar(max)), 1, 0),
                    (N'tenant.vision.edge.view',                     CAST(NULL AS nvarchar(max)), N'View camera panels and occupancy streams for branch edge devices.',                   N'View Vision Edge',            CAST(NULL AS nvarchar(max)), 1, 0)
                ) AS v([Key], [Category], [Description], [DisplayName], [Icon], [Scope], [SortOrder])
                WHERE NOT EXISTS (SELECT 1 FROM [aut].[Permissions] p WHERE p.[Key] = v.[Key]);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "aut",
                table: "Permissions",
                keyColumn: "Key",
                keyValue: "platform.support.selenium.run");

            migrationBuilder.DeleteData(
                schema: "aut",
                table: "Permissions",
                keyColumn: "Key",
                keyValue: "tenant.notifications.templates.manage");

            migrationBuilder.DeleteData(
                schema: "aut",
                table: "Permissions",
                keyColumn: "Key",
                keyValue: "tenant.notifications.templates.media.manage");

            migrationBuilder.DeleteData(
                schema: "aut",
                table: "Permissions",
                keyColumn: "Key",
                keyValue: "tenant.notifications.templates.publish");

            migrationBuilder.DeleteData(
                schema: "aut",
                table: "Permissions",
                keyColumn: "Key",
                keyValue: "tenant.notifications.templates.view");

            migrationBuilder.DeleteData(
                schema: "aut",
                table: "Permissions",
                keyColumn: "Key",
                keyValue: "tenant.setup.track");

            migrationBuilder.DeleteData(
                schema: "aut",
                table: "Permissions",
                keyColumn: "Key",
                keyValue: "tenant.vision.edge.manage");

            migrationBuilder.DeleteData(
                schema: "aut",
                table: "Permissions",
                keyColumn: "Key",
                keyValue: "tenant.vision.edge.view");
        }
    }
}
