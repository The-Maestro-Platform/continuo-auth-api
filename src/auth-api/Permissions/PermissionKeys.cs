namespace AuthApi.Permissions;

public static class PermissionKeys {
    public static class Platform {
        public const string TenantsManage = "platform.tenants.manage";
        public const string ParametersManage = "platform.parameters.manage";
        public const string SecurityManage = "platform.security.manage";
        public const string SecurityReveal = "platform.security.reveal";
        public const string LogsView = "platform.logs.view";
        public const string SupportImpersonate = "platform.support.impersonate";
        public const string SupportSeleniumRun = "platform.support.selenium.run";
        public const string AuthScreensManage = "platform.auth.screens.manage";
        public const string AuthUsersView = "platform.auth.users.view";
        public const string AuthUsersManage = "platform.auth.users.manage";
        public const string AuthRolesManage = "platform.auth.roles.manage";
        public const string InfraManage = "platform.infra.manage";
        public const string InfraLogsView = "platform.infra.logs.view";
        public const string InfraView = "platform.infra.view";
        public const string InfraFilesRead = "platform.infra.files.read";
        public const string InfraFilesWrite = "platform.infra.files.write";
        public const string InfraFilesDelete = "platform.infra.files.delete";
        public const string InfraCleanupExecute = "platform.infra.cleanup.execute";
        public const string InfraPlaybookRun = "platform.infra.playbook.run";
        public const string InfraVmControl = "platform.infra.vm.control";
        public const string InfraBootstrap = "platform.infra.bootstrap";

        /// <summary>Manage platform-scope Metronome scheduled jobs (disk
        /// cleanup, billing rollup, ML retrain). continuo-ops-ui gates the
        /// Metronome panel on this permission.</summary>
        public const string MetronomeManage = "platform.metronome.manage";

        /// <summary>Manage platform-scope Tempo workflow definitions +
        /// instances (multi-step orchestrations spanning services).
        /// continuo-ops-ui gates the Tempo panel on this permission.</summary>
        public const string TempoManage = "platform.tempo.manage";

        /// <summary>Manage platform-wide FX margin (`forex.margin.default.pips`)
        /// and read TCMB rate history + tenant audit. console-admin gates the
        /// `/admin/forex` Margin Settings tab on this permission. See
        /// `docs/todo/EXCHANGE_RATE_API_PLAN.md` §2.</summary>
        public const string ForexManage = "platform.forex.manage";

        /// <summary>Trigger TCMB FX refresh (POST /internal/refresh on
        /// exchange-rate-api). Reserved for Tempo workflow + ops admin;
        /// service-internal calls use M2M instead.</summary>
        public const string ForexRefresh = "platform.forex.refresh";

        // Maestro AI Self-Service screen permissions.
        public const string MaestroUse = "platform.maestro.use";
        public const string MaestroContextAuthor = "platform.maestro.context.author";
        public const string MaestroPlaybookAuthor = "platform.maestro.playbook.author";
        /// <summary>
        /// Quota-bypass marker — when carried, maestro-api skips token-wallet
        /// balance checks. Assigned to internal personnel (dev / analyst /
        /// infraAdmin / platform.owner) so the platform team is never blocked
        /// by the same monetisation that gates tenant + customer surfaces.
        /// See <c>docs/todo/MAESTRO_TOKEN_WALLET_PLAN.md</c>.
        /// </summary>
        public const string MaestroUnlimited = "platform.maestro.unlimited";

        /// <summary>Manage platform-level legal agreements (KVKK / Terms of
        /// Use / Marketing Opt-in) shown to customers at signup/login. Gates
        /// the continuo-ops-ui Agreements admin panel and the auth-api admin CRUD
        /// endpoints. Public active-list endpoint is anonymous.</summary>
        public const string AgreementsManage = "platform.agreements.manage";

        // Track 3 — Portal SSO
        public const string PortalAccess = "platform.portal.access";
        public const string PortalEnvDev = "platform.portal.env.dev";
        public const string PortalEnvStaging = "platform.portal.env.staging";
        public const string PortalEnvProd = "platform.portal.env.prod";
    }

    public static class Ops {
        public const string AnalyticsView = "ops.analytics.view";
        public const string LayoutsManage = "ops.layouts.manage";
        public const string MlConfigure = "ops.ml.configure";
        public const string ParametersWrite = "ops.parameters.write";
        public const string PublicWebManage = "ops.public-web.manage";
        public const string RolesManage = "ops.roles.manage";
        // Doküman İzleme (tenant + platform DMS items) — continuo-ops-ui yeni modülü.
        public const string DocsView = "ops.docs.view";

        // Maestro per-tenant management — continuo-ops-ui MaestroTenantManagementPanel.
        // CRUD on tenant policies (quota/personality/allowed models) + role overrides.
        // See docs/todo/MAESTRO_TENANT_MANAGEMENT_PLAN.md.
        public const string MaestroTenantsManage = "ops.maestro.tenants.manage";

        // Tenant catalog (module + package + plan-discount + entitlement + provision requests)
        // — continuo-ops-ui Phase 1-5 paneller. See docs/todo/TENANT_PACKAGES_AND_MODULES_PLAN.md.
        public const string CatalogManage = "ops.catalog.manage";

        // Platform billing (Invoice/PaymentTransaction/BankReconciliation) — continuo-ops-ui
        // InvoicesPanel. Phase 4.5 — same plan.
        public const string BillingManage = "ops.billing.manage";

        // Platform-Ops dashboard cross-tenant fleet view — Phase 6.
        // See docs/todo/PLATFORM_OPS_DASHBOARD_PLAN.md.
        public const string DashboardPlatformView = "ops.dashboard.platform.view";
    }

    public static class Tenant {
        public const string ParametersManage = "tenant.parameters.manage";
        public const string SecurityManage = "tenant.security.manage";
        public const string LayoutManage = "tenant.layout.manage";
        public const string RobotsManage = "tenant.robots.manage";
        public const string ReportsView = "tenant.reports.view";
        public const string CashManage = "tenant.cash.manage";
        public const string AccountingManage = "tenant.accounting.manage";
        public const string BranchManage = "tenant.branch.manage";
        public const string SetupTrack = "tenant.setup.track";

        public const string InventoryView = "tenant.inventory.view";
        public const string InventoryManage = "tenant.inventory.manage";

        public const string PersonnelView = "tenant.personnel.view";
        public const string PersonnelManage = "tenant.personnel.manage";
        public const string PersonnelShiftsView = "tenant.personnel.shifts.view";
        public const string PersonnelShiftsManage = "tenant.personnel.shifts.manage";
        public const string PersonnelPayrollView = "tenant.personnel.payroll.view";
        public const string PersonnelPayrollManage = "tenant.personnel.payroll.manage";
        public const string PersonnelAdvancesView = "tenant.personnel.advances.view";
        public const string PersonnelAdvancesManage = "tenant.personnel.advances.manage";
        public const string PersonnelSelfView = "tenant.personnel.self.view";

        public const string MenuView = "tenant.menu.view";
        public const string MenuManage = "tenant.menu.manage";

        public const string OrdersView = "tenant.orders.view";
        public const string OrdersManage = "tenant.orders.manage";

        public const string PosOperate = "tenant.pos.operate";

        public const string UsersView = "tenant.users.view";
        public const string UsersManage = "tenant.users.manage";

        public const string RecipesView = "tenant.recipes.view";
        public const string RecipesManage = "tenant.recipes.manage";

        public const string CustomersView = "tenant.customers.view";
        public const string CustomersManage = "tenant.customers.manage";
        //content-template-api permissions
        public const string NotificationTemplatesView = "tenant.notifications.templates.view";
        public const string NotificationTemplatesManage = "tenant.notifications.templates.manage";
        public const string NotificationTemplatesPublish = "tenant.notifications.templates.publish";
        public const string NotificationTemplatesMediaManage = "tenant.notifications.templates.media.manage";

        //notification api permissions 
        public const string NotificationsView = "tenant.notifications.view";
        public const string NotificationsManage = "tenant.notifications.manage";
        public const string NotificationsDispatch = "tenant.notifications.dispatch";
        public const string NotificationsRetry = "tenant.notifications.retry";
        public const string NotificationsChannelsManage = "tenant.notifications.channels.manage";


        public const string TablesView = "tenant.tables.view";
        public const string TablesManage = "tenant.tables.manage";
        public const string VisionEdgeView = "tenant.vision.edge.view";
        public const string VisionEdgeManage = "tenant.vision.edge.manage";

        // Maestro AI per-tenant usage. `MaestroUse` controls the floating-button
        // visibility + chat access; quota/personality enforced server-side by
        // mae.MaestroTenantPolicy (managed via continuo-ops-ui by Ops.MaestroTenantsManage).
        public const string MaestroUse = "tenant.maestro.use";
        public const string MaestroContextAuthor = "tenant.maestro.context.author";
        public const string MaestroPlaybookAuthor = "tenant.maestro.playbook.author";
        /// <summary>
        /// TenantOwner-tier permission for the console-admin Settings → AI
        /// Token Cüzdanı purchase flow. Grants visibility + purchase access
        /// for the per-tenant token wallet; consumption (chat) is gated by
        /// <see cref="MaestroUse"/>.
        /// </summary>
        public const string SettingsBillingManage = "tenant.settings.billing.manage";
        /// <summary>Manage tenant-scope Metronome scheduled jobs (EOD Z report,
        /// nightly summary email, etc.). TenantOwner + TenantAdmin tier.</summary>
        public const string MetronomeManage = "tenant.metronome.manage";

        /// <summary>Author tenant-scope Tempo workflows — multi-step business
        /// orchestrations (sipariş checkout, EOD pipeline, vs.). TenantOwner +
        /// TenantAdmin tier; OperationManager only gets the START variant.</summary>
        public const string TempoManage = "tenant.tempo.manage";

        /// <summary>Trigger an existing tenant-scope Tempo workflow manually
        /// (without authoring rights). Lets a manager kick off pre-approved
        /// workflows from console-admin / Maestro chat.</summary>
        public const string TempoStart = "tenant.tempo.start";
    }

    /// <summary>
    /// Customer-tier permission keys — assigned automatically to every
    /// <c>CredentialOwnerType.Customer</c> credential at JWT issuance time so
    /// authenticated qrmenu guests can chat with Maestro and buy tokens
    /// without role-management overhead.
    /// </summary>
    public static class Customer {
        public const string MaestroUse = "customer.maestro.use";
        public const string MaestroBillingManage = "customer.maestro.billing.manage";
    }
}
