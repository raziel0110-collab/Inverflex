using Inverflex.DTOs;
using Inverflex.Model;
using Inverflex.Repositories;
using Inverflex.Queries;
using Inverflex.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inverflex.Controllers;

[ApiController, Route("api/v{version:range(1,2)}")]
[Authorize(Roles = "cliente,asesor,admin")]
public sealed class ClientController(IUnitOfWork uow, IOrderQueries orderQueries, PortfolioService portfolios, PortfolioAccessService access, InstrumentCatalogService catalog) : ControllerBase
{
    [HttpGet("portfolios/{clientId}")]
    [ApiExplorerSettings(GroupName = "cliente")]
    public async Task<IActionResult> Portfolio(int clientId, CancellationToken ct)
    {
        if (!await access.CanAccessAsync(User.UserId(), User.AppRole(), clientId, ct)) return Forbid();
        return Ok(await portfolios.GetAsync(clientId, ct));
    }

    [HttpGet("instruments")]
    [AllowAnonymous]
    [ApiExplorerSettings(GroupName = "cliente")]
    public async Task<IActionResult> Instruments([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] InstrumentType? type = null, [FromQuery] string? sector = null, CancellationToken ct = default)
    {
        if (page < 1 || pageSize is < 1 or > 100) return BadRequest(new ProblemDetails { Status = 400, Title = "page debe ser mayor que 0 y pageSize debe estar entre 1 y 100." });
        return Ok(new { page, pageSize, items = await catalog.GetPageAsync(page, pageSize, type, sector, ct) });
    }

    [HttpPost("orders")]
    [Authorize(Roles = "cliente,admin")]
    [ApiExplorerSettings(GroupName = "cliente")]
    public async Task<IActionResult> CreateOrder(OrderCreateRequestDto request, [FromServices] OrderService orders, CancellationToken ct)
    {
        // Validate quantity and instrument ID
        if (request.Quantity <= 0 || request.Quantity > 1000000)
            return BadRequest(new ProblemDetails { Status = 400, Title = "La cantidad debe estar entre 1 y 1,000,000." });
        if (string.IsNullOrWhiteSpace(request.InstrumentId) || request.InstrumentId.Length > 20)
            return BadRequest(new ProblemDetails { Status = 400, Title = "El ID del instrumento es obligatorio y debe tener máximo 20 caracteres." });
        if (!Enum.IsDefined(request.Side))
            return BadRequest(new ProblemDetails { Status = 400, Title = "El lado de la orden (Buy/Sell) no es válido." });

        var clientId = User.AppRole() == UserRole.Admin ? request.ClientId ?? User.UserId() : User.UserId();
        var order = await orders.CreateAsync(clientId, request.InstrumentId, request.Side, request.Quantity, ct);
        return CreatedAtAction(nameof(OrderHistory), new { version = RouteData.Values["version"] }, new OrderResponseDto(
            order.Id,
            order.ClientId,
            order.InstrumentId,
            order.Side.ToString(),
            order.Quantity,
            order.UnitPrice,
            order.TotalAmount,
            order.Status.ToString(),
            order.CreatedAt,
            order.CompletedAt,
            order.ApprovedBy,
            order.Version
        ));
    }

    [HttpGet("orders")]
    [ApiExplorerSettings(GroupName = "cliente")]
    public async Task<IActionResult> OrderHistory([FromQuery] string? instrumentId, [FromQuery] OrderStatus? status, [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        if (page < 1 || pageSize is < 1 or > 100 || (from.HasValue && to.HasValue && from > to)) return BadRequest(new ProblemDetails { Status = 400, Title = "Filtros de paginación o fechas inválidos." });

        int? clientId = User.AppRole() == UserRole.Admin ? null : User.UserId();
        var total = await orderQueries.CountHistoryAsync(clientId, instrumentId, status, from, to, ct);
        var items = await orderQueries.GetHistoryAsync(clientId, instrumentId, status, from, to, page, pageSize, ct);

        return Ok(new { page, pageSize, total, items });
    }

    [HttpDelete("orders/{orderId}")]
    [Authorize(Roles = "cliente,admin")]
    [ApiExplorerSettings(GroupName = "cliente")]
    public async Task<IActionResult> CancelOrder(string orderId, [FromServices] OrderService orders, CancellationToken ct) =>
        Ok(await orders.CancelAsync(orderId, User.UserId(), User.AppRole(), ct));

    [HttpPost("alerts")]
    [Authorize(Roles = "cliente,admin")]
    [ApiExplorerSettings(GroupName = "cliente")]
    public async Task<IActionResult> CreateAlert(AlertCreateRequestDto request, IMarketPriceService market, CancellationToken ct)
    {
        if (request.PercentChange <= 0 || request.PercentChange > 100) return BadRequest(new ProblemDetails { Status = 400, Title = "percentChange debe estar entre 0 y 100." });
        var clientId = User.AppRole() == UserRole.Admin ? request.ClientId ?? User.UserId() : User.UserId();
        var baseline = await market.GetPriceAsync(request.InstrumentId, ct);
        var alert = new PriceAlert { ClientId = clientId, InstrumentId = request.InstrumentId, Direction = request.Direction, BaselinePrice = baseline, PercentChange = request.PercentChange };
        await uow.Alerts.AddAsync(alert, ct);
        return CreatedAtAction(nameof(Notifications), new { version = RouteData.Values["version"] }, new AlertResponseDto(alert.Id, alert.ClientId, alert.InstrumentId, alert.Direction, alert.BaselinePrice, alert.PercentChange, alert.IsTriggered, alert.TriggeredAt));
    }

    [HttpGet("alerts")]
    [ApiExplorerSettings(GroupName = "cliente")]
    public async Task<IActionResult> Alerts(CancellationToken ct)
    {
        var alerts = await uow.Alerts.GetByClientIdAsync(User.UserId(), ct);
        var dtos = alerts.Select(a => new AlertResponseDto(a.Id, a.ClientId, a.InstrumentId, a.Direction, a.BaselinePrice, a.PercentChange, a.IsTriggered, a.TriggeredAt)).ToList();
        return Ok(dtos);
    }

    [HttpGet("notifications")]
    [ApiExplorerSettings(GroupName = "cliente")]
    public async Task<IActionResult> Notifications(CancellationToken ct)
    {
        var notifications = await uow.Notifications.GetByClientIdAsync(User.UserId(), 100, ct);
        var dtos = notifications.Select(n => new NotificationResponseDto(n.Id, n.ClientId, n.Message, n.CreatedAt)).ToList();
        return Ok(dtos);
    }
}
