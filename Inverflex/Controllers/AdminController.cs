using Inverflex.DTOs;
using Inverflex.Model;
using Inverflex.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;

namespace Inverflex.Controllers;

[ApiController, Route("api/v{version:range(1,2)}/admin")]
[Authorize(Roles = "admin")]
[ApiExplorerSettings(GroupName = "admin")]
public sealed class AdminController(IUnitOfWork uow, IDistributedCache cache) : ControllerBase
{
    [HttpGet("users")]
    public async Task<IActionResult> Users(CancellationToken ct)
    {
        var users = await uow.Users.GetAllAsync(ct);
        return Ok(users.Select(x => new UserListDto(x.Id, x.Name, x.Username, x.Role.ToString())).ToList());
    }

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser(UserCreateRequestDto request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new ProblemDetails { Status = 400, Title = "El nombre, el usuario y la contraseña son obligatorios." });
        var name = request.Name.Trim();
        var username = request.Username.Trim();
        var password = request.Password.Trim();

        // Additional validation
        if (name.Length < 3 || name.Length > 200)
            return BadRequest(new ProblemDetails { Status = 400, Title = "El nombre debe tener entre 3 y 200 caracteres." });
        if (username.Length < 3 || username.Length > 100)
            return BadRequest(new ProblemDetails { Status = 400, Title = "El usuario debe tener entre 3 y 100 caracteres." });
        if (password.Length < 8)
            return BadRequest(new ProblemDetails { Status = 400, Title = "La contraseña debe tener al menos 8 caracteres." });
        // Validate password complexity
        if (!System.Text.RegularExpressions.Regex.IsMatch(password, @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[a-zA-Z\d@$!%*?&]+$"))
            return BadRequest(new ProblemDetails { Status = 400, Title = "La contraseña debe contener mayúsculas, minúsculas, números y caracteres especiales." });

        if (!Enum.IsDefined(request.Role))
            return BadRequest(new ProblemDetails { Status = 400, Title = "El rol especificado no es válido." });
        if (await uow.Users.ExistsByUsernameAsync(username, ct))
            return Conflict(new ProblemDetails { Status = 409, Title = "El nombre de usuario ya existe." });

        var user = new AppUser { Name = name, Username = username, Role = request.Role };
        user.PasswordHash = new PasswordHasher<AppUser>().HashPassword(user, password);
        await uow.Users.AddAsync(user, ct);

        if (user.Role == UserRole.Cliente)
        {
            var portfolio = new Portfolio { ClientId = user.Id };
            await uow.Portfolios.AddAsync(portfolio, ct);
        }

        return StatusCode(StatusCodes.Status201Created, new UserCreateResponseDto(user.Id, user.Name, user.Username, user.Role));
    }

    [HttpPost("instruments")]
    public async Task<IActionResult> CreateInstrument(InstrumentCreateRequestDto request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Symbol) || string.IsNullOrWhiteSpace(request.Name)
            || string.IsNullOrWhiteSpace(request.Sector) || string.IsNullOrWhiteSpace(request.Currency))
            return BadRequest(new ProblemDetails { Status = 400, Title = "Símbolo, nombre, sector y moneda son obligatorios." });
        var symbol = request.Symbol.Trim().ToUpperInvariant();
        var name = request.Name.Trim();
        var sector = request.Sector.Trim();
        var currency = request.Currency.Trim().ToUpperInvariant();

        // Validate input lengths and format
        if (symbol.Length < 1 || symbol.Length > 20 || !System.Text.RegularExpressions.Regex.IsMatch(symbol, @"^[A-Z0-9]+$"))
            return BadRequest(new ProblemDetails { Status = 400, Title = "Símbolo debe tener 1-20 caracteres alfanuméricos." });
        if (name.Length < 3 || name.Length > 200)
            return BadRequest(new ProblemDetails { Status = 400, Title = "Nombre debe tener 3-200 caracteres." });
        if (sector.Length < 3 || sector.Length > 100)
            return BadRequest(new ProblemDetails { Status = 400, Title = "Sector debe tener 3-100 caracteres." });
        if (currency.Length != 3 || !System.Text.RegularExpressions.Regex.IsMatch(currency, @"^[A-Z]{3}$"))
            return BadRequest(new ProblemDetails { Status = 400, Title = "Moneda debe ser código ISO 4217 (3 letras mayúsculas)." });

        if (!Enum.IsDefined(request.Type))
            return BadRequest(new ProblemDetails { Status = 400, Title = "El tipo de instrumento especificado no es válido." });
        if (request.ReferencePrice <= 0 || request.ReferencePrice > decimal.MaxValue / 1000000)
            return BadRequest(new ProblemDetails { Status = 400, Title = "El precio de referencia debe ser mayor que cero y válido." });
        if (await uow.Instruments.ExistsBySymbolAsync(symbol, ct))
            return Conflict(new ProblemDetails { Status = 409, Title = "El símbolo del instrumento ya existe." });

        var instrument = new Instrument
        {
            Id = symbol,
            Symbol = symbol,
            Name = name,
            Type = request.Type,
            Sector = sector,
            ReferencePrice = request.ReferencePrice,
            Currency = currency
        };
        await uow.Instruments.AddAsync(instrument, ct);
        await cache.RemoveAsync("catalog:v1", ct);

        return StatusCode(StatusCodes.Status201Created, instrument);
    }

    [HttpGet("assignments")]
    public async Task<IActionResult> Assignments(CancellationToken ct)
    {
        var assignments = await uow.Assignments.GetAllAsync(ct);
        return Ok(assignments);
    }

    [HttpPost("assignments")]
    public async Task<IActionResult> Assign(AssignmentRequestDto request, CancellationToken ct)
    {
        var advisor = await uow.Users.GetByIdAsync(request.AdvisorId, ct);
        var client = await uow.Users.GetByIdAsync(request.ClientId, ct);

        if (advisor is null || advisor.Role != UserRole.Asesor || client is null || client.Role != UserRole.Cliente)
            return BadRequest(new ProblemDetails { Status = 400, Title = "La asignación debe referenciar un asesor y un cliente válidos." });

        if (await uow.Assignments.ExistsAsync(request.AdvisorId, request.ClientId, ct))
            return Conflict(new ProblemDetails { Status = 409, Title = "La asignación ya existe." });

        var assignment = new AdvisorAssignment { AdvisorId = request.AdvisorId, ClientId = request.ClientId };
        await uow.Assignments.AddAsync(assignment, ct);

        return NoContent();
    }
}
