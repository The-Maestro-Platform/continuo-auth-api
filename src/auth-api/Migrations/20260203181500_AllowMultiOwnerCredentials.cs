using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace authapi.Migrations
{
    /// <inheritdoc />
    public partial class AllowMultiOwnerCredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Credentials_OneOwner",
                schema: "aut",
                table: "Credentials");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Credentials_OneOwner",
                schema: "aut",
                table: "Credentials",
                sql: @"
                (CASE WHEN PlatformUserId IS NOT NULL THEN 1 ELSE 0 END)
              + (CASE WHEN TenantUserId IS NOT NULL THEN 1 ELSE 0 END)
              + (CASE WHEN CustomerId IS NOT NULL THEN 1 ELSE 0 END) >= 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Credentials_OneOwner",
                schema: "aut",
                table: "Credentials");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Credentials_OneOwner",
                schema: "aut",
                table: "Credentials",
                sql: @"
                (CASE WHEN PlatformUserId IS NOT NULL THEN 1 ELSE 0 END)
              + (CASE WHEN TenantUserId IS NOT NULL THEN 1 ELSE 0 END)
              + (CASE WHEN CustomerId IS NOT NULL THEN 1 ELSE 0 END) = 1");
        }
    }
}
