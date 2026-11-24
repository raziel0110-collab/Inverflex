using Inverflex.Data;
using Inverflex.Model;
using Microsoft.EntityFrameworkCore;

namespace Inverflex.Services;

public sealed record OrderApprovalDecision(bool RequiresApproval, bool CanApprove, string? Error);

public static class OrderApprovalPolicy
{
    public const decimal ApprovalThreshold = 50_000m;

    public static OrderApprovalDecision Evaluate(decimal amount, OrderStatus status, UserRole role)
    {
        if (amount <= 0) return new(false, false, "El monto debe ser positivo.");
        if (status != OrderStatus.PendienteAprobacion) return new(amount >= ApprovalThreshold, false, "La orden no está pendiente de aprobación.");
        if (amount < ApprovalThreshold) return new(false, false, "La orden no requiere aprobación de asesor.");
        if (role is not UserRole.Asesor and not UserRole.Admin) return new(true, false, "Solo un asesor puede aprobar esta orden.");
        return new(true, true, null);
    }
}

public sealed class OrderService(InvestmentDbContext db, IMarketPriceService market, PortfolioService portfolios)
{
    public async Task<InvestmentOrder> CreateAsync(int clientId, string instrumentId, OrderSide side, decimal quantity, CancellationToken ct)
    {
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), "La cantidad debe ser positiva.");
        var instrumentExists = await db.Instruments.AnyAsync(x => x.Id == instrumentId, ct);
        if (!instrumentExists) throw new KeyNotFoundException("Instrumento no encontrado.");
        var price = await market.GetPriceAsync(instrumentId, ct);
        var amount = decimal.Round(price * quantity, 2);
        var status = amount >= OrderApprovalPolicy.ApprovalThreshold ? OrderStatus.PendienteAprobacion : OrderStatus.Ejecutada;
        var order = new InvestmentOrder { ClientId = clientId, InstrumentId = instrumentId, Side = side, Quantity = quantity, UnitPrice = price, TotalAmount = amount, Status = status };
        if (status == OrderStatus.Ejecutada)
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await ApplyPositionAsync(clientId, instrumentId, side, quantity, price, ct);
            order.CompletedAt = DateTimeOffset.UtcNow;
            db.Orders.Add(order);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            await portfolios.InvalidateAsync(clientId, ct);
            return order;
        }
        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);
        return order;
    }

    public async Task<InvestmentOrder> ApproveAsync(string orderId, int advisorId, UserRole role, CancellationToken ct)
    {
        var order = await db.Orders.SingleOrDefaultAsync(x => x.Id == orderId, ct) ?? throw new KeyNotFoundException("Orden no encontrada.");
        var decision = OrderApprovalPolicy.Evaluate(order.TotalAmount, order.Status, role);
        if (!decision.CanApprove) throw new InvalidOperationException(decision.Error);
        if (role != UserRole.Admin && !await db.AdvisorAssignments.AnyAsync(x => x.AdvisorId == advisorId && x.ClientId == order.ClientId, ct))
            throw new UnauthorizedAccessException("El asesor no está asignado al cliente.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var completedAt = DateTimeOffset.UtcNow;
        var pendingOrder = db.Orders.Where(x => x.Id == orderId && x.Status == OrderStatus.PendienteAprobacion);
        var changed = db.Database.IsSqlite()
            ? await pendingOrder.ExecuteUpdateAsync(update => update.SetProperty(x => x.Status, OrderStatus.Ejecutada)
                .SetProperty(x => x.ApprovedBy, advisorId).SetProperty(x => x.CompletedAt, completedAt)
                .SetProperty(x => x.CompletedAtUtcTicks, InvestmentDbContext.ToUtcSecondTicks(completedAt))
                .SetProperty(x => x.Version, x => x.Version + 1), ct)
            : await pendingOrder.ExecuteUpdateAsync(update => update.SetProperty(x => x.Status, OrderStatus.Ejecutada)
                .SetProperty(x => x.ApprovedBy, advisorId).SetProperty(x => x.CompletedAt, completedAt)
                .SetProperty(x => x.Version, x => x.Version + 1), ct);
        if (changed != 1) throw new InvalidOperationException("La orden ya fue procesada por otro asesor.");
        await ApplyPositionAsync(order.ClientId, order.InstrumentId, order.Side, order.Quantity, order.UnitPrice, ct);
        await tx.CommitAsync(ct);
        await portfolios.InvalidateAsync(order.ClientId, ct);
        return await db.Orders.AsNoTracking().SingleAsync(x => x.Id == orderId, ct);
    }

    public async Task<InvestmentOrder> CancelAsync(string orderId, int clientId, UserRole role, CancellationToken ct)
    {
        var query = db.Orders.Where(x => x.Id == orderId);
        if (role != UserRole.Admin) query = query.Where(x => x.ClientId == clientId);
        var order = await query.SingleOrDefaultAsync(ct) ?? throw new KeyNotFoundException("Orden no encontrada.");
        if (order.Status != OrderStatus.PendienteAprobacion) throw new InvalidOperationException("Solo se pueden cancelar órdenes pendientes.");
        order.Status = OrderStatus.Cancelada;
        order.CompletedAt = DateTimeOffset.UtcNow;
        order.Version++;
        await db.SaveChangesAsync(ct);
        return order;
    }

    private async Task ApplyPositionAsync(int clientId, string instrumentId, OrderSide side, decimal quantity, decimal price, CancellationToken ct)
    {
        var portfolio = await db.Portfolios.Include(x => x.Positions).SingleOrDefaultAsync(x => x.ClientId == clientId, ct)
            ?? throw new KeyNotFoundException("Portafolio no encontrado.");
        var position = portfolio.Positions.SingleOrDefault(x => x.InstrumentId == instrumentId);
        if (side == OrderSide.Venta && (position is null || position.Quantity < quantity))
            throw new InvalidOperationException("Cantidad insuficiente para ejecutar la venta.");
        if (side == OrderSide.Venta)
        {
            position!.Quantity -= quantity;
            if (position.Quantity == 0) db.Positions.Remove(position);
        }
        else if (position is null)
            portfolio.Positions.Add(new Position { InstrumentId = instrumentId, Quantity = quantity, AverageCost = price });
        else
        {
            position.AverageCost = (position.Quantity * position.AverageCost + quantity * price) / (position.Quantity + quantity);
            position.Quantity += quantity;
        }
        await db.SaveChangesAsync(ct);
    }
}
