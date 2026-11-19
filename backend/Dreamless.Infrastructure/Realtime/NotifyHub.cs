using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Dreamless.Infrastructure.Realtime;

public static class NotifyGroups
{
    public static string Tenant(long tenantId) => $"tenant:{tenantId}";

    public static string Dept(long tenantId, long deptId) => $"dept:{tenantId}:{deptId}";

    public static string User(long tenantId, long userId) => $"user:{tenantId}:{userId}";
}

[Authorize]
public sealed class NotifyHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var user = Context.User;
        if (long.TryParse(user?.FindFirst("tenant_id")?.Value, out var tenantId) && tenantId > 0)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, NotifyGroups.Tenant(tenantId));
            if (long.TryParse(user?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var userId) && userId > 0)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, NotifyGroups.User(tenantId, userId));
            }

            if (long.TryParse(user?.FindFirst("dept_id")?.Value, out var deptId) && deptId > 0)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, NotifyGroups.Dept(tenantId, deptId));
            }
        }

        await base.OnConnectedAsync();
    }
}
