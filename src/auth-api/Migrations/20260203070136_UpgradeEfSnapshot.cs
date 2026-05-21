using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace authapi.Migrations
{
    /// <inheritdoc />
    public partial class UpgradeEfSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Seed data (Permissions) already exists in the database.
            // This migration only updates the EF Core model snapshot from 8.0.8 to 10.0.0.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No schema changes to revert.
        }
    }
}
