using AuthApi.Infrastructure;

namespace AuthApi.Middleware;

public class TenantResolutionMiddleware {
    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next) {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext) {
        if (context is null) {
            throw new ArgumentNullException(nameof(context));
        }

        var cancellationToken = context.RequestAborted;
        await tenantContext.EnsureResolvedAsync(context, cancellationToken);
        await _next(context);
    }
}
