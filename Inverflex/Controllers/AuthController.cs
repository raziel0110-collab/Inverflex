using Inverflex.Data;
using Inverflex.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;

namespace Inverflex.Controllers;

public sealed record LoginRequest(string Username, string Password);

[ApiController, Route("api/v{version:range(1,2)}/auth")]
[ApiExplorerSettings(GroupName = "cliente")] 

public sealed class AuthController(InvestmentDbContext db, TokenService tokens) : ControllerBase
{
    [HttpPost("token"), ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Token(LoginRequest request, CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.Username == request.Username, ct);

        // Always attempt password verification to prevent timing attacks
        var dummyHash = new PasswordHasher<Inverflex.Model.AppUser>().HashPassword(null!, "dummy");
        var verificationResult = user is null 
            ? new PasswordHasher<Inverflex.Model.AppUser>().VerifyHashedPassword(null!, dummyHash, request.Password)
            : new PasswordHasher<Inverflex.Model.AppUser>().VerifyHashedPassword(user, user.PasswordHash, request.Password);

        if (user is null || verificationResult == PasswordVerificationResult.Failed)
            return Unauthorized(new ProblemDetails { Status = 401, Title = "Credenciales inválidas." });

        return Ok(tokens.Create(user.Id, user.Username, user.Role));
    }

    [Microsoft.AspNetCore.Authorization.Authorize]
    [HttpPost("revoke")]
    public async Task<IActionResult> Revoke(CancellationToken ct)
    {
        var authorization = Request.Headers.Authorization.ToString();
        if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return BadRequest();
        await tokens.RevokeAsync(authorization[7..], ct);
        return NoContent();
    }
}
