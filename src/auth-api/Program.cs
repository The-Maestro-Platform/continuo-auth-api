using System.Text;
using AuthApi;
using AuthApi.Infrastructure;
using AuthApi.Middleware;
using AuthApi.Seed;
using AuthApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Continuo.Configuration.Extensions;
using Continuo.Configuration.Parameters;
using Continuo.Configuration.Services;
using Continuo.Messaging;
using Continuo.Observability;
using Continuo.Observability.Discovery;
using Continuo.Persistence;

var serviceName = "auth-api";
var builder = Bootstrap.CreateBuilder(args, serviceName);

// Register persistence, messaging and other building-blocks
builder.Services.AddContinuoPersistence(builder.Configuration, serviceName);
builder.Services.AddContinuoMessaging(builder.Configuration, serviceName);
builder.Services.AddContinuoParameterStore(builder.Configuration);

builder.Services.AddServiceDbContext<AuthDbContext>(builder.Configuration, serviceName);
builder.Services.AddContinuoServiceDiscovery(builder.Configuration, serviceName);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ParameterDefinitionsService<AuthDbContext>>();
builder.Services.AddScoped<ITenantContext, TenantContext>();

// JWT configuration
var jwtSecret = builder.Configuration["JWT:SECRET"] ?? builder.Configuration["JWT__SECRET"] ?? "ReplaceWithASecureSecretKeyOfAtLeast32Chars";
if (jwtSecret.StartsWith("security://", StringComparison.OrdinalIgnoreCase)) {
    var secretName = jwtSecret["security://".Length..].Trim();
    if (string.IsNullOrWhiteSpace(secretName)) {
        throw new InvalidOperationException("JWT secret uses security:// but secret name is empty.");
    }

    var resolved = await SecurityApiRuntimeClient.TryResolvePlatformSecretAsync(builder.Configuration, secretName, CancellationToken.None);
    if (string.IsNullOrWhiteSpace(resolved)) {
        throw new InvalidOperationException($"JWT secret '{jwtSecret}' could not be resolved from security-api. Ensure security-api is running and M2M_API_KEY is configured.");
    }

    // Make it available for TokenService and other consumers.
    builder.Configuration["JWT__SECRET"] = resolved;
    builder.Configuration["JWT:SECRET"] = resolved;
    jwtSecret = resolved;
}
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));

builder.Services.AddAuthentication(options => {
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
    .AddJwtBearer(options => {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["JWT:ISSUER"] ?? builder.Configuration["JWT__ISSUER"],
            ValidAudience = builder.Configuration["JWT:AUDIENCE"] ?? builder.Configuration["JWT__AUDIENCE"],
            IssuerSigningKey = signingKey
        };
    });

// Token service for JWT and refresh tokens
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<ISessionService, SessionService>();
builder.Services.AddScoped<AuthApi.Services.RolesService>();
builder.Services.AddScoped<AuthApi.Services.RoleService>();
builder.Services.AddScoped<AuthApi.Services.TenantsService>();
builder.Services.AddScoped<AuthApi.Services.CredentialsService>();
builder.Services.AddScoped<AuthApi.Services.CustomersService>();
builder.Services.AddScoped<AuthApi.Services.CustomerProjectionBackfillService>();
builder.Services.AddScoped<AuthApi.Services.UsersService>();
builder.Services.AddScoped<AuthApi.Services.CommunicationService>();
builder.Services.AddScoped<AuthApi.Services.PermissionsService>();
builder.Services.AddScoped<IScreenAccessService, ScreenAccessService>();
builder.Services.AddScoped<AuthApi.Services.NavigationService>();
builder.Services.AddScoped<AuthApi.Services.PlatformSettings.IPlatformSettingsService, AuthApi.Services.PlatformSettings.PlatformSettingsService>();
builder.Services.Configure<TwoFactorOptions>(builder.Configuration.GetSection("TwoFactor"));
builder.Services.AddScoped<TwoFactorService>();
builder.Services.AddScoped<ITrustedDeviceService, TrustedDeviceService>();
builder.Services.AddScoped<PasswordResetService>();

// PlatformSecretResolver (5dk in-memory cache) â€” Google_ClientId vb. secret'leri
// security-api Ã¼zerinden Ã§ekmek iÃ§in. M2MKey dependency'si sÄ±rasÄ± Ã¶nemli.
// AddPlatformM2MKey idempotent deÄŸil; Bootstrap zaten kayÄ±t etmiÅŸse tekrar etmeyiz.
builder.Services.AddPlatformM2MKey();
builder.Services.AddPlatformSecretResolver();
builder.Services.AddScoped<IGoogleAuthService, GoogleAuthService>();

// Use Outbox + MassTransit for M2M 2FA dispatch (idempotent consumers handle duplicates).
builder.Services.AddScoped<ITwoFactorNotifier, OutboxTwoFactorNotifier>();

builder.Services.AddContinuoMigrationHostedService<AuthDbContext>(serviceName, ensureCreatedFallback: false);
var app = Bootstrap.CreateApp(builder, serviceName);

app.MapParameterDefinitionEndpoints<AuthDbContext>(serviceName);

app.UpdateEndpointProxyFromRoutes(serviceName, baseUrl: null, version: "v1");

// Apply migrations and seed custom SSO data

// Track 3 â€” Portal SSO iÃ§in PortalHandoffs tablosunu + permission seed'i
// idempotent oluÅŸtur. Auth-api EF migrations kullanÄ±yor ama bu Ã¶zel bir hot-fix
// (mevcut DB'ye migration yazmak yerine). Permanent migration ileride Ã¼retilebilir.
using (var ddlScope = app.Services.CreateScope()) {
    try {
        var db = ddlScope.ServiceProvider.GetRequiredService<AuthDbContext>();
        if (db.Database.IsRelational()) {
            await db.Database.ExecuteSqlRawAsync(@"
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'aut' AND TABLE_NAME = 'PortalHandoffs')
BEGIN
    CREATE TABLE [aut].[PortalHandoffs] (
        [Id] nvarchar(26) NOT NULL,
        [Nonce] nvarchar(64) NOT NULL,
        [CredentialId] nvarchar(26) NOT NULL,
        [TargetUiApp] nvarchar(64) NOT NULL,
        [Environment] nvarchar(16) NOT NULL,
        [TenantSlug] nvarchar(120) NULL,
        [TargetUrl] nvarchar(512) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [ExpiresAtUtc] datetime2 NOT NULL,
        [ConsumedAtUtc] datetime2 NULL,
        [ConsumedFromIp] nvarchar(64) NULL,
        CONSTRAINT [PK_PortalHandoffs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PortalHandoffs_Credentials_CredentialId]
            FOREIGN KEY ([CredentialId]) REFERENCES [aut].[Credentials] ([Id])
    );
    CREATE UNIQUE INDEX [IX_PortalHandoffs_Nonce] ON [aut].[PortalHandoffs] ([Nonce]);
    CREATE INDEX [IX_PortalHandoffs_ExpiresAtUtc] ON [aut].[PortalHandoffs] ([ExpiresAtUtc]);
END;

-- Portal permission keys idempotent seed
INSERT INTO [aut].[Permissions] ([Key], [Category], [Description], [DisplayName], [Icon], [Scope], [SortOrder])
SELECT v.[Key], v.[Category], v.[Description], v.[DisplayName], v.[Icon], v.[Scope], v.[SortOrder]
FROM (VALUES
    (N'platform.portal.access',      CAST(NULL AS nvarchar(max)), N'Allows the user to log in to the platform portal and pick a target environment / tenant.', N'Portal Access',       CAST(NULL AS nvarchar(max)), 0, 0),
    (N'platform.portal.env.dev',     CAST(NULL AS nvarchar(max)), N'Dev environment shown in the portal env picker.',                                          N'Portal Env: Dev',     CAST(NULL AS nvarchar(max)), 0, 0),
    (N'platform.portal.env.staging', CAST(NULL AS nvarchar(max)), N'Staging environment shown in the portal env picker.',                                      N'Portal Env: Staging', CAST(NULL AS nvarchar(max)), 0, 0),
    (N'platform.portal.env.prod',    CAST(NULL AS nvarchar(max)), N'Prod environment shown in the portal env picker. Restricted role.',                        N'Portal Env: Prod',    CAST(NULL AS nvarchar(max)), 0, 0)
) AS v([Key], [Category], [Description], [DisplayName], [Icon], [Scope], [SortOrder])
WHERE NOT EXISTS (SELECT 1 FROM [aut].[Permissions] p WHERE p.[Key] = v.[Key]);
");

            // 2FA trusted-device + resend-tracking schema. Idempotent hot-fix
            // (same pattern as PortalHandoffs above). A permanent EF migration
            // should be generated via `dotnet ef migrations add` next pass; the
            // entity is already configured in AuthDbContext so the model state
            // is correct, only DDL is bootstrapped here.
            await db.Database.ExecuteSqlRawAsync(@"
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'aut' AND TABLE_NAME = 'TwoFactorChallenges' AND COLUMN_NAME = 'LastResendAtUtc')
BEGIN
    ALTER TABLE [aut].[TwoFactorChallenges] ADD [LastResendAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'aut' AND TABLE_NAME = 'TrustedDevices')
BEGIN
    CREATE TABLE [aut].[TrustedDevices] (
        [Id] nvarchar(26) NOT NULL,
        [CredentialId] nvarchar(26) NOT NULL,
        [TokenHash] nvarchar(128) NOT NULL,
        [UserAgent] nvarchar(512) NULL,
        [IpAddress] nvarchar(64) NULL,
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [ExpiresAtUtc] datetime2 NOT NULL,
        [LastUsedAtUtc] datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [RevokedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_TrustedDevices] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TrustedDevices_Credentials_CredentialId]
            FOREIGN KEY ([CredentialId]) REFERENCES [aut].[Credentials] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_TrustedDevices_Lookup] ON [aut].[TrustedDevices] ([CredentialId], [TokenHash], [RevokedAtUtc]);
END;
");
        }
    }
    catch (Exception ex) {
        Console.WriteLine($"Portal DDL bootstrap warning: {ex.Message}");
    }
}

using (var scope = app.Services.CreateScope()) {
    try {
        var ctx = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        await AuthSeeder.SeedAsync(ctx);
    }
    catch (Exception ex) {
        Console.WriteLine($"Error seeding auth data: {ex}");
    }
}

app.MapGet("/", () => Results.Ok(new { Service = serviceName }));

app.UseMiddleware<TenantResolutionMiddleware>();
app.UseExceptionLogging();
app.UseRequestResponseLogging();
app.Run();

public partial class Program;
