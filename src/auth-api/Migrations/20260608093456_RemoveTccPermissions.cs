using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace authapi.Migrations
{
    /// <inheritdoc />
    public partial class RemoveTccPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "aut",
                table: "Permissions",
                keyColumn: "Key",
                keyValue: "platform.tcc.manage");

            migrationBuilder.DeleteData(
                schema: "aut",
                table: "Permissions",
                keyColumn: "Key",
                keyValue: "tenant.tcc.manage");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "aut",
                table: "Permissions",
                columns: new[] { "Key", "Category", "Description", "DisplayName", "Icon", "Scope", "SortOrder" },
                values: new object[,]
                {
                    { "platform.tcc.manage", null, "Operate global TCC treasury, rewards and wallet caps.", "Manage TCC Operations", null, 0, 0 },
                    { "tenant.tcc.manage", null, "Operate tenant-scoped TCC programs and wallets.", "Manage Tenant TCC", null, 1, 0 }
                });
        }
    }
}
