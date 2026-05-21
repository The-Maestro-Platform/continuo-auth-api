using System;
using AuthApi.Models;
using Microsoft.EntityFrameworkCore;

namespace AuthApi.Infrastructure;

public static class LegacyBranchBackfill {
    private const string DefaultBranchConfigKey = "AUTH_DEFAULT_BRANCH_CODE";

    public static async Task ApplyForConsoleAdminAsync(
        AuthDbContext db,
        TenantBranchActorScope actorScope,
        string? tenantContextBranchCode,
        IConfiguration configuration,
        CancellationToken ct) {
        if (!actorScope.RequiresBranchScope || string.IsNullOrWhiteSpace(actorScope.TenantCode)) {
            return;
        }

        var fallbackBranch = ResolveFallbackBranchCode(actorScope, tenantContextBranchCode, configuration);
        if (string.IsNullOrWhiteSpace(fallbackBranch)) {
            return;
        }

        var scopedTenantCode = TenantBranchAuthorization.NormalizeTenantCode(actorScope.TenantCode);
        if (string.IsNullOrWhiteSpace(scopedTenantCode)) {
            return;
        }

        var tenantId = await db.Tenants
            .Where(t =>
                t.Code == scopedTenantCode ||
                t.Slug == scopedTenantCode ||
                t.Subdomain == scopedTenantCode)
            .Select(t => (Ulid?)t.Id)
            .FirstOrDefaultAsync(ct);
        if (!tenantId.HasValue) {
            return;
        }

        var updated = await db.UserRoles
            .Where(ur =>
                ur.TenantUserId != null &&
                ur.BranchCode == null &&
                ur.TenantUser != null &&
                ur.TenantUser.TenantId == tenantId.Value)
            .ToListAsync(ct);

        if (updated.Count == 0) {
            return;
        }

        foreach (var userRole in updated) {
            userRole.BranchCode = fallbackBranch;
        }

        await db.SaveChangesAsync(ct);
    }

    private static string? ResolveFallbackBranchCode(
        TenantBranchActorScope actorScope,
        string? tenantContextBranchCode,
        IConfiguration configuration) {
        var configured = TenantBranchAuthorization.NormalizeBranchCode(configuration[DefaultBranchConfigKey]);
        if (!string.IsNullOrWhiteSpace(configured)) {
            return configured;
        }

        var contextBranch = TenantBranchAuthorization.NormalizeBranchCode(tenantContextBranchCode);
        if (!string.IsNullOrWhiteSpace(contextBranch)) {
            return contextBranch;
        }

        return actorScope.BranchCodes
            .Select(TenantBranchAuthorization.NormalizeBranchCode)
            .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(code => !string.IsNullOrWhiteSpace(code));
    }
}
