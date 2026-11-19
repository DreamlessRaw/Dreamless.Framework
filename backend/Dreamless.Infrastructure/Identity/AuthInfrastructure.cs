using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Dreamless.Application.Security;
using Dreamless.Core.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Dreamless.Infrastructure.Identity;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "Dreamless";

    public string Audience { get; set; } = "Dreamless";

    public string Key { get; set; } = string.Empty;

    public int AccessMinutes { get; set; } = 120;

    public int RefreshDays { get; set; } = 7;
}

public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public long UserId => ReadLong(JwtRegisteredClaimNames.Sub);

    public long TenantId => ReadLong("tenant_id");

    public long? DeptId
    {
        get
        {
            var value = Principal?.FindFirst("dept_id")?.Value;
            return long.TryParse(value, out var id) ? id : null;
        }
    }

    public string UserName => Principal?.FindFirst(JwtRegisteredClaimNames.UniqueName)?.Value ?? string.Empty;

    public bool IsHostAdmin => Principal?.FindFirst("host")?.Value == "1";

    public string Culture => accessor.HttpContext?.Items["Culture"] as string ?? "zh-CN";

    private long ReadLong(string claim)
    {
        var value = Principal?.FindFirst(claim)?.Value;
        return long.TryParse(value, out var id) ? id : 0;
    }
}

public sealed class PasswordService
{
    private readonly PasswordHasher<SysUser> _hasher = new();

    public string Hash(SysUser user, string password) => _hasher.HashPassword(user, password);

    public bool Verify(SysUser user, string password) =>
        _hasher.VerifyHashedPassword(user, user.PasswordHash, password) != PasswordVerificationResult.Failed;
}

public sealed class JwtTokenService(IOptions<JwtOptions> options)
{
    public (string Token, DateTime Expires) CreateAccessToken(SysUser user)
    {
        var opt = options.Value;
        var expires = DateTime.UtcNow.AddMinutes(opt.AccessMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.UserName),
            new("tenant_id", user.TenantId.ToString()),
            new("dept_id", user.DeptId?.ToString() ?? string.Empty),
            new("host", user.IsHostAdmin ? "1" : "0")
        };
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opt.Key));
        var jwt = new JwtSecurityToken(
            opt.Issuer,
            opt.Audience,
            claims,
            expires: expires,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return (new JwtSecurityTokenHandler().WriteToken(jwt), expires);
    }

    public static string CreateRefreshToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
