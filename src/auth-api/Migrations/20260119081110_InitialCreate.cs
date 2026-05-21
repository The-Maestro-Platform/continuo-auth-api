using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace authapi.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "aut");

            migrationBuilder.CreateTable(
                name: "ParameterDefinitions",
                schema: "aut",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(26)", maxLength: 26, nullable: false),
                    Module = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Section = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Key = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    DataType = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, defaultValue: "string"),
                    Scope = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, defaultValue: "global"),
                    Environment = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, defaultValue: "prod"),
                    TenantCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Locale = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    SiteCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FallbackValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Revision = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    IsSensitive = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParameterDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Permissions",
                schema: "aut",
                columns: table => new
                {
                    Key = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Scope = table.Column<int>(type: "int", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Icon = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "PlatformUsers",
                schema: "aut",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(26)", maxLength: 26, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                schema: "aut",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(26)", maxLength: 26, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Scope = table.Column<int>(type: "int", nullable: false),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false),
                    ParentRoleId = table.Column<string>(type: "nvarchar(26)", maxLength: 26, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Roles_Roles_ParentRoleId",
                        column: x => x.ParentRoleId,
                        principalSchema: "aut",
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Screens",
                schema: "aut",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(26)", maxLength: 26, nullable: false),
                    AppCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ScreenKey = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    RequiredPermissionsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Path = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Icon = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Group = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Screens", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SystemLogs",
                schema: "aut",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(26)", maxLength: 26, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UserId = table.Column<string>(type: "nvarchar(26)", maxLength: 26, nullable: true),
                    UserType = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(26)", maxLength: 26, nullable: true),
                    Action = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    EntityId = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tenants",
                schema: "aut",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(26)", maxLength: 26, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Subdomain = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    ContactEmail = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    ContactPhone = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Notes = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                schema: "aut",
                columns: table => new
                {
                    RoleId = table.Column<string>(type: "nvarchar(26)", maxLength: 26, nullable: false),
                    PermissionKey = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => new { x.RoleId, x.PermissionKey });
                    table.ForeignKey(
                        name: "FK_RolePermissions_Permissions_PermissionKey",
                        column: x => x.PermissionKey,
                        principalSchema: "aut",
                        principalTable: "Permissions",
                        principalColumn: "Key",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RolePermissions_Roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "aut",
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScreenRoles",
                schema: "aut",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(26)", maxLength: 26, nullable: false),
                    ScreenId = table.Column<string>(type: "nvarchar(26)", maxLength: 26, nullable: false),
                    RoleId = table.Column<string>(type: "nvarchar(26)", maxLength: 26, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScreenRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScreenRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "aut",
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScreenRoles_Screens_ScreenId",
                        column: x => x.ScreenId,
                        principalSchema: "aut",
                        principalTable: "Screens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScreenUsers",
                schema: "aut",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(26)", maxLength: 26, nullable: false),
                    ScreenId = table.Column<string>(type: "nvarchar(26)", maxLength: 26, nullable: false),
                    PlatformUserId = table.Column<string>(type: "nvarchar(26)", maxLength: 26, nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(26)", maxLength: 26, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScreenUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScreenUsers_PlatformUsers_PlatformUserId",
                        column: x => x.PlatformUserId,
                        principalSchema: "aut",
                        principalTable: "PlatformUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScreenUsers_Screens_ScreenId",
                        column: x => x.ScreenId,
                        principalSchema: "aut",
                        principalTable: "Screens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Customers",
                schema: "aut",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(26)", maxLength: 26, nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(26)", maxLength: 26, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LoyaltyWalletId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    FullName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    City = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Country = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    AddressLine1 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AddressLine2 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PostalCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    MarketingOptIn = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Customers_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aut",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TenantUsers",
                schema: "aut",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(26)", maxLength: 26, nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(26)", maxLength: 26, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    LastName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    AddressLine1 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AddressLine2 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    City = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Country = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    PostalCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    PositionTitle = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    MarketingOptIn = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantUsers_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aut",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CommunicationInfos",
                schema: "aut",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(26)", maxLength: 26, nullable: false),
                    OwnerType = table.Column<int>(type: "int", nullable: false),
                    PlatformUserId = table.Column<string>(type: "nvarchar(26)", maxLength: 26, nullable: true),
                    TenantUserId = table.Column<string>(type: "nvarchar(26)", maxLength: 26, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunicationInfos", x => x.Id);
                    table.CheckConstraint("CK_CommunicationInfos_Target", "\r\n                (CASE WHEN PlatformUserId IS NOT NULL THEN 1 ELSE 0 END)\r\n              + (CASE WHEN TenantUserId IS NOT NULL THEN 1 ELSE 0 END) = 1");
                    table.ForeignKey(
                        name: "FK_CommunicationInfos_PlatformUsers_PlatformUserId",
                        column: x => x.PlatformUserId,
                        principalSchema: "aut",
                        principalTable: "PlatformUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CommunicationInfos_TenantUsers_TenantUserId",
                        column: x => x.TenantUserId,
                        principalSchema: "aut",
                        principalTable: "TenantUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Credentials",
                schema: "aut",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(26)", maxLength: 26, nullable: false),
                    Login = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    PasswordHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    OwnerType = table.Column<int>(type: "int", nullable: false),
                    PlatformUserId = table.Column<string>(type: "nvarchar(26)", maxLength: 26, nullable: true),
                    TenantUserId = table.Column<string>(type: "nvarchar(26)", maxLength: 26, nullable: true),
                    CustomerId = table.Column<string>(type: "nvarchar(26)", maxLength: 26, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastLoginAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AgreementsAcceptedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AgreementsVersion = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    MustChangePassword = table.Column<bool>(type: "bit", nullable: false),
                    PasswordChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Credentials", x => x.Id);
                    table.CheckConstraint("CK_Credentials_OneOwner", "\r\n                (CASE WHEN PlatformUserId IS NOT NULL THEN 1 ELSE 0 END)\r\n              + (CASE WHEN TenantUserId IS NOT NULL THEN 1 ELSE 0 END)\r\n              + (CASE WHEN CustomerId IS NOT NULL THEN 1 ELSE 0 END) = 1");
                    table.ForeignKey(
                        name: "FK_Credentials_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "aut",
                        principalTable: "Customers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Credentials_PlatformUsers_PlatformUserId",
                        column: x => x.PlatformUserId,
                        principalSchema: "aut",
                        principalTable: "PlatformUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Credentials_TenantUsers_TenantUserId",
                        column: x => x.TenantUserId,
                        principalSchema: "aut",
                        principalTable: "TenantUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                schema: "aut",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(26)", maxLength: 26, nullable: false),
                    RoleId = table.Column<string>(type: "nvarchar(26)", maxLength: 26, nullable: false),
                    PlatformUserId = table.Column<string>(type: "nvarchar(26)", maxLength: 26, nullable: true),
                    TenantUserId = table.Column<string>(type: "nvarchar(26)", maxLength: 26, nullable: true),
                    AssignedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => x.Id);
                    table.CheckConstraint("CK_UserRoles_Target", "\r\n                (CASE WHEN PlatformUserId IS NOT NULL THEN 1 ELSE 0 END)\r\n              + (CASE WHEN TenantUserId IS NOT NULL THEN 1 ELSE 0 END) = 1");
                    table.ForeignKey(
                        name: "FK_UserRoles_PlatformUsers_PlatformUserId",
                        column: x => x.PlatformUserId,
                        principalSchema: "aut",
                        principalTable: "PlatformUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UserRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "aut",
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRoles_TenantUsers_TenantUserId",
                        column: x => x.TenantUserId,
                        principalSchema: "aut",
                        principalTable: "TenantUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ContactAddresses",
                schema: "aut",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(26)", maxLength: 26, nullable: false),
                    CommunicationInfoId = table.Column<string>(type: "nvarchar(26)", maxLength: 26, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Line1 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Line2 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    City = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Country = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    PostalCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContactAddresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContactAddresses_CommunicationInfos_CommunicationInfoId",
                        column: x => x.CommunicationInfoId,
                        principalSchema: "aut",
                        principalTable: "CommunicationInfos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContactPhones",
                schema: "aut",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(26)", maxLength: 26, nullable: false),
                    CommunicationInfoId = table.Column<string>(type: "nvarchar(26)", maxLength: 26, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    CountryCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    Number = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Extension = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContactPhones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContactPhones_CommunicationInfos_CommunicationInfoId",
                        column: x => x.CommunicationInfoId,
                        principalSchema: "aut",
                        principalTable: "CommunicationInfos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExternalLogins",
                schema: "aut",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(26)", maxLength: 26, nullable: false),
                    CredentialId = table.Column<string>(type: "nvarchar(26)", maxLength: 26, nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProviderUserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ProviderEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ProfilePictureUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    LastUsedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalLogins", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalLogins_Credentials_CredentialId",
                        column: x => x.CredentialId,
                        principalSchema: "aut",
                        principalTable: "Credentials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                schema: "aut",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(26)", maxLength: 26, nullable: false),
                    Token = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Expires = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Revoked = table.Column<bool>(type: "bit", nullable: false),
                    CredentialId = table.Column<string>(type: "nvarchar(26)", maxLength: 26, nullable: false),
                    ProcessedOn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshTokens_Credentials_CredentialId",
                        column: x => x.CredentialId,
                        principalSchema: "aut",
                        principalTable: "Credentials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TwoFactorChallenges",
                schema: "aut",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(26)", maxLength: 26, nullable: false),
                    CredentialId = table.Column<string>(type: "nvarchar(26)", maxLength: 26, nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Target = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CodeHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VerifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailedAttempts = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TwoFactorChallenges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TwoFactorChallenges_Credentials_CredentialId",
                        column: x => x.CredentialId,
                        principalSchema: "aut",
                        principalTable: "Credentials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "aut",
                table: "Permissions",
                columns: new[] { "Key", "Category", "Description", "DisplayName", "Icon", "Scope", "SortOrder" },
                values: new object[,]
                {
                    { "platform.auth.roles.manage", null, "Create platform roles and assign UI screens.", "Manage Platform Roles", null, 0, 0 },
                    { "platform.auth.screens.manage", null, "Create screens and assign screen access to platform users.", "Manage Screen Access", null, 0, 0 },
                    { "platform.auth.users.manage", null, "Create platform users, credentials and role assignments.", "Manage Platform Users", null, 0, 0 },
                    { "platform.auth.users.view", null, "List platform users for assignment and directory views.", "View Platform Users", null, 0, 0 },
                    { "platform.logs.view", null, "Inspect logs and audits across tenants.", "View System Logs", null, 0, 0 },
                    { "platform.parameters.manage", null, "Adjust system-wide configuration and parameter store.", "Manage Global Parameters", null, 0, 0 },
                    { "platform.security.manage", null, "Manage encrypted credentials, connection strings and platform secrets.", "Manage Security Vault", null, 0, 0 },
                    { "platform.security.reveal", null, "Reveal plaintext secrets (restricted to platform owner).", "Reveal Security Secrets", null, 0, 0 },
                    { "platform.support.impersonate", null, "Temporarily act on behalf of a tenant user for support.", "Impersonate Tenant User", null, 0, 0 },
                    { "platform.tcc.manage", null, "Operate global TCC treasury, rewards and wallet caps.", "Manage TCC Operations", null, 0, 0 },
                    { "platform.tenants.manage", null, "Create, update or disable any tenant instance.", "Manage Tenants", null, 0, 0 },
                    { "tenant.accounting.manage", null, "Control accounting workflows and billing reconciliation.", "Manage Accounting", null, 1, 0 },
                    { "tenant.branch.manage", null, "Onboard or update dealer and branch staff.", "Manage Branch Users", null, 1, 0 },
                    { "tenant.cash.manage", null, "Reconcile cashiers, settle cashier sessions.", "Manage Cash Registers", null, 1, 0 },
                    { "tenant.inventory.manage", null, "Add, update or adjust inventory items.", "Manage Inventory", null, 1, 0 },
                    { "tenant.inventory.view", null, "View stock levels and inventory items.", "View Inventory", null, 1, 0 },
                    { "tenant.layout.manage", null, "Adjust robot or floor layouts per tenant.", "Manage Layouts", null, 1, 0 },
                    { "tenant.menu.manage", null, "Create, update or remove menu items.", "Manage Menu", null, 1, 0 },
                    { "tenant.menu.view", null, "View menu items and categories.", "View Menu", null, 1, 0 },
                    { "tenant.orders.manage", null, "Update order status and process refunds.", "Manage Orders", null, 1, 0 },
                    { "tenant.orders.view", null, "View order history and details.", "View Orders", null, 1, 0 },
                    { "tenant.parameters.manage", null, "Adjust tenant-scoped configuration and parameter store.", "Manage Tenant Parameters", null, 1, 0 },
                    { "tenant.pos.operate", null, "Use point of sale for transactions.", "Operate POS", null, 1, 0 },
                    { "tenant.recipes.manage", null, "Create or update recipes.", "Manage Recipes", null, 1, 0 },
                    { "tenant.recipes.view", null, "View recipe definitions.", "View Recipes", null, 1, 0 },
                    { "tenant.reports.view", null, "Access tenant-specific sales and activity reports.", "View Tenant Reports", null, 1, 0 },
                    { "tenant.robots.manage", null, "Enroll, configure and monitor robots for tenant.", "Manage Robots", null, 1, 0 },
                    { "tenant.security.manage", null, "Manage tenant-scoped external API keys and credentials.", "Manage Tenant Secrets", null, 1, 0 },
                    { "tenant.tables.manage", null, "Configure table layout and assignments.", "Manage Tables", null, 1, 0 },
                    { "tenant.tables.view", null, "View table layout and status.", "View Tables", null, 1, 0 },
                    { "tenant.tcc.manage", null, "Operate tenant-scoped TCC programs and wallets.", "Manage Tenant TCC", null, 1, 0 },
                    { "tenant.users.manage", null, "Create or update tenant staff accounts.", "Manage Tenant Users", null, 1, 0 },
                    { "tenant.users.view", null, "View tenant staff and their roles.", "View Tenant Users", null, 1, 0 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationInfos_PlatformUserId",
                schema: "aut",
                table: "CommunicationInfos",
                column: "PlatformUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationInfos_TenantUserId",
                schema: "aut",
                table: "CommunicationInfos",
                column: "TenantUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ContactAddresses_CommunicationInfoId_IsPrimary",
                schema: "aut",
                table: "ContactAddresses",
                columns: new[] { "CommunicationInfoId", "IsPrimary" });

            migrationBuilder.CreateIndex(
                name: "IX_ContactPhones_CommunicationInfoId_IsPrimary",
                schema: "aut",
                table: "ContactPhones",
                columns: new[] { "CommunicationInfoId", "IsPrimary" });

            migrationBuilder.CreateIndex(
                name: "IX_Credentials_CustomerId",
                schema: "aut",
                table: "Credentials",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Credentials_Login",
                schema: "aut",
                table: "Credentials",
                column: "Login",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Credentials_PlatformUserId",
                schema: "aut",
                table: "Credentials",
                column: "PlatformUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Credentials_TenantUserId",
                schema: "aut",
                table: "Credentials",
                column: "TenantUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_TenantId",
                schema: "aut",
                table: "Customers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalLogins_CredentialId",
                schema: "aut",
                table: "ExternalLogins",
                column: "CredentialId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalLogins_Provider_ProviderUserId",
                schema: "aut",
                table: "ExternalLogins",
                columns: new[] { "Provider", "ProviderUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParameterDefinitions_KeyScope",
                schema: "aut",
                table: "ParameterDefinitions",
                columns: new[] { "Module", "Section", "Key", "Environment", "Scope", "TenantCode", "Locale", "SiteCode" });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformUsers_Email",
                schema: "aut",
                table: "PlatformUsers",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_CredentialId",
                schema: "aut",
                table: "RefreshTokens",
                column: "CredentialId");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_PermissionKey",
                schema: "aut",
                table: "RolePermissions",
                column: "PermissionKey");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Name",
                schema: "aut",
                table: "Roles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Roles_ParentRoleId",
                schema: "aut",
                table: "Roles",
                column: "ParentRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_ScreenRoles_RoleId",
                schema: "aut",
                table: "ScreenRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_ScreenRoles_ScreenId_RoleId",
                schema: "aut",
                table: "ScreenRoles",
                columns: new[] { "ScreenId", "RoleId" });

            migrationBuilder.CreateIndex(
                name: "IX_Screens_AppCode_ScreenKey",
                schema: "aut",
                table: "Screens",
                columns: new[] { "AppCode", "ScreenKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScreenUsers_PlatformUserId",
                schema: "aut",
                table: "ScreenUsers",
                column: "PlatformUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ScreenUsers_ScreenId_PlatformUserId_TenantId",
                schema: "aut",
                table: "ScreenUsers",
                columns: new[] { "ScreenId", "PlatformUserId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Code",
                schema: "aut",
                table: "Tenants",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantUsers_TenantId",
                schema: "aut",
                table: "TenantUsers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TwoFactorChallenges_CredentialId_CreatedAtUtc",
                schema: "aut",
                table: "TwoFactorChallenges",
                columns: new[] { "CredentialId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_PlatformUserId",
                schema: "aut",
                table: "UserRoles",
                column: "PlatformUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId",
                schema: "aut",
                table: "UserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_TenantUserId",
                schema: "aut",
                table: "UserRoles",
                column: "TenantUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContactAddresses",
                schema: "aut");

            migrationBuilder.DropTable(
                name: "ContactPhones",
                schema: "aut");

            migrationBuilder.DropTable(
                name: "ExternalLogins",
                schema: "aut");

            migrationBuilder.DropTable(
                name: "ParameterDefinitions",
                schema: "aut");

            migrationBuilder.DropTable(
                name: "RefreshTokens",
                schema: "aut");

            migrationBuilder.DropTable(
                name: "RolePermissions",
                schema: "aut");

            migrationBuilder.DropTable(
                name: "ScreenRoles",
                schema: "aut");

            migrationBuilder.DropTable(
                name: "ScreenUsers",
                schema: "aut");

            migrationBuilder.DropTable(
                name: "SystemLogs",
                schema: "aut");

            migrationBuilder.DropTable(
                name: "TwoFactorChallenges",
                schema: "aut");

            migrationBuilder.DropTable(
                name: "UserRoles",
                schema: "aut");

            migrationBuilder.DropTable(
                name: "CommunicationInfos",
                schema: "aut");

            migrationBuilder.DropTable(
                name: "Permissions",
                schema: "aut");

            migrationBuilder.DropTable(
                name: "Screens",
                schema: "aut");

            migrationBuilder.DropTable(
                name: "Credentials",
                schema: "aut");

            migrationBuilder.DropTable(
                name: "Roles",
                schema: "aut");

            migrationBuilder.DropTable(
                name: "Customers",
                schema: "aut");

            migrationBuilder.DropTable(
                name: "PlatformUsers",
                schema: "aut");

            migrationBuilder.DropTable(
                name: "TenantUsers",
                schema: "aut");

            migrationBuilder.DropTable(
                name: "Tenants",
                schema: "aut");
        }
    }
}
