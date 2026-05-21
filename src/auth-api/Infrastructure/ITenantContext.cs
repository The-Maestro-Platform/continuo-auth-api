using AuthApi.Models;

namespace AuthApi.Infrastructure;

public interface ITenantContext {
    Ulid? TenantId { get; }
    string? TenantCode { get; }
    string? BranchCode { get; }
    TenantStatus? Status { get; }
    bool HasTenant { get; }
    Task EnsureResolvedAsync(HttpContext context, CancellationToken ct);
}
