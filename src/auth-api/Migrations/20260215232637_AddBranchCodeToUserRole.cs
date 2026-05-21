using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace authapi.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchCodeToUserRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserRoles_TenantUserId",
                schema: "aut",
                table: "UserRoles");

            migrationBuilder.AddColumn<string>(
                name: "BranchCode",
                schema: "aut",
                table: "UserRoles",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            // This migration may run on databases where these permission keys
            // were already inserted by older seeds/scripts. Keep insert idempotent.
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM [aut].[Permissions] WHERE [Key] = N'tenant.customers.manage')
                BEGIN
                    INSERT INTO [aut].[Permissions] ([Key], [Category], [Description], [DisplayName], [Icon], [Scope], [SortOrder])
                    VALUES (N'tenant.customers.manage', NULL, N'Edit customer profiles and manage customer data.', N'Manage Customers', NULL, 1, 0);
                END;

                IF NOT EXISTS (SELECT 1 FROM [aut].[Permissions] WHERE [Key] = N'tenant.customers.view')
                BEGIN
                    INSERT INTO [aut].[Permissions] ([Key], [Category], [Description], [DisplayName], [Icon], [Scope], [SortOrder])
                    VALUES (N'tenant.customers.view', NULL, N'View customer list and profiles.', N'View Customers', NULL, 1, 0);
                END;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_TenantUserId_RoleId_BranchCode",
                schema: "aut",
                table: "UserRoles",
                columns: new[] { "TenantUserId", "RoleId", "BranchCode" },
                unique: true,
                filter: "TenantUserId IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserRoles_TenantUserId_RoleId_BranchCode",
                schema: "aut",
                table: "UserRoles");

            migrationBuilder.DeleteData(
                schema: "aut",
                table: "Permissions",
                keyColumn: "Key",
                keyValue: "tenant.customers.manage");

            migrationBuilder.DeleteData(
                schema: "aut",
                table: "Permissions",
                keyColumn: "Key",
                keyValue: "tenant.customers.view");

            migrationBuilder.DropColumn(
                name: "BranchCode",
                schema: "aut",
                table: "UserRoles");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_TenantUserId",
                schema: "aut",
                table: "UserRoles",
                column: "TenantUserId");
        }
    }
}
