using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
namespace AuthApi.Infrastructure.Authorization;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public sealed class RequirePermissionAttribute : Attribute, IAsyncAuthorizationFilter {
    private readonly string[] _permissions;

    public RequirePermissionAttribute(params string[] permissions) {
        _permissions = permissions ?? Array.Empty<string>();
    }

    public Task OnAuthorizationAsync(AuthorizationFilterContext context) {
        if (_permissions.Length == 0) {
            return Task.CompletedTask;
        }

        if (context.HttpContext.User?.Identity?.IsAuthenticated != true) {
            context.Result = new UnauthorizedResult();
            return Task.CompletedTask;
        }

        var configuration = context.HttpContext.RequestServices.GetService<IConfiguration>()
            ?? throw new InvalidOperationException("IConfiguration is not available.");

        if (PermissionAuthorization.HasAllPermissions(context.HttpContext, configuration, _permissions)) {
            return Task.CompletedTask;
        }

        context.Result = new ForbidResult();
        return Task.CompletedTask;
    }
    public string[]? Permissions { get; init; }
}
