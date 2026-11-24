using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Inverflex.Data;
using Inverflex.Model;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.IdentityModel.Tokens;

namespace Inverflex.Services;

public sealed class PortfolioAccessService(InvestmentDbContext db)
{
    public async Task<bool> CanAccessAsync(int userId, UserRole role, int clientId, CancellationToken ct) =>
        role == UserRole.Admin || userId == clientId || (role == UserRole.Asesor && await db.AdvisorAssignments.AnyAsync(x => x.AdvisorId == userId && x.ClientId == clientId, ct));
}

public sealed record TokenResponse(string AccessToken, DateTimeOffset ExpiresAt, string TokenType = "Bearer");
public sealed class TokenService(IConfiguration configuration, IDistributedCache cache)
{
    public TokenResponse Create(int userId, string username, UserRole role)
    {
        var now = DateTimeOffset.UtcNow;
        var expires = now.AddMinutes(30);
        var claims = new[] { new Claim("userId", userId.ToString()), new Claim("role", role.ToString().ToLowerInvariant()), new Claim(JwtRegisteredClaimNames.Sub, username), new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")) };
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:SigningKey"]!));
        var token = new JwtSecurityToken(configuration["Jwt:Issuer"], configuration["Jwt:Audience"], claims, now.UtcDateTime, expires.UtcDateTime, new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    public async Task RevokeAsync(string token, CancellationToken ct)
    {
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        if (jwt.ValidTo <= DateTime.UtcNow) return;
        await cache.SetStringAsync($"revoked:{jwt.Id}", "1", new DistributedCacheEntryOptions { AbsoluteExpiration = jwt.ValidTo }, ct);
    }
}

public sealed class DeprecatedApiAttribute : Attribute { }
public sealed class DeprecatedEndpointFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.ActionDescriptor.EndpointMetadata.OfType<DeprecatedApiAttribute>().Any())
            context.HttpContext.Response.Headers["Deprecated"] = "true";
        await next();
    }
}

public static class ClaimsPrincipalExtensions
{
    public static int UserId(this ClaimsPrincipal user) => int.Parse(user.FindFirstValue("userId") ?? throw new UnauthorizedAccessException("El token no contiene userId."));
    public static UserRole AppRole(this ClaimsPrincipal user) => Enum.Parse<UserRole>(user.FindFirstValue("role") ?? "", true);
}
