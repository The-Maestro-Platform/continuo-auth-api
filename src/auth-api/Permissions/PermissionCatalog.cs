using AuthApi.Models;

namespace AuthApi.Permissions;

public record PermissionDefinition(string Key, string DisplayName, string Description, RoleScope Scope);

public static class PermissionCatalog {
    private static readonly PermissionDefinition[] PlatformOnly =
    {
        new(PermissionKeys.Platform.TenantsManage, "Manage Tenants", "Create, update or disable any tenant instance.", RoleScope.Platform),
        new(PermissionKeys.Platform.ParametersManage, "Manage Global Parameters", "Adjust system-wide configuration and parameter store.", RoleScope.Platform),
        new(PermissionKeys.Platform.SecurityManage, "Manage Security Vault", "Manage encrypted credentials, connection strings and platform secrets.", RoleScope.Platform),
        new(PermissionKeys.Platform.SecurityReveal, "Reveal Security Secrets", "Reveal plaintext secrets (restricted to platform owner).", RoleScope.Platform),
        new(PermissionKeys.Platform.TccManage, "Manage TCC Operations", "Operate global TCC treasury, rewards and wallet caps.", RoleScope.Platform),
        new(PermissionKeys.Platform.LogsView, "View System Logs", "Inspect logs and audits across tenants.", RoleScope.Platform),
        new(PermissionKeys.Platform.SupportImpersonate, "Impersonate Tenant User", "Temporarily act on behalf of a tenant user for support.", RoleScope.Platform),
        new(PermissionKeys.Platform.SupportSeleniumRun, "Run Selenium Tests", "Queue, run and manage Selenium runner test catalog, scenarios and flows from the support console.", RoleScope.Platform),
        new(PermissionKeys.Platform.AuthScreensManage, "Manage Screen Access", "Create screens and assign screen access to platform users.", RoleScope.Platform),
        new(PermissionKeys.Platform.AuthUsersView, "View Platform Users", "List platform users for assignment and directory views.", RoleScope.Platform),
        new(PermissionKeys.Platform.AuthUsersManage, "Manage Platform Users", "Create platform users, credentials and role assignments.", RoleScope.Platform),
        new(PermissionKeys.Platform.AuthRolesManage, "Manage Platform Roles", "Create platform roles and assign UI screens.", RoleScope.Platform),
        new(PermissionKeys.Platform.InfraManage, "Manage Infrastructure", "Umbrella permission — implies all granular infra permissions (files.delete, bootstrap, vm.control included).", RoleScope.Platform),
        new(PermissionKeys.Platform.InfraLogsView, "View Infrastructure Logs", "View infrastructure playbook output, health probes and backup history (read-only).", RoleScope.Platform),
        new(PermissionKeys.Platform.InfraView, "View Infrastructure", "List servers, view health/metrics, VM list, containers, backup history and playbook runs (read-only).", RoleScope.Platform),
        new(PermissionKeys.Platform.InfraFilesRead, "Browse Infrastructure Files", "Browse the SFTP file allowlist and download files (read-only).", RoleScope.Platform),
        new(PermissionKeys.Platform.InfraFilesWrite, "Upload Infrastructure Files", "Upload files to a server via the SFTP allowlist.", RoleScope.Platform),
        new(PermissionKeys.Platform.InfraFilesDelete, "Delete Infrastructure Files", "Delete files/folders via the SFTP allowlist (destructive — recursive directory delete).", RoleScope.Platform),
        new(PermissionKeys.Platform.InfraCleanupExecute, "Execute Cleanup Actions", "Run cleanup actions (journald vacuum, log truncate, docker prune, custom-path delete).", RoleScope.Platform),
        new(PermissionKeys.Platform.InfraPlaybookRun, "Run Ansible Playbooks", "Trigger Ansible playbook runs from the catalog (check-mode or execute).", RoleScope.Platform),
        new(PermissionKeys.Platform.InfraVmControl, "Control VMs", "Start, shutdown and force-stop VMs on managed hypervisors.", RoleScope.Platform),
        new(PermissionKeys.Platform.InfraBootstrap, "Bootstrap Servers", "Register new servers or update existing ones via the Bootstrap Wizard.", RoleScope.Platform),

        // Maestro AI Self-Service permissions (maestro-console screen)
        new(PermissionKeys.Platform.MaestroUse, "Use Maestro AI", "Open the Maestro chat screen, run sessions and consume LLM credentials configured in Profile.", RoleScope.Platform),
        new(PermissionKeys.Platform.MaestroContextAuthor, "Author Maestro Contexts", "Create and edit reusable Maestro contexts (system prompts + knowledge sources) for personal or team use.", RoleScope.Platform),
        new(PermissionKeys.Platform.MaestroPlaybookAuthor, "Author Maestro Playbooks", "Create, version and activate Maestro playbooks consumed by the task engine.", RoleScope.Platform),

        // continuo-ops-ui (operations) module permissions
        new(PermissionKeys.Ops.AnalyticsView, "View Tenant Analytics", "Access tenant analytics and KPI reporting in Ops UI.", RoleScope.Platform),
        new(PermissionKeys.Ops.LayoutsManage, "Manage Tenant Layouts", "Manage tenant layouts, theming and client app access in Ops UI.", RoleScope.Platform),
        new(PermissionKeys.Ops.MlConfigure, "Configure ML", "Manage ML definitions, languages and translations in Ops UI.", RoleScope.Platform),
        new(PermissionKeys.Ops.ParametersWrite, "Manage Parameters", "Create or update platform parameters in Ops UI.", RoleScope.Platform),
        new(PermissionKeys.Ops.PublicWebManage, "Manage Public Web API Content", "Manage public web content copy and sections in Ops UI.", RoleScope.Platform),
        new(PermissionKeys.Ops.RolesManage, "Manage Ops Roles", "Manage role/screen mappings in Ops UI.", RoleScope.Platform),
        new(PermissionKeys.Ops.DocsView, "View Documents", "Browse tenant and platform documents (DMS) in Ops UI.", RoleScope.Platform)
    };

    private static readonly PermissionDefinition[] TenantOnly =
    {
        new(PermissionKeys.Tenant.ParametersManage, "Manage Tenant Parameters", "Adjust tenant-scoped configuration and parameter store.", RoleScope.Tenant),
        new(PermissionKeys.Tenant.SecurityManage, "Manage Tenant Secrets", "Manage tenant-scoped external API keys and credentials.", RoleScope.Tenant),
        new(PermissionKeys.Tenant.LayoutManage, "Manage Layouts", "Adjust robot or floor layouts per tenant.", RoleScope.Tenant),
        new(PermissionKeys.Tenant.RobotsManage, "Manage Robots", "Enroll, configure and monitor robots for tenant.", RoleScope.Tenant),
        new(PermissionKeys.Tenant.ReportsView, "View Tenant Reports", "Access tenant-specific sales and activity reports.", RoleScope.Tenant),
        new(PermissionKeys.Tenant.CashManage, "Manage Cash Registers", "Reconcile cashiers, settle cashier sessions.", RoleScope.Tenant),
        new(PermissionKeys.Tenant.AccountingManage, "Manage Accounting", "Control accounting workflows and billing reconciliation.", RoleScope.Tenant),
        new(PermissionKeys.Tenant.BranchManage, "Manage Branch Users", "Onboard or update dealer and branch staff.", RoleScope.Tenant),
        new(PermissionKeys.Tenant.SetupTrack, "Track Setup Progress", "Track tenant onboarding and setup checklist progress.", RoleScope.Tenant),
        new(PermissionKeys.Tenant.TccManage, "Manage Tenant TCC", "Operate tenant-scoped TCC programs and wallets.", RoleScope.Tenant),
        // Inventory
        new(PermissionKeys.Tenant.InventoryView, "View Inventory", "View stock levels and inventory items.", RoleScope.Tenant),
        new(PermissionKeys.Tenant.InventoryManage, "Manage Inventory", "Add, update or adjust inventory items.", RoleScope.Tenant),
        // Personnel
        new(PermissionKeys.Tenant.PersonnelView, "View Personnel", "View tenant personnel records.", RoleScope.Tenant),
        new(PermissionKeys.Tenant.PersonnelManage, "Manage Personnel", "Create, update or deactivate tenant personnel records.", RoleScope.Tenant),
        new(PermissionKeys.Tenant.PersonnelShiftsView, "View Personnel Shifts", "View personnel shift assignments.", RoleScope.Tenant),
        new(PermissionKeys.Tenant.PersonnelShiftsManage, "Manage Personnel Shifts", "Create, update or cancel personnel shift assignments.", RoleScope.Tenant),
        new(PermissionKeys.Tenant.PersonnelPayrollView, "View Personnel Payroll", "View personnel salary and payroll records.", RoleScope.Tenant),
        new(PermissionKeys.Tenant.PersonnelPayrollManage, "Manage Personnel Payroll", "Create salary records and update payroll payment status.", RoleScope.Tenant),
        new(PermissionKeys.Tenant.PersonnelAdvancesView, "View Personnel Advances", "View personnel advance records.", RoleScope.Tenant),
        new(PermissionKeys.Tenant.PersonnelAdvancesManage, "Manage Personnel Advances", "Create and track personnel advances.", RoleScope.Tenant),
        new(PermissionKeys.Tenant.PersonnelSelfView, "View Own Personnel Profile", "View the authenticated user's own personnel profile when linked.", RoleScope.Tenant),
        // Menu
        new(PermissionKeys.Tenant.MenuView, "View Menu", "View menu items and categories.", RoleScope.Tenant),
        new(PermissionKeys.Tenant.MenuManage, "Manage Menu", "Create, update or remove menu items.", RoleScope.Tenant),
        // Orders
        new(PermissionKeys.Tenant.OrdersView, "View Orders", "View order history and details.", RoleScope.Tenant),
        new(PermissionKeys.Tenant.OrdersManage, "Manage Orders", "Update order status and process refunds.", RoleScope.Tenant),
        // POS
        new(PermissionKeys.Tenant.PosOperate, "Operate POS", "Use point of sale for transactions.", RoleScope.Tenant),
        // Users (tenant-level)
        new(PermissionKeys.Tenant.UsersView, "View Tenant Users", "View tenant staff and their roles.", RoleScope.Tenant),
        new(PermissionKeys.Tenant.UsersManage, "Manage Tenant Users", "Create or update tenant staff accounts.", RoleScope.Tenant),
        // Recipes
        new(PermissionKeys.Tenant.RecipesView, "View Recipes", "View recipe definitions.", RoleScope.Tenant),
        new(PermissionKeys.Tenant.RecipesManage, "Manage Recipes", "Create or update recipes.", RoleScope.Tenant),
        // Customers
        new(PermissionKeys.Tenant.CustomersView, "View Customers", "View customer list and profiles.", RoleScope.Tenant),
        new(PermissionKeys.Tenant.CustomersManage, "Manage Customers", "Edit customer profiles and manage customer data.", RoleScope.Tenant),
        // Notification / Content Templates
        new(PermissionKeys.Tenant.NotificationTemplatesView, "View Content Templates", "View tenant-scoped content and notification template definitions.", RoleScope.Tenant),
        new(PermissionKeys.Tenant.NotificationTemplatesManage, "Manage Content Templates", "Create and update tenant-scoped content and notification templates.", RoleScope.Tenant),
        new(PermissionKeys.Tenant.NotificationTemplatesPublish, "Publish Content Templates", "Publish or archive tenant-scoped content and notification templates.", RoleScope.Tenant),
        new(PermissionKeys.Tenant.NotificationTemplatesMediaManage, "Manage Template Media", "Upload and manage template media assets such as email images.", RoleScope.Tenant),

        //Notification Dispatch Operations
        // Notification Dispatch Operations
        new(PermissionKeys.Tenant.NotificationsView, "View Notifications", "View tenant-scoped notification definitions, dispatch history and operational status.", RoleScope.Tenant),
        new(PermissionKeys.Tenant.NotificationsManage, "Manage Notifications", "Create and update tenant-scoped notification definitions and recipient policies.", RoleScope.Tenant),
        new(PermissionKeys.Tenant.NotificationsDispatch, "Dispatch Notifications", "Trigger test sends and manual notification dispatch operations.", RoleScope.Tenant),
        new(PermissionKeys.Tenant.NotificationsRetry, "Retry Notifications", "Retry failed notification deliveries and manage requeue operations.", RoleScope.Tenant),
        new(PermissionKeys.Tenant.NotificationsChannelsManage, "Manage Notification Channels", "Manage tenant-scoped notification channel settings and readiness checks.", RoleScope.Tenant),

        // Tables
        new(PermissionKeys.Tenant.TablesView, "View Tables", "View table layout and status.", RoleScope.Tenant),
        new(PermissionKeys.Tenant.TablesManage, "Manage Tables", "Configure table layout and assignments.", RoleScope.Tenant),
        // Vision / Edge
        new(PermissionKeys.Tenant.VisionEdgeView, "View Vision Edge", "View camera panels and occupancy streams for branch edge devices.", RoleScope.Tenant),
        new(PermissionKeys.Tenant.VisionEdgeManage, "Manage Vision Edge", "Configure edge connection, camera list and stream method.", RoleScope.Tenant)
    };

    public static readonly PermissionDefinition[] All = PlatformOnly.Concat(TenantOnly).ToArray();
}
