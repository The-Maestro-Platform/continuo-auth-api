using AuthApi.Models;
using AuthApi.Permissions;
using Microsoft.EntityFrameworkCore;

namespace AuthApi.Seed;

public static class AuthSeeder {
    private static readonly TenantSeed[] DefaultTenants =
    {
        new("t-001", "Continuo Central", "default", "central", "contact@continuo.local", "+905551111111", "Default tenant for internal testing and onboarding")
    };

    private static readonly SeedRole[] PlatformRoles =
    {
        // PlatformOwner = super-admin: TÜM permission'lara sahip olmali (Platform +
        // Tenant scope hepsi). Niye scope filter kaldirildi: order.admin.mark-paid
        // gibi tenant scope endpoint'ler PlatformOwner'in 403 almasina sebep
        // oluyordu. PlatformOwner zaten platform-wide super-admin rolu; tenant
        // operasyonlarini da gormeli (debug, support, ops bypass).
        new("PlatformOwner", "Full platform + tenant control (super-admin)", RoleScope.Platform,
            PermissionCatalog.All.Select(p => p.Key).ToArray()),
        new("PlatformAdmin", "Tenant management and global operations", RoleScope.Platform,
            new[]
            {
                "platform.tenants.manage",
                "platform.parameters.manage",
                "platform.security.manage",
                "platform.logs.view",
                "platform.auth.users.view",
                "platform.auth.users.manage",
                "platform.auth.screens.manage",
                "platform.auth.roles.manage",
                // tc-ops-ui MaestroTenantManagementPanel: configure per-tenant
                // Maestro quota, persona and allowed models.
                "ops.maestro.tenants.manage",
                // tc-ops-ui Platform Agreements admin panel.
                "platform.agreements.manage"
            }),
        new("PlatformSupport", "Support and logging access", RoleScope.Platform,
            new[] { "platform.logs.view", "platform.support.impersonate", "platform.auth.users.view" }),
        new("PlatformDev", "Developer tools and TCC maintenance", RoleScope.Platform,
            new[] { "platform.parameters.manage", "platform.tcc.manage", "platform.auth.users.view",
                    // Umbrella + granular (backward compat — existing PlatformDev users retain full infra power)
                    "platform.infra.manage", "platform.infra.logs.view",
                    "platform.infra.view", "platform.infra.files.read", "platform.infra.files.write",
                    "platform.infra.files.delete", "platform.infra.cleanup.execute",
                    "platform.infra.playbook.run", "platform.infra.vm.control",
                    "platform.infra.bootstrap",
                    // Maestro AI — devs are the primary audience for the chat + context + playbook authoring screen.
                    // Unlimited marker so the wallet check is bypassed (internal staff never see wallet-empty).
                    "platform.maestro.use", "platform.maestro.context.author", "platform.maestro.playbook.author",
                    "platform.maestro.unlimited",
                    // Metronome — devs wire/edit/disable platform scheduled jobs.
                    "platform.metronome.manage",
                    // Tempo — devs author/edit platform workflows (multi-step orchestrations).
                    "platform.tempo.manage" }),
        new("InfraReader", "Read-only infrastructure access", RoleScope.Platform,
            new[] { "platform.infra.view", "platform.infra.logs.view", "platform.infra.files.read" }),
        new("InfraOperator", "Standard infrastructure operator — cleanup, playbooks, file upload, VM control",
            RoleScope.Platform,
            new[] { "platform.infra.view", "platform.infra.logs.view",
                    "platform.infra.files.read", "platform.infra.files.write",
                    "platform.infra.cleanup.execute", "platform.infra.playbook.run",
                    "platform.infra.vm.control" }),
        new("InfraAdmin", "Infrastructure admin — includes file delete and bootstrap", RoleScope.Platform,
            new[] { "platform.infra.manage", "platform.infra.logs.view",
                    "platform.infra.view", "platform.infra.files.read", "platform.infra.files.write",
                    "platform.infra.files.delete", "platform.infra.cleanup.execute",
                    "platform.infra.playbook.run", "platform.infra.vm.control",
                    "platform.infra.bootstrap",
                    // Maestro AI unlimited — InfraAdmin runs Maestro for incident response, never billed.
                    "platform.maestro.use", "platform.maestro.unlimited" }),
        // Analyst role — read-heavy persona that triages production data via
        // Maestro chat queries. Carries the Unlimited marker so quota never
        // gates an incident drill-down.
        new("Analyst", "Data analyst / business support — unlimited Maestro", RoleScope.Platform,
            new[] { "platform.logs.view", "platform.auth.users.view",
                    "platform.maestro.use", "platform.maestro.context.author",
                    "platform.maestro.unlimited" })
    };

    private static readonly SeedRole[] TenantRoles =
    {
        new("TenantOwner", "Ownership privileges for tenant", RoleScope.Tenant,
            PermissionCatalog.All.Where(p => p.Scope == RoleScope.Tenant).Select(p => p.Key).ToArray()),
        new("TenantAdmin", "Administrators inside tenant", RoleScope.Tenant,
            new[] {
                "tenant.parameters.manage",
                "tenant.security.manage",
                "tenant.layout.manage", "tenant.robots.manage", "tenant.tcc.manage", "tenant.branch.manage",
                "tenant.inventory.view", "tenant.inventory.manage", "tenant.menu.view", "tenant.menu.manage",
                "tenant.personnel.view", "tenant.personnel.manage",
                "tenant.personnel.shifts.view", "tenant.personnel.shifts.manage",
                "tenant.personnel.payroll.view", "tenant.personnel.payroll.manage",
                "tenant.personnel.advances.view", "tenant.personnel.advances.manage",
                "tenant.personnel.self.view",
                "tenant.orders.view", "tenant.orders.manage", "tenant.pos.operate",
                "tenant.users.view", "tenant.users.manage", "tenant.recipes.view", "tenant.recipes.manage",
                "tenant.tables.view", "tenant.tables.manage", "tenant.reports.view",
                "tenant.customers.view", "tenant.customers.manage",
                "tenant.notifications.templates.view", "tenant.notifications.templates.manage",
                "tenant.notifications.templates.publish", "tenant.notifications.templates.media.manage",
                "tenant.notifications.view", "tenant.notifications.manage",
                "tenant.notifications.dispatch", "tenant.notifications.retry",
                "tenant.notifications.channels.manage",
                "tenant.vision.edge.view", "tenant.vision.edge.manage",
                // Maestro AI — tenant admins can use the assistant + author contexts/playbooks
                // for their own tenant. Quota enforced server-side by mae.MaestroTenantPolicy.
                "tenant.maestro.use", "tenant.maestro.context.author", "tenant.maestro.playbook.author",
                // Billing — TenantAdmin sees the AI Token Cüzdanı + can purchase. Owner role
                // grants every tenant permission via PermissionCatalog.All so it gets this too.
                "tenant.settings.billing.manage",
                // Metronome — TenantAdmin defines tenant-scope scheduled jobs (EOD reports, etc).
                "tenant.metronome.manage",
                // Tempo — TenantAdmin authors workflows + can start them.
                "tenant.tempo.manage", "tenant.tempo.start"
            }),
        new("OperationManager", "Operations lead", RoleScope.Tenant,
            new[] {
                "tenant.parameters.manage",
                "tenant.layout.manage", "tenant.robots.manage", "tenant.reports.view",
                "tenant.inventory.view", "tenant.inventory.manage",
                "tenant.personnel.view", "tenant.personnel.manage",
                "tenant.personnel.shifts.view", "tenant.personnel.shifts.manage",
                "tenant.menu.view", "tenant.menu.manage",
                "tenant.orders.view", "tenant.orders.manage",
                "tenant.tables.view", "tenant.tables.manage",
                "tenant.pos.operate",
                "tenant.customers.view",
                "tenant.notifications.templates.view", "tenant.notifications.templates.manage",
                "tenant.notifications.templates.publish", "tenant.notifications.templates.media.manage",
                "tenant.notifications.view", "tenant.notifications.manage",
                "tenant.notifications.dispatch", "tenant.notifications.retry",
                "tenant.notifications.channels.manage",
                "tenant.vision.edge.view", "tenant.vision.edge.manage",
                // Tempo — OperationManager can START approved workflows but
                // not author new ones (TenantAdmin owns authoring).
                "tenant.tempo.start"
            }),
        new("CashOperator", "Cash register and cashier oversight", RoleScope.Tenant,
            new[] { "tenant.cash.manage", "tenant.pos.operate", "tenant.orders.view" }),
        new("AccountingManager", "Financial and accounting control", RoleScope.Tenant,
            new[] { "tenant.parameters.manage", "tenant.accounting.manage", "tenant.reports.view", "tenant.inventory.view",
                "tenant.personnel.payroll.view", "tenant.personnel.payroll.manage",
                "tenant.personnel.advances.view", "tenant.personnel.advances.manage" }),
        new("BranchAdmin", "Branch/dealer supervisors", RoleScope.Tenant,
           new[] {
                "tenant.branch.manage",
                "tenant.reports.view",
                "tenant.users.view",
                "tenant.notifications.templates.view",
                "tenant.notifications.view",
                "tenant.vision.edge.view",
                "tenant.vision.edge.manage"
                }),

        new("ReadOnlyStaff", "View-only staff", RoleScope.Tenant,
            new[] { "tenant.reports.view", "tenant.orders.view", "tenant.inventory.view", "tenant.menu.view", "tenant.vision.edge.view" })
    };

    private static readonly SeedScreen[] Screens =
    {
        new("console-admin", "/notifications", "Notifications", "Template tabanli bildirim gonderimi ve izleme", new[] { "tenant.notifications.view" }, "/notifications", "bell", "Core", 87),
        // tc-ops-ui – temel paneller
        new("tc-ops-ui", "overview", "Ops Overview", "Operasyon genel bakış ve KPI'lar", Array.Empty<string>(), "/operations", "layout-dashboard", "Core", 10),
        new("tc-ops-ui", "barista", "Barista", "Barista kontrol paneli", Array.Empty<string>(), "/operations", "shopping-bag", "Core", 20),
        new("tc-ops-ui", "transporter", "Transporter", "Transporter kuyruk görüntüleme", Array.Empty<string>(), "/operations", "truck", "Core", 30),
        new("tc-ops-ui", "robot-sim", "Robot Sim", "Robot simülasyon paneli", new[] { "tenant.robots.manage" }, "/operations", "bot", "Core", 40),
        new("tc-ops-ui", "courier", "Courier", "Kurye görevleri ve harita", Array.Empty<string>(), "/operations", "shopping-cart", "Core", 50),
        new("tc-ops-ui", "notifications", "Notifications", "Bildirim listesi", Array.Empty<string>(), "/operations", "bell", "Core", 60),

        // tc-ops-ui – modüller
        new("tc-ops-ui", "ml", "ML Tanımları", "Model envanteri ve training tetikleme", new[] { "ops.ml.configure" }, "/operations", "flask", "Modules", 110),
        new("tc-ops-ui", "parameters", "Parametre Yönetimi", "Platform parametreleri ve sürüm yönetimi", new[] { "ops.parameters.write" }, "/operations", "settings", "Modules", 120),
        new("tc-ops-ui", "public-web", "Public Web API", "Metinler ve kampanya kopyaları", new[] { "ops.public-web.manage" }, "/operations", "globe", "Modules", 130),
        new("tc-ops-ui", "users", "Platform Kullanıcıları", "Platform kullanıcılarını ve credential'larını yönet", new[] { "platform.auth.users.manage" }, "/operations", "users", "Modules", 140),
        new("tc-ops-ui", "roles", "Platform Roller & Ekranlar", "Platform rol/screen yetkilerini yönet", new[] { "platform.auth.roles.manage" }, "/operations", "panels", "Modules", 150),
        new("tc-ops-ui", "ops-roles", "Operasyon Roller", "Rol ve credential eşleşmeleri", new[] { "ops.roles.manage" }, "/operations", "shield", "Modules", 160),
        new("tc-ops-ui", "tenant-users", "Tenant Kullanıcıları", "Tenant kullanıcıları için credential ve rol atama", new[] { "platform.auth.users.manage" }, "/operations", "users", "Modules", 170),
        new("tc-ops-ui", "tenant-roles", "Tenant Roller & Ekranlar", "Tenant rollerini ve console-admin ekran yetkilerini yönet", new[] { "platform.auth.roles.manage" }, "/operations", "panels", "Modules", 180),
        new("tc-ops-ui", "layouts", "Tenant Layouts", "Kiosk/table layoutları ve tema ayarları", new[] { "ops.layouts.manage" }, "/operations", "layout-template", "Modules", 190),
        new("tc-ops-ui", "robot-tasks", "Robot Tasks", "Robot task taslakları ve robot atamaları", new[] { "ops.layouts.manage" }, "/operations", "bot", "Modules", 195),
        new("tc-ops-ui", "analytics", "Tenant Analitikleri", "Satış ve başarı oranı raporları", new[] { "ops.analytics.view" }, "/operations", "activity", "Modules", 200),
        new("tc-ops-ui", "screen-admin", "Ekran Yetkilendirme", "Ekran erişimlerini yönet", new[] { "platform.auth.screens.manage" }, "/operations", "shield", "Modules", 210),
        new("tc-ops-ui", "security", "Security Tanımları", "Gizli bilgi tanımları (credential, connection string, key)", new[] { "platform.security.manage" }, "/operations", "key-round", "Modules", 220),
        new("tc-ops-ui", "docs-tracking", "Doküman İzleme", "Tenant ve platform dökümanlarını izleme", new[] { "ops.docs.view" }, "/operations", "file-text", "Modules", 225),

        // tc-ops-ui – tenant paket+modül entitlement (Phase 1-6, plan: TENANT_PACKAGES_AND_MODULES_PLAN.md)
        new("tc-ops-ui", "tenant-modules", "Modül Kataloğu", "Platform modül tanımları, kategori, bağımlılık ve add-on fiyatları",
            new[] { "ops.catalog.manage" }, "/operations", "tags", "Catalog", 230),
        new("tc-ops-ui", "tenant-packages", "Paket Kataloğu", "Satış paketleri — tier, dahil modüller, multi-currency fiyat",
            new[] { "ops.catalog.manage" }, "/operations", "tags", "Catalog", 240),
        new("tc-ops-ui", "plan-discounts", "İskonto & Kampanyalar", "Tenant-bazlı veya global indirim/kupon yönetimi",
            new[] { "ops.catalog.manage" }, "/operations", "tags", "Catalog", 250),
        new("tc-ops-ui", "tenant-entitlements", "Tenant Paket & Modül Atamaları", "Her tenantta hangi paket aktif, hangi modüller etkin, hangi iskontolar uygulanıyor",
            new[] { "ops.catalog.manage" }, "/operations", "tags", "Catalog", 260),
        new("tc-ops-ui", "tenant-create", "Yeni Tenant Sihirbazı", "Tek noktada tenant bootstrap akışı — kimlik, paket, modüller, fiyat ve ödeme adımlarıyla provision talebi oluşturur",
            new[] { "ops.catalog.manage" }, "/operations", "sparkles", "Catalog", 265),
        new("tc-ops-ui", "provision-requests", "Provision Onayları", "Public signup + ops manuel tenant talepleri, manuel görev checklist'i ve admin onay paneli",
            new[] { "ops.catalog.manage" }, "/operations", "tags", "Catalog", 270),
        new("tc-ops-ui", "billing-invoices", "Faturalar & Tahsilat", "Platform abonelik faturaları, ödeme işaretleme, IBAN EFT mutabakat",
            new[] { "ops.billing.manage" }, "/operations", "tags", "Catalog", 280),
        new("tc-ops-ui", "payment-provider-settings", "Ödeme Entegrasyonları", "Platform ve tenant başına IBAN / Iyzico ödeme sağlayıcı ayarları. Sırlar security-api'de.",
            new[] { "ops.billing.manage" }, "/operations", "wallet", "Catalog", 285),
        new("tc-ops-ui", "platform-ops", "Platform Ops Paneli", "Tenant filo watchlist, MRR, modül adopsiyon, risk + onay durumu (cross-tenant)",
            new[] { "ops.dashboard.platform.view" }, "/operations", "layout-dashboard", "Platform", 290),
        new("tc-ops-ui", "/operations/agreements", "Sözleşmeler", "KVKK, Kullanım Koşulları ve Pazarlama İzni metinlerini yönet ve versiyonla",
            new[] { "platform.agreements.manage" }, "/operations/agreements", "file-text", "Platform", 295),

        // console-admin – tenant admin ekranları
        new("console-admin", "/", "Dashboard", "Tenant genel görünümü", Array.Empty<string>(), "/", "home", "Core", 10),
        new("console-admin", "/dashboard/kds", "KDS Dashboard", "Mutfak gösterge paneli ve sipariş takibi", new[] { "tenant.orders.manage" }, "/dashboard/kds", "desktop", "Dashboards", 11),
        new("console-admin", "/dashboard/accounting", "Accounting Dashboard", "Finansal analiz ve raporlar", new[] { "tenant.accounting.manage" }, "/dashboard/accounting", "bar-chart", "Dashboards", 12),
        new("console-admin", "/dashboard/accounting/daily", "Günlük Muhasebe (Şube)", "Şube bazlı günlük muhasebe icmali", new[] { "tenant.accounting.manage" }, "/dashboard/accounting/daily", "calendar", "Dashboards", 12),
        new("console-admin", "/dashboard/accounting/z-reports", "Z-Raporları", "Gün sonu Z-raporları", new[] { "tenant.accounting.manage" }, "/dashboard/accounting/z-reports", "file-text", "Dashboards", 12),
        new("console-admin", "/dashboard/manager", "Manager Dashboard", "Kapsamlı operasyonel metrikler", new[] { "tenant.reports.view" }, "/dashboard/manager", "dashboard", "Dashboards", 13),
        new("console-admin", "/pos", "POS", "Kasiyer ve satış ekranı", Array.Empty<string>(), "/pos", "shop", "Core", 20),
        new("console-admin", "/orders-board", "Orders Dashboard", "Anlik ve gunluk siparis akis panosu", Array.Empty<string>(), "/orders-board", "desktop", "Core", 30),
        new("console-admin", "/orders", "Order Management", "Siparis detaylari, filtreleme ve yonetim ekrani", Array.Empty<string>(), "/orders", "shopping-cart", "Core", 40),
        new("console-admin", "/inventory", "Inventory", "Stok yönetimi", Array.Empty<string>(), "/inventory", "database", "Core", 50),
        new("console-admin", "/distributors", "Distributors", "Inventory distributor ve stok bildirim alicilari", new[] { "tenant.inventory.view" }, "/distributors", "truck", "Core", 55),
        new("console-admin", "/campaigns", "Campaigns", "Kampanya yönetimi", Array.Empty<string>(), "/campaigns", "gift", "Core", 60),
        new("console-admin", "/devices", "Devices", "Cihaz yönetimi", Array.Empty<string>(), "/devices", "hdd", "Core", 70),
        new("console-admin", "/customers", "Customers", "Müşteri yönetimi", Array.Empty<string>(), "/customers", "team", "Core", 80),
        new("console-admin", "/personnel", "Personnel", "Personel kayıt, vardiya, maaş ve avans yönetimi", new[] { "tenant.personnel.view" }, "/personnel", "team", "Core", 82),
        new("console-admin", "/templates", "Templates", "İçerik ve bildirim şablon yönetimi", new[] { "tenant.notifications.templates.view" }, "/templates", "copy", "Core", 85),
        new("console-admin", "/menu", "Menu", "Menü ve ürün yönetimi", Array.Empty<string>(), "/menu", "menu", "Core", 90),
        new("console-admin", "/menu/tax-settings", "Menü Vergi Ayarları", "Kategori/ürün KDV oran yönetimi", new[] { "tenant.accounting.manage" }, "/menu/tax-settings", "calculator", "Core", 91),
        new("console-admin", "/users", "Users & Roles", "Kullanıcı ve rol yönetimi", Array.Empty<string>(), "/users", "user", "Core", 100),
        new("console-admin", "/analytics", "Analytics", "Analitik ve raporlar", Array.Empty<string>(), "/analytics", "bar-chart", "Core", 110),
        // ScreenKey stays "/fatura" so the upsert (keyed on AppCode::ScreenKey) repoints the
        // existing nav row instead of orphaning it; the live Path now targets the rich
        // E-Fatura İzleme screen (/invoices). The standalone /fatura issue page was retired —
        // issuing now lives inside /invoices as the "Fatura Kes" action.
        new("console-admin", "/fatura", "Invoices", "Fatura izleme ve kesme", new[] { "tenant.accounting.manage" }, "/invoices", "file-text", "Core", 120),
        new("console-admin", "/reservations", "Reservations", "Rezervasyon yönetimi", Array.Empty<string>(), "/reservations", "calendar", "Core", 130),
        new("console-admin", "/network", "Network", "Ağ ve bağlantı ayarları", Array.Empty<string>(), "/network", "cloud", "Core", 140),
        new("console-admin", "/robots", "Robots", "Robot yönetimi", Array.Empty<string>(), "/robots", "robot", "Core", 150),
        new("console-admin", "/aggregator", "Aggregator", "Aggregator entegrasyonları", Array.Empty<string>(), "/aggregator", "share", "Core", 160),
        new("console-admin", "/kds", "KDS", "Kitchen display sistemi", Array.Empty<string>(), "/kds", "desktop", "Core", 170),
        new("console-admin", "/tables", "Tables", "Masa yönetimi", new[] { "tenant.tables.view" }, "/tables", "table", "Core", 180),
        new("console-admin", "/recipes", "Recipes", "Reçete yönetimi", Array.Empty<string>(), "/recipes", "book", "Core", 190),
        new("console-admin", "/branches", "Branches", "Şube yönetimi", Array.Empty<string>(), "/branches", "bank", "Core", 200),
        new("console-admin", "/workstations", "Workstations", "Çalışma istasyonları", Array.Empty<string>(), "/workstations", "build", "Core", 210),
        new("console-admin", "/logs", "Logs", "Log inceleme", Array.Empty<string>(), "/logs", "file-search", "Core", 220),
        new("console-admin", "/settings", "Settings", "Ayarlar ve yapılandırma", Array.Empty<string>(), "/settings", "setting", "Core", 230),
        new("console-admin", "/vision-edge", "Vision / Edge", "Kamera izleme ve edge bağlantı ayarları", new[] { "tenant.vision.edge.view" }, "/vision-edge", "cloud", "Core", 232),
        new("console-admin", "/settings/secrets", "Tenant Gizli Anahtarları", "Tenant kapsamındaki secret/key yönetimi", new[] { "tenant.security.manage" }, "/settings/secrets", "key", "Core", 235),
        new("console-admin", "/settings/scheduled-jobs", "Zamanlanmış Görevler", "Cron + tek seferlik scheduled jobs (Metronome)", new[] { "tenant.metronome.manage" }, "/settings/scheduled-jobs", "clock-circle", "Core", 236),

        // dev-support-console – geliştirici/destek ekranları
        new("dev-support-console", "/", "Landing", "Giriş ve persona seçimi", Array.Empty<string>()),
        new("dev-support-console", "/workspaces/dev", "Dev Workspace", "CI/CD, log ve test panosu", Array.Empty<string>()),
        new("dev-support-console", "/workspaces/dev/infra", "Infra Control", "Sunucu sağlık, playbook tetikleme, VM yönetimi, servis izleme, backup", new[] { "platform.infra.manage", "platform.infra.logs.view", "platform.infra.view" }, "/workspaces/dev/infra", "server", "Developer", 240),
        new("dev-support-console", "/workspaces/dev/maestro", "Maestro AI", "Kullanıcının kendi LLM API key'leri ile chat, kod analizi ve task tetikleme.", new[] { "platform.maestro.use", "platform.maestro.context.author", "platform.maestro.playbook.author" }, "/workspaces/dev/maestro", "sparkles", "Developer", 250),
        new("dev-support-console", "/workspaces/profile", "Profil & AI Agents", "Kullanıcı profili ve LLM provider API key yönetimi.", Array.Empty<string>(), "/workspaces/profile", "user", "Developer", 260),
        new("dev-support-console", "/workspaces/support", "Support Workspace", "Olay yönetimi ve vardiya planlama", Array.Empty<string>())
    };

    public static async Task SeedAsync(AuthDbContext db) {
        await db.Database.EnsureCreatedAsync();

        await EnsureTenantsAsync(db);
        await EnsurePermissionsAsync(db);
        await EnsureRolesAsync(db);
        await EnsurePlatformUserAsync(db);
        await EnsureTenantOwnerAsync(db);
        await EnsureScreensAsync(db);
    }

    private static async Task EnsureTenantsAsync(AuthDbContext db) {
        foreach (var tenantSeed in DefaultTenants) {
            var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Code == tenantSeed.Code);
            if (tenant == null) {
                tenant = new Tenant {
                    Code = tenantSeed.Code,
                    Name = tenantSeed.Name,
                    Slug = tenantSeed.Slug,
                    Subdomain = tenantSeed.Subdomain,
                    ContactEmail = tenantSeed.Email,
                    ContactPhone = tenantSeed.Phone,
                    Notes = tenantSeed.Notes
                };
                db.Tenants.Add(tenant);
            }
            else {
                tenant.Name = tenantSeed.Name;
                tenant.Slug = tenantSeed.Slug;
                tenant.Subdomain = tenantSeed.Subdomain;
                tenant.ContactEmail ??= tenantSeed.Email;
                tenant.ContactPhone ??= tenantSeed.Phone;
                tenant.Notes ??= tenantSeed.Notes;
            }
        }

        await db.SaveChangesAsync();
    }

    private static async Task EnsurePermissionsAsync(AuthDbContext db) {
        var existingKeys = await db.Permissions.Select(p => p.Key).ToListAsync();
        var missing = PermissionCatalog.All.Where(p => !existingKeys.Contains(p.Key, StringComparer.OrdinalIgnoreCase)).ToList();
        if (missing.Count == 0) {
            return;
        }

        foreach (var perm in missing) {
            db.Permissions.Add(new Permission {
                Key = perm.Key,
                DisplayName = perm.DisplayName,
                Description = perm.Description,
                Scope = perm.Scope
            });
        }
        await db.SaveChangesAsync();
    }

    private static async Task EnsureRolesAsync(AuthDbContext db) {
        foreach (var roleSeed in PlatformRoles.Concat(TenantRoles)) {
            var role = await db.Roles.Include(r => r.Permissions).FirstOrDefaultAsync(r => r.Name == roleSeed.Name);
            if (role == null) {
                role = new Role {
                    Name = roleSeed.Name,
                    Description = roleSeed.Description,
                    Scope = roleSeed.Scope,
                    IsSystem = true
                };
                db.Roles.Add(role);
            }
            else {
                role.Description = roleSeed.Description;
                role.Scope = roleSeed.Scope;
            }

            var desired = roleSeed.PermissionKeys
                .Select(k => k.Trim())
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var existing = role.Permissions.Select(p => p.PermissionKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var key in desired) {
                if (!existing.Contains(key)) {
                    role.Permissions.Add(new RolePermission {
                        Role = role,
                        PermissionKey = key
                    });
                }
            }
        }

        await db.SaveChangesAsync();
    }

    private static async Task EnsurePlatformUserAsync(AuthDbContext db) {
        var ownerEmail =
            Environment.GetEnvironmentVariable("AUTH_OWNER_LOGIN")
            ?? Environment.GetEnvironmentVariable("AUTH__OWNER__LOGIN")
            ?? "platform.owner@example.local";
        var ownerPassword =
            Environment.GetEnvironmentVariable("AUTH_OWNER_PASSWORD")
            ?? Environment.GetEnvironmentVariable("AUTH__OWNER__PASSWORD")
            ?? Environment.GetEnvironmentVariable("ADMIN_PASSWORD")
            ?? "Continuo!123";

        var role = await db.Roles.FirstAsync(r => r.Name == "PlatformOwner");
        var user = await db.PlatformUsers.Include(u => u.Roles).FirstOrDefaultAsync(u => u.Email == ownerEmail);
        if (user == null) {
            user = new PlatformUser {
                Email = ownerEmail,
                DisplayName = "Continuo Platform Owner"
            };
            var credential = new Credential {
                Login = ownerEmail,
                Email = ownerEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(ownerPassword),
                OwnerType = CredentialOwnerType.PlatformUser,
                PlatformUser = user,
                MustChangePassword = true
            };
            user.Credentials.Add(credential);
            user.Roles.Add(new UserRole {
                Role = role,
                PlatformUser = user
            });
            db.PlatformUsers.Add(user);
            db.Credentials.Add(credential);
        }
        else {
            var cred = await db.Credentials.FirstOrDefaultAsync(c => c.Login == ownerEmail);
            if (cred == null) {
                cred = new Credential {
                    Login = ownerEmail,
                    Email = ownerEmail,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(ownerPassword),
                    OwnerType = CredentialOwnerType.PlatformUser,
                    PlatformUser = user,
                    MustChangePassword = true
                };
                user.Credentials.Add(cred);
                db.Credentials.Add(cred);
            }
            else {
                // Opt-in force reset: dev/staging'de owner şifresi UI'dan değişmiş
                // olabilir, smoke test'lerde takılmamak için env flag ile sıfırlanır.
                // Env: AUTH_OWNER_FORCE_RESET=true → her startup'ta env'deki şifreye
                // yeniden hashlenir + must-change-password=false (test ergonomisi).
                // Production'da bu flag SETLENMEMELİ.
                var forceReset = (Environment.GetEnvironmentVariable("AUTH_OWNER_FORCE_RESET") ?? string.Empty)
                    .Equals("true", StringComparison.OrdinalIgnoreCase);
                if (forceReset) {
                    cred.PasswordHash = BCrypt.Net.BCrypt.HashPassword(ownerPassword);
                    cred.MustChangePassword = false;
                }
            }

            var hasRole = user.Roles.Any(r => r.RoleId == role.Id);
            if (!hasRole) {
                user.Roles.Add(new UserRole {
                    Role = role,
                    PlatformUser = user
                });
            }
        }

        await db.SaveChangesAsync();
    }

    private static async Task EnsureTenantOwnerAsync(AuthDbContext db) {
        var tenant = await db.Tenants.FirstAsync(t => t.Code == DefaultTenants[0].Code);
        var role = await db.Roles.FirstAsync(r => r.Name == "TenantOwner");
        const string ownerLogin = "owner@continuo.local";
        const string ownerPassword = "Continuo!123";

        var user = await db.TenantUsers.Include(u => u.Roles).FirstOrDefaultAsync(u => u.Email == ownerLogin);
        if (user == null) {
            user = new TenantUser {
                Tenant = tenant,
                DisplayName = "Tenant Owner",
                Email = ownerLogin,
                MarketingOptIn = true,
                PositionTitle = "Owner"
            };
            var credential = new Credential {
                Login = ownerLogin,
                Email = ownerLogin,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(ownerPassword),
                OwnerType = CredentialOwnerType.TenantUser,
                TenantUser = user
            };
            user.Credentials.Add(credential);
            user.Roles.Add(new UserRole {
                Role = role,
                TenantUser = user
            });
            db.TenantUsers.Add(user);
            db.Credentials.Add(credential);
        }
        else {
            if (!user.Roles.Any(r => r.RoleId == role.Id)) {
                user.Roles.Add(new UserRole {
                    Role = role,
                    TenantUser = user
                });
            }
        }

        await db.SaveChangesAsync();
    }

    private static async Task EnsureScreensAsync(AuthDbContext db) {
        var existing = await db.Screens.ToListAsync();
        var lookup = existing.ToDictionary(
            s => $"{s.AppCode}::{s.ScreenKey}".ToLowerInvariant(),
            s => s,
            StringComparer.OrdinalIgnoreCase
        );
        foreach (var seed in Screens) {
            var key = $"{seed.AppCode}::{seed.ScreenKey}".ToLowerInvariant();
            lookup.TryGetValue(key, out var screen);

            var requiredJson = System.Text.Json.JsonSerializer.Serialize(seed.RequiredPermissions ?? Array.Empty<string>());

            if (screen == null) {
                screen = new Screen {
                    AppCode = seed.AppCode,
                    ScreenKey = seed.ScreenKey,
                    Title = seed.Title,
                    Description = seed.Description,
                    RequiredPermissionsJson = requiredJson,
                    Path = seed.Path,
                    Icon = seed.Icon,
                    Group = seed.Group,
                    SortOrder = seed.SortOrder,
                    IsSystem = seed.IsSystem
                };
                db.Screens.Add(screen);
                lookup[key] = screen;
            }
            else {
                screen.Title = seed.Title;
                screen.Description = seed.Description;
                screen.RequiredPermissionsJson = requiredJson;
                screen.Path = seed.Path;
                screen.Icon = seed.Icon;
                screen.Group = seed.Group;
                screen.SortOrder = seed.SortOrder;
                screen.IsSystem = seed.IsSystem;
            }
        }

        await db.SaveChangesAsync();
    }
}
