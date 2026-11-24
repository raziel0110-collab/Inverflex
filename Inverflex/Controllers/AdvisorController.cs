using Inverflex.Model;
using Inverflex.Repositories;
using Inverflex.Queries;
using Inverflex.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Text;

namespace Inverflex.Controllers;

[ApiController, Route("api/v{version:range(1,2)}/advisor")]
[Authorize(Roles = "asesor,admin")]
[ApiExplorerSettings(GroupName = "asesor")]
public sealed class AdvisorController(IUnitOfWork uow, IOrderQueries orderQueries, OrderService orders, PortfolioService portfolios) : ControllerBase
{
    [HttpGet("clients/{clientId}/portfolio")]
    public async Task<IActionResult> ClientPortfolio(int clientId, PortfolioAccessService access, PortfolioService portfolios, CancellationToken ct)
    {
        if (!await access.CanAccessAsync(User.UserId(), User.AppRole(), clientId, ct)) return Forbid();
        return Ok(await portfolios.GetAsync(clientId, ct));
    }

    [HttpGet("orders/pending")]
    public async Task<IActionResult> Pending(CancellationToken ct)
    {
        var userId = User.UserId();
        var userRole = User.AppRole();

        List<int> clientIds;
        if (userRole == UserRole.Admin)
        {
            clientIds = (await uow.Users.GetAllAsync(ct))
                .Where(x => x.Role == UserRole.Cliente)
                .Select(x => x.Id)
                .ToList();
        }
        else
        {
            clientIds = (await uow.Assignments.GetByAdvisorIdAsync(userId, ct))
                .Select(x => x.ClientId)
                .ToList();
        }

        var pendingOrders = await orderQueries.GetPendingByClientsAsync(clientIds, ct);

        var filteredOrders = pendingOrders
            .Where(x => clientIds.Contains(x.ClientId))
            .OrderBy(x => x.CreatedAt)
            .Select(x => new
            {
                x.Id,
                x.ClientId,
                x.InstrumentId,
                x.Side,
                x.Quantity,
                x.UnitPrice,
                x.TotalAmount,
                Status = x.Status.ToString(),
                x.CreatedAt,
                x.CompletedAt,
                x.ApprovedBy,
                x.Version
            })
            .ToList();

        return Ok(filteredOrders);
    }

    [HttpPost("orders/{orderId}/approve")]
    public async Task<IActionResult> Approve(string orderId, CancellationToken ct) =>
        Ok(await orders.ApproveAsync(orderId, User.UserId(), User.AppRole(), ct));

    [HttpPost("orders/{orderId}/revoke")]
    public async Task<IActionResult> CancelOrden(string orderId, int clientId, CancellationToken ct) =>
        Ok(await orders.CancelAsync(orderId, clientId, User.AppRole(), ct));

    [HttpGet("reports/performance")]
    public async Task<IActionResult> Report([FromQuery] DateTimeOffset from, [FromQuery] DateTimeOffset to, [FromQuery] string format = "json", CancellationToken ct = default)
    {
        if (from > to) return BadRequest(new ProblemDetails { Status = 400, Title = "from debe ser anterior o igual a to." });
        if (format is not "json" and not "csv") return BadRequest(new ProblemDetails { Status = 400, Title = "format debe ser json o csv." });

        List<int> clientIds;
        if (User.AppRole() == UserRole.Admin)
        {
            clientIds = (await uow.Users.GetAllAsync(ct))
                .Where(x => x.Role == UserRole.Cliente)
                .Select(x => x.Id)
                .ToList();
        }
        else
        {
            clientIds = (await uow.Assignments.GetByAdvisorIdAsync(User.UserId(), ct))
                .Select(x => x.ClientId)
                .ToList();
        }

        var periodOrders = await orderQueries.GetExecutedByClientsAsync(clientIds, from, to, ct);

        var data = new List<PerformanceReportRow>();
        foreach (var clientId in clientIds)
        {
            var portfolio = await portfolios.GetAsync(clientId, ct);
            var clientOrders = periodOrders.Where(x => x.ClientId == clientId).ToArray();
            var netFlows = clientOrders.Sum(x => x.Side == OrderSide.Compra ? -x.TotalAmount : x.TotalAmount);
            data.Add(new(clientId, portfolio.TotalValue, portfolio.GainLoss, portfolio.ReturnPercent, netFlows, clientOrders.Length, portfolio.CalculatedAt));
        }

        if (format == "json") return Ok(new { from, to, clients = data });

        var csv = new StringBuilder("clientId,portfolioValue,gainLoss,returnPercent,netFlowsInPeriod,executedOrders,asOf\n");
        foreach (var row in data)
            csv.Append(row.ClientId).Append(',').Append(row.PortfolioValue.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(row.GainLoss.ToString(CultureInfo.InvariantCulture)).Append(',').Append(row.ReturnPercent.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(row.NetFlowsInPeriod.ToString(CultureInfo.InvariantCulture)).Append(',').Append(row.ExecutedOrders).Append(',')
                .AppendLine(row.AsOf.ToUniversalTime().ToString(UtcDateTimeOffsetJsonConverter.Format, CultureInfo.InvariantCulture));
        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", "performance-report.csv");
    }
}

public sealed record PerformanceReportRow(int ClientId, decimal PortfolioValue, decimal GainLoss, decimal ReturnPercent, decimal NetFlowsInPeriod, int ExecutedOrders, DateTimeOffset AsOf);
