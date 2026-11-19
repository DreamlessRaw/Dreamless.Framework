using Dreamless.Application.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Dreamless.WebApi.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class HasPermissionAttribute(string code) : Attribute, IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var current = context.HttpContext.RequestServices.GetRequiredService<ICurrentUser>();
        if (!current.IsAuthenticated)
        {
            context.Result = new UnauthorizedObjectResult(new { message = "未登录或登录已过期" });
            return;
        }

        if (current.IsHostAdmin)
        {
            return;
        }

        var permissions = context.HttpContext.RequestServices.GetRequiredService<IPermissionService>();
        if (!await permissions.HasAsync(current.UserId, code))
        {
            var l10n = context.HttpContext.RequestServices.GetRequiredService<ILocalizationService>();
            context.Result = new ObjectResult(new { message = await l10n.T("error.forbidden", "无权限访问") })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }
    }
}
