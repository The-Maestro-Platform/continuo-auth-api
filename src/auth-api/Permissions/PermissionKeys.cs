namespace AuthApi.Permissions;

public static class PermissionKeys {
    public static class Platform {
        public const string TenantsManage = "platform.tenants.manage";
        public const string ParametersManage = "platform.parameters.manage";
        public const string SecurityManage = "platform.security.manage";
        public const string SecurityReveal = "platform.security.reveal";
        public const string TccManage = "platform.tcc.manage";
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

        // Maestro AI Self-Service screen permissions.
        public const string MaestroUse = "platform.maestro.use";
        public const string MaestroContextAuthor = "platform.maestro.context.author";
        public const string MaestroPlaybookAuthor = "platform.maestro.playbook.author";

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
        public const string TccManage = "tenant.tcc.manage";

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
    }
}
