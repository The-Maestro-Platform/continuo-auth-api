using AuthApi.Models;
using AuthApi.Permissions;
using Microsoft.EntityFrameworkCore;
using Continuo.Persistence;
using Continuo.Shared.Security;

namespace AuthApi;

public class AuthDbContext : ContinuoDbContext {
    private readonly ITenantContext? _tenantContext;

    // Tek constructor: DI ITenantContext'i set eder; EF tooling (migrations
    // design-time) sadece options ile çağırır → tenantContext null kalır →
    // global filter pasif. Login/register gibi pre-auth path'lerde de aynı
    // davranış (HasTenantScope=false → filter no-op).
    public AuthDbContext(DbContextOptions<AuthDbContext> options, ITenantContext? tenantContext = null) : base(options) {
        _tenantContext = tenantContext;
    }

    private bool HasTenantScope => _tenantContext?.HasTenant == true && _tenantContext.TenantId != Ulid.Empty;
    private Ulid CurrentTenantId => _tenantContext?.TenantId ?? Ulid.Empty;

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantUser> TenantUsers => Set<TenantUser>();
    public DbSet<PlatformUser> PlatformUsers => Set<PlatformUser>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Credential> Credentials => Set<Credential>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<SystemLog> SystemLogs => Set<SystemLog>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<TwoFactorChallenge> TwoFactorChallenges => Set<TwoFactorChallenge>();
    public DbSet<TrustedDevice> TrustedDevices => Set<TrustedDevice>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<Screen> Screens => Set<Screen>();
    public DbSet<ScreenUser> ScreenUsers => Set<ScreenUser>();
    public DbSet<ScreenRole> ScreenRoles => Set<ScreenRole>();
    public DbSet<CommunicationInfo> CommunicationInfos => Set<CommunicationInfo>();
    public DbSet<ContactAddress> ContactAddresses => Set<ContactAddress>();
    public DbSet<ContactPhone> ContactPhones => Set<ContactPhone>();
    public DbSet<ExternalLogin> ExternalLogins => Set<ExternalLogin>();
    public DbSet<PortalHandoff> PortalHandoffs => Set<PortalHandoff>();

    protected override void OnModelCreating(ModelBuilder builder) {
        base.OnModelCreating(builder);

        builder.Entity<Tenant>(b => {
            b.HasKey(x => x.Id);
            b.Property(x => x.Id)
                .ValueGeneratedNever()
                .HasConversion(v => v.ToString(), v => Ulid.Parse(v))
                .HasMaxLength(26);
            b.Property(x => x.Code).HasMaxLength(40).IsRequired();
            b.HasIndex(x => x.Code).IsUnique();
            b.Property(x => x.Name).HasMaxLength(160).IsRequired();
            b.Property(x => x.Slug).HasMaxLength(120);
            b.Property(x => x.Subdomain).HasMaxLength(80);
            b.Property(x => x.ContactEmail).HasMaxLength(160);
            b.Property(x => x.ContactPhone).HasMaxLength(32);
            b.Property(x => x.Notes).HasMaxLength(400);
            b.Property(x => x.Status).HasDefaultValue(TenantStatus.Active);
        });

        builder.Entity<TenantUser>(b => {
            b.HasKey(x => x.Id);
            b.Property(x => x.Id)
                .ValueGeneratedNever()
                .HasConversion(v => v.ToString(), v => Ulid.Parse(v))
                .HasMaxLength(26);
            b.Property(x => x.TenantId)
                .HasConversion(v => v.ToString(), v => Ulid.Parse(v))
                .HasMaxLength(26);
            b.Property(x => x.DisplayName).HasMaxLength(160).IsRequired();
            b.Property(x => x.FirstName).HasMaxLength(120);
            b.Property(x => x.LastName).HasMaxLength(120);
            b.Property(x => x.Email).HasMaxLength(160);
            b.Property(x => x.PhoneNumber).HasMaxLength(32);
            b.Property(x => x.AddressLine1).HasMaxLength(200);
            b.Property(x => x.AddressLine2).HasMaxLength(200);
            b.Property(x => x.City).HasMaxLength(120);
            b.Property(x => x.Country).HasMaxLength(120);
            b.Property(x => x.PostalCode).HasMaxLength(32);
            b.Property(x => x.PositionTitle).HasMaxLength(160);
            b.Property(x => x.Status).HasDefaultValue(TenantUserStatus.Active);
            b.HasOne(x => x.Tenant).WithMany(t => t.Users).HasForeignKey(x => x.TenantId);
            // Defansif tenant filter: TenantUser staff PII + role assignment'lara
            // bağlı → cross-tenant leak en riskli tablo. Login/seed pre-auth path'leri
            // tenant context yok → HasTenantScope=false → filter pasif. Cross-tenant
            // admin endpoint'leri için IgnoreQueryFilters() bypass.
            //
            // Global email uniqueness: TenantUser.Email check'leri scope'lansa da
            // Credential.Login UNIQUE index global yakalıyor; defense-in-depth bozulmuyor.
            b.HasQueryFilter(x => !HasTenantScope || x.TenantId == CurrentTenantId);
        });

        builder.Entity<PlatformUser>(b => {
            b.HasKey(x => x.Id);
            b.Property(x => x.Id)
                .ValueGeneratedNever()
                .HasConversion(v => v.ToString(), v => Ulid.Parse(v))
                .HasMaxLength(26);
            b.Property(x => x.Email).HasMaxLength(160).IsRequired();
            // Filtered: pasif PlatformUser maili yeni aktif PlatformUser eklemeyi bloklamaz.
            b.HasIndex(x => x.Email).IsUnique().HasFilter("[IsActive] = 1");
            b.Property(x => x.DisplayName).HasMaxLength(160).IsRequired();
        });

        builder.Entity<CommunicationInfo>(b => {
            b.HasKey(x => x.Id);
            b.Property(x => x.Id)
                .ValueGeneratedNever()
                .HasConversion(v => v.ToString(), v => Ulid.Parse(v))
                .HasMaxLength(26);
            b.Property(x => x.PlatformUserId).HasMaxLength(26);
            b.Property(x => x.TenantUserId).HasMaxLength(26);
            b.Property(x => x.OwnerType).HasConversion<int>();
            b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
            b.HasOne(x => x.PlatformUser).WithMany().HasForeignKey(x => x.PlatformUserId);
            b.HasOne(x => x.TenantUser).WithMany().HasForeignKey(x => x.TenantUserId);
            b.ToTable("CommunicationInfos", tb => tb.HasCheckConstraint("CK_CommunicationInfos_Target", @"
                (CASE WHEN PlatformUserId IS NOT NULL THEN 1 ELSE 0 END)
              + (CASE WHEN TenantUserId IS NOT NULL THEN 1 ELSE 0 END) = 1"));
        });

        builder.Entity<ContactAddress>(b => {
            b.HasKey(x => x.Id);
            b.Property(x => x.Id)
                .ValueGeneratedNever()
                .HasConversion(v => v.ToString(), v => Ulid.Parse(v))
                .HasMaxLength(26);
            b.Property(x => x.CommunicationInfoId)
                .HasConversion(v => v.ToString(), v => Ulid.Parse(v))
                .HasMaxLength(26);
            b.Property(x => x.Label).HasMaxLength(80);
            b.Property(x => x.Line1).HasMaxLength(200).IsRequired();
            b.Property(x => x.Line2).HasMaxLength(200);
            b.Property(x => x.City).HasMaxLength(120);
            b.Property(x => x.Country).HasMaxLength(120);
            b.Property(x => x.PostalCode).HasMaxLength(32);
            b.Property(x => x.Notes).HasMaxLength(200);
            b.Property(x => x.Type).HasConversion<int>();
            b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
            b.HasOne(x => x.CommunicationInfo).WithMany(ci => ci.Addresses).HasForeignKey(x => x.CommunicationInfoId);
            b.HasIndex(x => new { x.CommunicationInfoId, x.IsPrimary });
        });

        builder.Entity<ContactPhone>(b => {
            b.HasKey(x => x.Id);
            b.Property(x => x.Id)
                .ValueGeneratedNever()
                .HasConversion(v => v.ToString(), v => Ulid.Parse(v))
                .HasMaxLength(26);
            b.Property(x => x.CommunicationInfoId)
                .HasConversion(v => v.ToString(), v => Ulid.Parse(v))
                .HasMaxLength(26);
            b.Property(x => x.CountryCode).HasMaxLength(8);
            b.Property(x => x.Number).HasMaxLength(32).IsRequired();
            b.Property(x => x.Extension).HasMaxLength(16);
            b.Property(x => x.Notes).HasMaxLength(200);
            b.Property(x => x.Type).HasConversion<int>();
            b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
            b.HasOne(x => x.CommunicationInfo).WithMany(ci => ci.Phones).HasForeignKey(x => x.CommunicationInfoId);
            b.HasIndex(x => new { x.CommunicationInfoId, x.IsPrimary });
        });


        builder.Entity<Customer>(b => {
            b.HasKey(x => x.Id);
            b.Property(x => x.Id)
                .ValueGeneratedNever()
                .HasConversion(v => v.ToString(), v => Ulid.Parse(v))
                .HasMaxLength(26);
            b.Property(x => x.TenantId)
                .HasConversion(v => v.ToString(), v => Ulid.Parse(v))
                .HasMaxLength(26);
            b.Property(x => x.Email).HasMaxLength(160);
            b.Property(x => x.PhoneNumber).HasMaxLength(32);
            b.Property(x => x.DisplayName).HasMaxLength(200);
            b.Property(x => x.LoyaltyWalletId).HasMaxLength(120);
            b.Property(x => x.FullName).HasMaxLength(160);
            b.Property(x => x.City).HasMaxLength(120);
            b.Property(x => x.Country).HasMaxLength(120);
            b.Property(x => x.AddressLine1).HasMaxLength(200);
            b.Property(x => x.AddressLine2).HasMaxLength(200);
            b.Property(x => x.PostalCode).HasMaxLength(32);
            b.Property(x => x.Version).HasDefaultValue(1L).IsRequired().IsConcurrencyToken();
            b.HasOne(x => x.Tenant).WithMany(t => t.Customers).HasForeignKey(x => x.TenantId);
            // Defansif tenant filter: ITenantContext set ise queries otomatik tenant'a kısıtlanır.
            // Tenant set edilmediği pre-auth path'lerinde (login, public lookup) filter pasif kalır.
            // Bypass etmek isteyen kod IgnoreQueryFilters() kullanır.
            b.HasQueryFilter(x => !HasTenantScope || x.TenantId == CurrentTenantId);
        });

        builder.Entity<Credential>(b => {
            b.HasKey(x => x.Id);
            b.Property(x => x.Id)
                .ValueGeneratedNever()
                .HasConversion(v => v.ToString(), v => Ulid.Parse(v))
                .HasMaxLength(26);
            b.Property(x => x.Login).HasMaxLength(120).IsRequired();
            // Filtered: pasif credential ayni Login ile aktif yeni credential eklemeyi bloklamaz.
            // Audit icin pasif satir korunur; aktif satirlar arasinda tekillik garanti.
            b.HasIndex(x => x.Login).IsUnique().HasFilter("[IsActive] = 1");
            b.Property(x => x.Email).HasMaxLength(160);
            b.Property(x => x.PasswordHash).HasMaxLength(256).IsRequired();
            b.Property(x => x.PlatformUserId).HasMaxLength(26);
            b.Property(x => x.TenantUserId).HasMaxLength(26);
            b.Property(x => x.CustomerId).HasMaxLength(26);
            b.HasOne(x => x.PlatformUser).WithMany(p => p.Credentials).HasForeignKey(x => x.PlatformUserId);
            b.HasOne(x => x.TenantUser).WithMany(u => u.Credentials).HasForeignKey(x => x.TenantUserId);
            b.HasOne(x => x.Customer).WithMany(c => c.Credentials).HasForeignKey(x => x.CustomerId);
            b.ToTable("Credentials", tb => tb.HasCheckConstraint("CK_Credentials_OneOwner", @"
                (CASE WHEN PlatformUserId IS NOT NULL THEN 1 ELSE 0 END)
              + (CASE WHEN TenantUserId IS NOT NULL THEN 1 ELSE 0 END)
              + (CASE WHEN CustomerId IS NOT NULL THEN 1 ELSE 0 END) >= 1"));
        });

        builder.Entity<Role>(b => {
            b.HasKey(x => x.Id);
            b.Property(x => x.Id)
                .ValueGeneratedNever()
                .HasConversion(v => v.ToString(), v => Ulid.Parse(v))
                .HasMaxLength(26);
            b.Property(x => x.Name).HasMaxLength(100).IsRequired();
            b.HasIndex(x => x.Name).IsUnique();
            b.Property(x => x.Description).HasMaxLength(200);
            b.Property(x => x.ParentRoleId)
                .HasConversion(v => v.HasValue ? v.Value.ToString() : null, v => string.IsNullOrWhiteSpace(v) ? null : Ulid.Parse(v))
                .HasMaxLength(26);
            b.HasOne(x => x.ParentRole).WithMany(r => r.ChildRoles).HasForeignKey(x => x.ParentRoleId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<TwoFactorChallenge>(b => {
            b.HasKey(x => x.Id);
            b.Property(x => x.Id)
                .ValueGeneratedNever()
                .HasConversion(v => v.ToString(), v => Ulid.Parse(v))
                .HasMaxLength(26);
            b.Property(x => x.CredentialId)
                .HasConversion(v => v.ToString(), v => Ulid.Parse(v))
                .HasMaxLength(26)
                .IsRequired();
            b.Property(x => x.Channel).HasMaxLength(160).IsRequired();
            b.Property(x => x.Target).HasMaxLength(256).IsRequired();
            b.Property(x => x.CodeHash).HasMaxLength(128).IsRequired();
            b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
            b.Property(x => x.ExpiresAtUtc);
            b.Property(x => x.LastError).HasMaxLength(200);
            b.Property(x => x.LastResendAtUtc);
            b.HasOne(x => x.Credential).WithMany().HasForeignKey(x => x.CredentialId);
            b.HasIndex(x => new { x.CredentialId, x.CreatedAtUtc });
        });

        builder.Entity<TrustedDevice>(b => {
            b.HasKey(x => x.Id);
            b.Property(x => x.Id)
                .ValueGeneratedNever()
                .HasConversion(v => v.ToString(), v => Ulid.Parse(v))
                .HasMaxLength(26);
            b.Property(x => x.CredentialId)
                .HasConversion(v => v.ToString(), v => Ulid.Parse(v))
                .HasMaxLength(26)
                .IsRequired();
            b.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
            b.Property(x => x.UserAgent).HasMaxLength(512);
            b.Property(x => x.IpAddress).HasMaxLength(64);
            b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
            b.Property(x => x.LastUsedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
            b.HasOne(x => x.Credential).WithMany().HasForeignKey(x => x.CredentialId);
            b.HasIndex(x => new { x.CredentialId, x.TokenHash, x.RevokedAtUtc })
                .HasDatabaseName("IX_TrustedDevices_Lookup");
        });

        builder.Entity<PasswordResetToken>(b => {
            b.HasKey(x => x.Id);
            b.Property(x => x.Id)
                .ValueGeneratedNever()
                .HasConversion(v => v.ToString(), v => Ulid.Parse(v))
                .HasMaxLength(26);
            b.Property(x => x.CredentialId)
                .HasConversion(v => v.ToString(), v => Ulid.Parse(v))
                .HasMaxLength(26)
                .IsRequired();
            b.Property(x => x.AppId).HasMaxLength(64).IsRequired();
            b.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
            b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
            b.Property(x => x.RequestIp).HasMaxLength(64);
            b.Property(x => x.UserAgent).HasMaxLength(512);
            b.HasOne(x => x.Credential).WithMany().HasForeignKey(x => x.CredentialId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TokenHash).IsUnique();
            b.HasIndex(x => new { x.CredentialId, x.AppId, x.ConsumedAtUtc });
            b.HasIndex(x => x.ExpiresAtUtc);
        });

        builder.Entity<Permission>(b => {
            b.HasKey(x => x.Key);
            b.Property(x => x.Key).HasMaxLength(120).IsRequired();
            b.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
            b.Property(x => x.Description).HasMaxLength(400);
            b.Property(x => x.Category).HasMaxLength(80);
            b.Property(x => x.Icon).HasMaxLength(80);
            b.HasData(PermissionCatalog.All.Select(p => new Permission {
                Key = p.Key,
                DisplayName = p.DisplayName,
                Description = p.Description,
                Scope = p.Scope
            }));
        });

        builder.Entity<RolePermission>(b => {
            b.HasKey(x => new { x.RoleId, x.PermissionKey });
            b.Property(x => x.RoleId)
                .HasConversion(v => v.ToString(), v => Ulid.Parse(v))
                .HasMaxLength(26);
            b.Property(x => x.PermissionKey).HasMaxLength(120);
            b.HasOne(x => x.Role).WithMany(r => r.Permissions).HasForeignKey(x => x.RoleId);
            b.HasOne(x => x.Permission).WithMany(p => p.Roles).HasForeignKey(x => x.PermissionKey);
        });

        builder.Entity<UserRole>(b => {
            b.HasKey(x => x.Id);
            b.Property(x => x.Id)
                .ValueGeneratedNever()
                .HasConversion(v => v.ToString(), v => Ulid.Parse(v))
                .HasMaxLength(26);
            b.Property(x => x.RoleId)
                .HasConversion(v => v.ToString(), v => Ulid.Parse(v))
                .HasMaxLength(26);
            b.Property(x => x.PlatformUserId).HasMaxLength(26);
            b.Property(x => x.TenantUserId).HasMaxLength(26);
            b.Property(x => x.AssignedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
            b.HasOne(x => x.PlatformUser).WithMany(p => p.Roles).HasForeignKey(x => x.PlatformUserId);
            b.HasOne(x => x.TenantUser).WithMany(u => u.Roles).HasForeignKey(x => x.TenantUserId);
            b.HasOne(x => x.Role).WithMany(r => r.Members).HasForeignKey(x => x.RoleId);
            b.Property(x => x.BranchCode).HasMaxLength(50);
            b.HasIndex(x => new { x.TenantUserId, x.RoleId, x.BranchCode })
                .HasFilter("TenantUserId IS NOT NULL")
                .IsUnique();
            b.ToTable("UserRoles", tb => tb.HasCheckConstraint("CK_UserRoles_Target", @"
                (CASE WHEN PlatformUserId IS NOT NULL THEN 1 ELSE 0 END)
              + (CASE WHEN TenantUserId IS NOT NULL THEN 1 ELSE 0 END) = 1"));
        });

        builder.Entity<SystemLog>(b => {
            b.HasKey(x => x.Id);
            b.Property(x => x.Id)
                .ValueGeneratedNever()
                .HasConversion(v => v.ToString(), v => Ulid.Parse(v))
                .HasMaxLength(26);
            b.Property(x => x.UserId)
                .HasConversion(v => v.HasValue ? v.Value.ToString() : null, v => string.IsNullOrWhiteSpace(v) ? null : Ulid.Parse(v))
                .HasMaxLength(26);
            b.Property(x => x.Timestamp).HasDefaultValueSql("SYSUTCDATETIME()");
            b.Property(x => x.Action).HasMaxLength(160).IsRequired();
            b.Property(x => x.EntityType).HasMaxLength(160);
            b.Property(x => x.EntityId).HasMaxLength(160);
            b.Property(x => x.Metadata);
            b.Property(x => x.TenantId)
                .HasConversion(v => v.HasValue ? v.Value.ToString() : null, v => string.IsNullOrWhiteSpace(v) ? null : Ulid.Parse(v))
                .HasMaxLength(26);
        });

        builder.Entity<RefreshToken>(b => {
            b.HasKey(x => x.Id);
            b.Property(x => x.Id)
                .ValueGeneratedNever()
                .HasConversion(v => v.ToString(), v => Ulid.Parse(v))
                .HasMaxLength(26);
            b.Property(x => x.Token).IsRequired();
            b.Property(x => x.CredentialId)
                .HasConversion(v => v.ToString(), v => Ulid.Parse(v))
                .HasMaxLength(26);
            b.HasOne(x => x.Credential).WithMany().HasForeignKey(x => x.CredentialId);
        });

        builder.Entity<UserSession>(b => {
            b.HasKey(x => x.Id);
            b.Property(x => x.Id)
                .ValueGeneratedNever()
                .HasConversion(v => v.ToString(), v => Ulid.Parse(v))
                .HasMaxLength(26);
            b.Property(x => x.CredentialId)
                .HasConversion(v => v.ToString(), v => Ulid.Parse(v))
                .HasMaxLength(26)
                .IsRequired();
            b.Property(x => x.AppId).HasMaxLength(64).IsRequired();
            b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
            b.Property(x => x.LastSeenAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
            b.Property(x => x.RevokedReason).HasMaxLength(40);
            b.Property(x => x.IpAddress).HasMaxLength(64);
            b.Property(x => x.UserAgent).HasMaxLength(512);
            b.HasOne(x => x.Credential).WithMany().HasForeignKey(x => x.CredentialId);
            b.HasIndex(x => new { x.CredentialId, x.AppId, x.RevokedAtUtc });
        });

        builder.Entity<Screen>(b => {
            b.HasKey(x => x.Id);
            b.Property(x => x.Id)
                .ValueGeneratedNever()
                .HasConversion(v => v.ToString(), v => Ulid.Parse(v))
                .HasMaxLength(26);
            b.Property(x => x.AppCode).HasMaxLength(80).IsRequired();
            b.Property(x => x.ScreenKey).HasMaxLength(160).IsRequired();
            b.Property(x => x.Title).HasMaxLength(160).IsRequired();
            b.Property(x => x.Description).HasMaxLength(400);
            b.Property(x => x.RequiredPermissionsJson);
            b.Property(x => x.Path).HasMaxLength(200);
            b.Property(x => x.Icon).HasMaxLength(80);
            b.Property(x => x.Group).HasMaxLength(120);
            b.Property(x => x.SortOrder).HasDefaultValue(0);
            b.Property(x => x.IsSystem).HasDefaultValue(false);
            b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
            b.HasIndex(x => new { x.AppCode, x.ScreenKey }).IsUnique();
        });

        builder.Entity<ScreenUser>(b => {
            b.HasKey(x => x.Id);
            b.Property(x => x.Id)
                .ValueGeneratedNever()
                .HasConversion(v => v.ToString(), v => Ulid.Parse(v))
                .HasMaxLength(26);
            b.Property(x => x.ScreenId)
                .HasConversion(v => v.ToString(), v => Ulid.Parse(v))
                .HasMaxLength(26);
            b.Property(x => x.PlatformUserId)
                .HasConversion(v => v.ToString(), v => Ulid.Parse(v))
                .HasMaxLength(26);
            b.Property(x => x.TenantId)
                .HasConversion(v => v.HasValue ? v.Value.ToString() : null, v => string.IsNullOrWhiteSpace(v) ? null : Ulid.Parse(v))
                .HasMaxLength(26);
            b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
            b.Property(x => x.CreatedBy).HasMaxLength(160);
            b.HasOne(x => x.Screen).WithMany().HasForeignKey(x => x.ScreenId);
            b.HasOne(x => x.PlatformUser).WithMany().HasForeignKey(x => x.PlatformUserId);
            b.HasIndex(x => new { x.ScreenId, x.PlatformUserId, x.TenantId });
        });

        builder.Entity<ScreenRole>(b => {
            b.HasKey(x => x.Id);
            b.Property(x => x.Id)
                .ValueGeneratedNever()
                .HasConversion(v => v.ToString(), v => Ulid.Parse(v))
                .HasMaxLength(26);
            b.Property(x => x.ScreenId)
                .HasConversion(v => v.ToString(), v => Ulid.Parse(v))
                .HasMaxLength(26);
            b.Property(x => x.RoleId)
                .HasConversion(v => v.ToString(), v => Ulid.Parse(v))
                .HasMaxLength(26);
            b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
            b.HasOne(x => x.Screen).WithMany().HasForeignKey(x => x.ScreenId);
            b.HasOne(x => x.Role).WithMany(r => r.ScreenAssignments).HasForeignKey(x => x.RoleId);
            b.HasIndex(x => new { x.ScreenId, x.RoleId });
        });

        builder.Entity<ExternalLogin>(b => {
            b.HasKey(x => x.Id);
            b.Property(x => x.Id)
                .ValueGeneratedNever()
                .HasConversion(v => v.ToString(), v => Ulid.Parse(v))
                .HasMaxLength(26);
            b.Property(x => x.CredentialId)
                .HasConversion(v => v.ToString(), v => Ulid.Parse(v))
                .HasMaxLength(26)
                .IsRequired();
            b.Property(x => x.Provider).HasMaxLength(50).IsRequired();
            b.Property(x => x.ProviderUserId).HasMaxLength(256).IsRequired();
            b.Property(x => x.ProviderEmail).HasMaxLength(256);
            b.Property(x => x.ProviderDisplayName).HasMaxLength(256);
            b.Property(x => x.ProfilePictureUrl).HasMaxLength(512);
            b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
            b.HasOne(x => x.Credential).WithMany().HasForeignKey(x => x.CredentialId);
            b.HasIndex(x => new { x.Provider, x.ProviderUserId }).IsUnique();
        });

        builder.Entity<PortalHandoff>(b => {
            b.HasKey(x => x.Id);
            b.Property(x => x.Id)
                .ValueGeneratedNever()
                .HasConversion(v => v.ToString(), v => Ulid.Parse(v))
                .HasMaxLength(26);
            b.Property(x => x.CredentialId)
                .HasConversion(v => v.ToString(), v => Ulid.Parse(v))
                .HasMaxLength(26)
                .IsRequired();
            b.Property(x => x.Nonce).HasMaxLength(64).IsRequired();
            b.Property(x => x.TargetUiApp).HasMaxLength(64).IsRequired();
            b.Property(x => x.Environment).HasMaxLength(16).IsRequired();
            b.Property(x => x.TenantSlug).HasMaxLength(120);
            b.Property(x => x.TargetUrl).HasMaxLength(512).IsRequired();
            b.Property(x => x.ConsumedFromIp).HasMaxLength(64);
            b.HasOne(x => x.Credential).WithMany().HasForeignKey(x => x.CredentialId);
            b.HasIndex(x => x.Nonce).IsUnique();
            b.HasIndex(x => x.ExpiresAtUtc);
        });
    }
}
