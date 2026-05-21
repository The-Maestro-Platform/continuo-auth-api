using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace authapi.Migrations
{
    /// <inheritdoc />
    public partial class AllowReuseOfInactiveLogin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Credentials.Login: filtered unique (sadece aktif satirlar arasinda tekil).
            // Pasif edilen credential ayni mail/login ile yeni aktif credential
            // eklemeyi bloklamaz; audit icin eski satir korunur.
            migrationBuilder.DropIndex(
                name: "IX_Credentials_Login",
                schema: "aut",
                table: "Credentials");

            migrationBuilder.CreateIndex(
                name: "IX_Credentials_Login",
                schema: "aut",
                table: "Credentials",
                column: "Login",
                unique: true,
                filter: "[IsActive] = 1");

            // PlatformUsers.Email: ayni mantik, pasif PlatformUser maili tekrar
            // aktif PlatformUser olarak eklenebilir.
            migrationBuilder.DropIndex(
                name: "IX_PlatformUsers_Email",
                schema: "aut",
                table: "PlatformUsers");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformUsers_Email",
                schema: "aut",
                table: "PlatformUsers",
                column: "Email",
                unique: true,
                filter: "[IsActive] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlatformUsers_Email",
                schema: "aut",
                table: "PlatformUsers");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformUsers_Email",
                schema: "aut",
                table: "PlatformUsers",
                column: "Email",
                unique: true);

            migrationBuilder.DropIndex(
                name: "IX_Credentials_Login",
                schema: "aut",
                table: "Credentials");

            migrationBuilder.CreateIndex(
                name: "IX_Credentials_Login",
                schema: "aut",
                table: "Credentials",
                column: "Login",
                unique: true);
        }
    }
}
