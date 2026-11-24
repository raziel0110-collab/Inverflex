using Inverflex.Model;
using Inverflex.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Globalization;

namespace Inverflex.Data;

public sealed class InvestmentDbContext(DbContextOptions<InvestmentDbContext> options) : DbContext(options)
{
    private static readonly ValueConverter<DateTimeOffset, string> SqliteDateConverter = new(
        value => value.ToUniversalTime().ToString(UtcDateTimeOffsetJsonConverter.Format, CultureInfo.InvariantCulture),
        value => DateTimeOffset.ParseExact(value, UtcDateTimeOffsetJsonConverter.Format, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal));

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<AdvisorAssignment> AdvisorAssignments => Set<AdvisorAssignment>();
    public DbSet<Instrument> Instruments => Set<Instrument>();
    public DbSet<Portfolio> Portfolios => Set<Portfolio>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<InvestmentOrder> Orders => Set<InvestmentOrder>();
    public DbSet<PriceAlert> PriceAlerts => Set<PriceAlert>();
    public DbSet<ClientNotification> Notifications => Set<ClientNotification>();

    public static long ToUtcSecondTicks(DateTimeOffset value)
    {
        var ticks = value.UtcDateTime.Ticks;
        return ticks - ticks % TimeSpan.TicksPerSecond;
    }

    public override int SaveChanges() => SaveChanges(true);

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        SynchronizeDateTicks();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        SaveChangesAsync(true, cancellationToken);

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        SynchronizeDateTicks();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void SynchronizeDateTicks()
    {
        if (!Database.IsSqlite()) return;

        foreach (var entry in ChangeTracker.Entries<InvestmentOrder>().Where(x => x.State is EntityState.Added or EntityState.Modified))
        {
            entry.Entity.CreatedAtUtcTicks = ToUtcSecondTicks(entry.Entity.CreatedAt);
            entry.Entity.CompletedAtUtcTicks = entry.Entity.CompletedAt is { } completedAt
                ? ToUtcSecondTicks(completedAt)
                : null;
        }

        foreach (var entry in ChangeTracker.Entries<PriceAlert>().Where(x => x.State is EntityState.Added or EntityState.Modified))
            entry.Entity.TriggeredAtUtcTicks = entry.Entity.TriggeredAt is { } triggeredAt
                ? ToUtcSecondTicks(triggeredAt)
                : null;

        foreach (var entry in ChangeTracker.Entries<ClientNotification>().Where(x => x.State is EntityState.Added or EntityState.Modified))
            entry.Entity.CreatedAtUtcTicks = ToUtcSecondTicks(entry.Entity.CreatedAt);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>().HasIndex(x => x.Id).IsUnique();
        modelBuilder.Entity<AppUser>().HasIndex(x => x.Username).IsUnique();
        modelBuilder.Entity<AdvisorAssignment>().HasIndex(x => new { x.AdvisorId, x.ClientId }).IsUnique();
        modelBuilder.Entity<Instrument>().HasIndex(x => x.Symbol).IsUnique();
        modelBuilder.Entity<Position>().HasIndex(x => new { x.PortfolioId, x.InstrumentId }).IsUnique();
        modelBuilder.Entity<InvestmentOrder>().Property(x => x.Version).IsConcurrencyToken();
        modelBuilder.Entity<PriceAlert>().HasIndex(x => new { x.ClientId, x.IsTriggered });
        if (Database.IsSqlite())
        {
            modelBuilder.Entity<InvestmentOrder>().Property(x => x.CreatedAt).HasConversion(SqliteDateConverter).HasColumnType("TEXT");
            modelBuilder.Entity<InvestmentOrder>().Property(x => x.CompletedAt).HasConversion(SqliteDateConverter).HasColumnType("TEXT");
            modelBuilder.Entity<PriceAlert>().Property(x => x.TriggeredAt).HasConversion(SqliteDateConverter).HasColumnType("TEXT");
            modelBuilder.Entity<ClientNotification>().Property(x => x.CreatedAt).HasConversion(SqliteDateConverter).HasColumnType("TEXT");
            modelBuilder.Entity<InvestmentOrder>().HasIndex(x => new { x.ClientId, x.CreatedAtUtcTicks });
        }
        else
        {
            modelBuilder.Entity<InvestmentOrder>().Property(x => x.CreatedAt).HasConversion(new DateTimeOffsetToBinaryConverter());
            modelBuilder.Entity<InvestmentOrder>().Property(x => x.CompletedAt).HasConversion(new DateTimeOffsetToBinaryConverter());
            modelBuilder.Entity<PriceAlert>().Property(x => x.TriggeredAt).HasConversion(new DateTimeOffsetToBinaryConverter());
            modelBuilder.Entity<ClientNotification>().Property(x => x.CreatedAt).HasConversion(new DateTimeOffsetToBinaryConverter());
            modelBuilder.Entity<InvestmentOrder>().Ignore(x => x.CreatedAtUtcTicks);
            modelBuilder.Entity<InvestmentOrder>().Ignore(x => x.CompletedAtUtcTicks);
            modelBuilder.Entity<PriceAlert>().Ignore(x => x.TriggeredAtUtcTicks);
            modelBuilder.Entity<ClientNotification>().Ignore(x => x.CreatedAtUtcTicks);
            modelBuilder.Entity<InvestmentOrder>().HasIndex(x => new { x.ClientId, x.CreatedAt });
        }
        modelBuilder.Entity<Portfolio>().HasMany(x => x.Positions).WithOne().HasForeignKey(x => x.PortfolioId);
        modelBuilder.Entity<AppUser>().HasMany(x => x.Portfolios).WithOne().HasForeignKey(x => x.ClientId);
    }
}
