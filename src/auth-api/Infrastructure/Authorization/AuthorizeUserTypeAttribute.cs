using AuthApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AuthApi.Infrastructure.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class AuthorizeUserTypeAttribute : Attribute, IAsyncAuthorizationFilter {
    private readonly UserType[] _allowed;

    public AuthorizeUserTypeAttribute(params UserType[] allowed) {
        _allowed = allowed ?? Array.Empty<UserType>();
    }

    public Task OnAuthorizationAsync(AuthorizationFilterContext context) {
        if (_allowed.Length == 0) {
            return Task.CompletedTask;
        }

        var claims = context.HttpContext.User;
        if (claims?.Identity?.IsAuthenticated != true) {
            context.Result = new UnauthorizedResult();
            return Task.CompletedTask;
        }

        var userTypeClaim = claims.FindFirst("user_type")?.Value;
        if (string.IsNullOrWhiteSpace(userTypeClaim)) {
            context.Result = new ForbidResult();
            return Task.CompletedTask;
        }

        if (Enum.TryParse<UserType>(userTypeClaim, true, out var parsed) && _allowed.Contains(parsed)) {
            return Task.CompletedTask;
        }

        context.Result = new ForbidResult();
        return Task.CompletedTask;
    }
    public UserType[]? Allowed { get; init; }
}
