using Inverflex.Data;
using Inverflex.Model;
using Microsoft.EntityFrameworkCore;

namespace Inverflex.Services;

public sealed class AlertEvaluationService(InvestmentDbContext db, IMarketPriceService market)
{
    public async Task<int> EvaluateAsync(CancellationToken ct)
    {
        var alerts = await db.PriceAlerts.Where(x => !x.IsTriggered).ToListAsync(ct);
        var triggered = 0;
        foreach (var alert in alerts)
        {
            var current = await market.GetPriceAsync(alert.InstrumentId, ct);
            var limit = alert.Direction == AlertDirection.Sube
                ? alert.BaselinePrice * (1 + alert.PercentChange / 100)
                : alert.BaselinePrice * (1 - alert.PercentChange / 100);
            if (alert.Direction == AlertDirection.Sube ? current < limit : current > limit) continue;

            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var triggeredAt = DateTimeOffset.UtcNow;
            var claimed = await db.PriceAlerts.Where(x => x.Id == alert.Id && !x.IsTriggered)
                .ExecuteUpdateAsync(update => update.SetProperty(x => x.IsTriggered, true).SetProperty(x => x.TriggeredAt, triggeredAt), ct);
            if (claimed == 0)
            {
                await transaction.RollbackAsync(ct);
                continue;
            }
            alert.IsTriggered = true;
            alert.TriggeredAt = triggeredAt;
            db.Notifications.Add(new ClientNotification { ClientId = alert.ClientId, Message = $"Alerta de precio: {alert.InstrumentId} alcanzó {current:C2} USD." });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            triggered++;
        }
        return triggered;
    }
}

public sealed class AlertEvaluationWorker(IServiceScopeFactory scopeFactory, ILogger<AlertEvaluationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<AlertEvaluationService>().EvaluateAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogError(exception, "Falló la evaluación periódica de alertas."); }
        }
    }
}
