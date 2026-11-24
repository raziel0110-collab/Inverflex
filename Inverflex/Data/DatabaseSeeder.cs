using Inverflex.Model;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Inverflex.Data;

public static class DatabaseSeeder
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InvestmentDbContext>();
        await db.Database.EnsureCreatedAsync();
        await EnsureAppUserNameColumnAsync(db);
        await EnsureIntegerAppUserIdAsync(db);
        await SqliteDateFormatMigration.ApplyAsync(db);

        var hasher = new PasswordHasher<AppUser>();
        var client = await GetOrCreateUserAsync("Johan Obando Picado", "JOBANDO", UserRole.Cliente);
        var client2 = await GetOrCreateUserAsync("Ana María Quesada Segura", "ANQUESADA", UserRole.Cliente);
        var advisor = await GetOrCreateUserAsync("Maribel Morales Ramos", "MAMORALES", UserRole.Asesor);
        await GetOrCreateUserAsync("Administrador", "admin", UserRole.Admin);
        await db.SaveChangesAsync();

        var instruments = new (string Symbol, string Name, InstrumentType Type, string Sector, decimal Price)[]
        {
            ("AAPL","Apple Inc.",InstrumentType.Accion,"Tecnología",225m),("MSFT","Microsoft Corp.",InstrumentType.Accion,"Tecnología",430m),
            ("NVDA","NVIDIA Corp.",InstrumentType.Accion,"Semiconductores",145m),("AMZN","Amazon.com",InstrumentType.Accion,"Consumo",215m),
            ("GOOGL","Alphabet Inc.",InstrumentType.Accion,"Tecnología",175m),("META","Meta Platforms",InstrumentType.Accion,"Tecnología",620m),
            ("TSLA","Tesla Inc.",InstrumentType.Accion,"Automotriz",340m),("JPM","JPMorgan Chase",InstrumentType.Accion,"Finanzas",245m),
            ("KO","Coca-Cola Co.",InstrumentType.Accion,"Consumo",70m),("XOM","Exxon Mobil",InstrumentType.Accion,"Energía",115m),
            ("SPY","SPDR S&P 500 ETF",InstrumentType.Etf,"Índice",600m),("QQQ","Invesco QQQ ETF",InstrumentType.Etf,"Tecnología",520m),
            ("VTI","Vanguard Total Market",InstrumentType.Etf,"Índice",300m),("EFA","iShares MSCI EAFE",InstrumentType.Etf,"Internacional",85m),
            ("AGG","iShares Core US Aggregate",InstrumentType.Etf,"Renta fija",100m),("GLD","SPDR Gold Shares",InstrumentType.Etf,"Materias primas",250m),
            ("BND","Vanguard Total Bond",InstrumentType.Etf,"Renta fija",75m),("TLT","iShares 20+ Year Treasury",InstrumentType.Etf,"Renta fija",90m),
            ("US10Y","Bono Tesoro CR 10 años",InstrumentType.Bono,"Gobierno",98m),("CORP5Y","Bono corporativo 5 años",InstrumentType.Bono,"Corporativo",102m)
        };
        var existingInstrumentIds = await db.Instruments.Select(x => x.Id).ToListAsync();
        db.Instruments.AddRange(instruments.Where(x => !existingInstrumentIds.Contains(x.Symbol))
            .Select(x => new Instrument { Id = x.Symbol, Symbol = x.Symbol, Name = x.Name, Type = x.Type, Sector = x.Sector, ReferencePrice = x.Price }));

        var portfolio = await db.Portfolios.Include(x => x.Positions).SingleOrDefaultAsync(x => x.ClientId == client.Id);

        if (portfolio is null)
        {
            portfolio = new Portfolio { ClientId = client.Id };
            db.Portfolios.Add(portfolio);
        }
        AddPositionIfMissing(portfolio, "AAPL", 10, 180);
        AddPositionIfMissing(portfolio, "SPY", 20, 540);

        var portfolio2 = await db.Portfolios.Include(x => x.Positions).SingleOrDefaultAsync(x => x.ClientId == client2.Id);
        if (portfolio2 is null)
        {
            portfolio2 = new Portfolio { ClientId = client2.Id };
            db.Portfolios.Add(portfolio2);
        }
        AddPositionIfMissing(portfolio2, "MSFT", 5, 390);

        foreach (var clientId in new[] { client.Id, client2.Id })
            if (!await db.AdvisorAssignments.AnyAsync(x => x.AdvisorId == advisor.Id && x.ClientId == clientId))
                db.AdvisorAssignments.Add(new AdvisorAssignment { AdvisorId = advisor.Id, ClientId = clientId });

        var now = DateTimeOffset.UtcNow;
        var seedOrders = new[]
        {
            new InvestmentOrder { Id = "order-executed-JOBANDO", ClientId = client.Id, InstrumentId = "AAPL", Side = OrderSide.Compra, Quantity = 10, UnitPrice = 180, TotalAmount = 1_800, Status = OrderStatus.Ejecutada, CreatedAt = now.AddDays(-10), CompletedAt = now.AddDays(-10) },
            new InvestmentOrder { Id = "order-pending-JOBANDO", ClientId = client.Id, InstrumentId = "MSFT", Side = OrderSide.Compra, Quantity = 150, UnitPrice = 430, TotalAmount = 64_500, Status = OrderStatus.PendienteAprobacion, CreatedAt = now.AddHours(-2) },
            new InvestmentOrder { Id = "order-cancelled-JOBANDO", ClientId = client.Id, InstrumentId = "QQQ", Side = OrderSide.Compra, Quantity = 2, UnitPrice = 520, TotalAmount = 1_040, Status = OrderStatus.Cancelada, CreatedAt = now.AddDays(-3), CompletedAt = now.AddDays(-3) },
            new InvestmentOrder { Id = "order-executed-ANQUESADA", ClientId = client2.Id, InstrumentId = "MSFT", Side = OrderSide.Compra, Quantity = 5, UnitPrice = 390, TotalAmount = 1_950, Status = OrderStatus.Ejecutada, CreatedAt = now.AddDays(-5), CompletedAt = now.AddDays(-5) }
        };
        var seedOrderIds = seedOrders.Select(x => x.Id).ToArray();
        var existingOrderIds = await db.Orders.Where(x => seedOrderIds.Contains(x.Id)).Select(x => x.Id).ToListAsync();
        db.Orders.AddRange(seedOrders.Where(x => !existingOrderIds.Contains(x.Id)));

        if (!await db.PriceAlerts.AnyAsync(x => x.ClientId == client.Id && x.InstrumentId == "AAPL" && x.Direction == AlertDirection.Sube))
            db.PriceAlerts.Add(new PriceAlert { ClientId = client.Id, InstrumentId = "AAPL", Direction = AlertDirection.Sube, BaselinePrice = 225, PercentChange = 5 });
        if (!await db.Notifications.AnyAsync(x => x.ClientId == client.Id && x.Message == "Tu alerta de AAPL está activa."))
            db.Notifications.Add(new ClientNotification { ClientId = client.Id, Message = "Tu alerta de AAPL está activa.", CreatedAt = now.AddMinutes(-30) });
        await db.SaveChangesAsync();

        async Task<AppUser> GetOrCreateUserAsync(string name, string username, UserRole role)
        {
            var user = await db.Users.SingleOrDefaultAsync(x => x.Username == username);
            if (user is not null)
            {
                if (string.IsNullOrWhiteSpace(user.Name))
                {
                    user.Name = name;
                    db.Users.Update(user);
                }
                return user;
            }
            user = new AppUser { Name = name, Username = username, Role = role };
            user.PasswordHash = hasher.HashPassword(user, "Demo123!");
            db.Users.Add(user);
            return user;
        }

        static async Task EnsureAppUserNameColumnAsync(InvestmentDbContext db)
        {
            if (db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
            {
                var connection = db.Database.GetDbConnection();
                var shouldOpen = connection.State != System.Data.ConnectionState.Open;
                if (shouldOpen) await connection.OpenAsync();

                await using var command = connection.CreateCommand();
                command.CommandText = "PRAGMA table_info(Users);";
                await using var reader = await command.ExecuteReaderAsync();
                var hasNameColumn = false;
                while (await reader.ReadAsync())
                {
                    if (reader.GetString(1).Equals("Name", StringComparison.OrdinalIgnoreCase))
                    {
                        hasNameColumn = true;
                        break;
                    }
                }

                await reader.DisposeAsync();
                if (!hasNameColumn)
                {
                    await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Users\" ADD COLUMN \"Name\" TEXT NOT NULL DEFAULT '';");
                }

                if (shouldOpen) await connection.CloseAsync();
            }
            else if (db.Database.ProviderName == "Microsoft.EntityFrameworkCore.SqlServer")
            {
                await db.Database.ExecuteSqlRawAsync("IF COL_LENGTH('Users', 'Name') IS NULL ALTER TABLE [Users] ADD [Name] nvarchar(max) NOT NULL DEFAULT(N'');");
            }
        }

        static async Task EnsureIntegerAppUserIdAsync(InvestmentDbContext db)
        {
            if (db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
            {
                var connection = db.Database.GetDbConnection();
                var shouldOpen = connection.State != System.Data.ConnectionState.Open;
                if (shouldOpen) await db.Database.OpenConnectionAsync();

                try
                {
                    await using var command = connection.CreateCommand();
                    command.CommandText = "PRAGMA table_info(Users);";
                    await using var reader = await command.ExecuteReaderAsync();
                    var idType = "";
                    while (await reader.ReadAsync())
                    {
                        if (reader.GetString(1).Equals("Id", StringComparison.OrdinalIgnoreCase))
                        {
                            idType = reader.GetString(2);
                            break;
                        }
                    }

                    await reader.DisposeAsync();
                    if (idType.Equals("INTEGER", StringComparison.OrdinalIgnoreCase)) return;
                    if (string.IsNullOrEmpty(idType)) throw new InvalidOperationException("No se encontró la columna Users.Id.");

                    await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=OFF;");
                    await using var transaction = await db.Database.BeginTransactionAsync();
                    await db.Database.ExecuteSqlRawAsync("CREATE TEMP TABLE UserIdMap (OldId TEXT PRIMARY KEY, NewId INTEGER NOT NULL);");
                    await db.Database.ExecuteSqlRawAsync("INSERT INTO UserIdMap (OldId, NewId) SELECT Id, ROW_NUMBER() OVER (ORDER BY rowid) FROM Users;");
                    await db.Database.ExecuteSqlRawAsync("CREATE TABLE Users_new (Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL, Username TEXT NOT NULL, PasswordHash TEXT NOT NULL, Role INTEGER NOT NULL);");
                    await db.Database.ExecuteSqlRawAsync("INSERT INTO Users_new (Id, Name, Username, PasswordHash, Role) SELECT UserIdMap.NewId, Users.Name, Users.Username, Users.PasswordHash, Users.Role FROM Users INNER JOIN UserIdMap ON UserIdMap.OldId = Users.Id;");
                    await db.Database.ExecuteSqlRawAsync("UPDATE Portfolios SET ClientId = (SELECT NewId FROM UserIdMap WHERE OldId = Portfolios.ClientId) WHERE EXISTS (SELECT 1 FROM UserIdMap WHERE OldId = Portfolios.ClientId);");
                    await db.Database.ExecuteSqlRawAsync("UPDATE AdvisorAssignments SET AdvisorId = (SELECT NewId FROM UserIdMap WHERE OldId = AdvisorAssignments.AdvisorId) WHERE EXISTS (SELECT 1 FROM UserIdMap WHERE OldId = AdvisorAssignments.AdvisorId);");
                    await db.Database.ExecuteSqlRawAsync("UPDATE AdvisorAssignments SET ClientId = (SELECT NewId FROM UserIdMap WHERE OldId = AdvisorAssignments.ClientId) WHERE EXISTS (SELECT 1 FROM UserIdMap WHERE OldId = AdvisorAssignments.ClientId);");
                    await db.Database.ExecuteSqlRawAsync("UPDATE Orders SET ClientId = (SELECT NewId FROM UserIdMap WHERE OldId = Orders.ClientId) WHERE EXISTS (SELECT 1 FROM UserIdMap WHERE OldId = Orders.ClientId);");
                    await db.Database.ExecuteSqlRawAsync("UPDATE Orders SET ApprovedBy = (SELECT NewId FROM UserIdMap WHERE OldId = Orders.ApprovedBy) WHERE EXISTS (SELECT 1 FROM UserIdMap WHERE OldId = Orders.ApprovedBy);");
                    await db.Database.ExecuteSqlRawAsync("UPDATE PriceAlerts SET ClientId = (SELECT NewId FROM UserIdMap WHERE OldId = PriceAlerts.ClientId) WHERE EXISTS (SELECT 1 FROM UserIdMap WHERE OldId = PriceAlerts.ClientId);");
                    await db.Database.ExecuteSqlRawAsync("UPDATE Notifications SET ClientId = (SELECT NewId FROM UserIdMap WHERE OldId = Notifications.ClientId) WHERE EXISTS (SELECT 1 FROM UserIdMap WHERE OldId = Notifications.ClientId);");
                    await db.Database.ExecuteSqlRawAsync("DROP TABLE Users;");
                    await db.Database.ExecuteSqlRawAsync("ALTER TABLE Users_new RENAME TO Users;");
                    await db.Database.ExecuteSqlRawAsync("CREATE UNIQUE INDEX IX_Users_Id ON Users (Id);");
                    await db.Database.ExecuteSqlRawAsync("CREATE UNIQUE INDEX IX_Users_Username ON Users (Username);");
                    await db.Database.ExecuteSqlRawAsync("DROP TABLE UserIdMap;");
                    await transaction.CommitAsync();
                }
                finally
                {
                    await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=ON;");
                    if (shouldOpen) await db.Database.CloseConnectionAsync();
                }
            }
            else if (db.Database.ProviderName == "Microsoft.EntityFrameworkCore.SqlServer")
            {
                var connection = db.Database.GetDbConnection();
                var shouldOpen = connection.State != System.Data.ConnectionState.Open;
                if (shouldOpen) await db.Database.OpenConnectionAsync();

                try
                {
                    await using var command = connection.CreateCommand();
                    command.CommandText = "SELECT DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Users' AND COLUMN_NAME = 'Id';";
                    var idType = await command.ExecuteScalarAsync() as string;
                    if (!string.Equals(idType, "int", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("La base SQL Server existente usa un Users.Id no entero. Migre la clave y sus referencias a int antes de iniciar la aplicación.");
                }
                finally
                {
                    if (shouldOpen) await db.Database.CloseConnectionAsync();
                }
            }
        }

        static void AddPositionIfMissing(Portfolio target, string instrumentId, decimal quantity, decimal averageCost)
        {
            if (target.Positions.All(x => x.InstrumentId != instrumentId))
                target.Positions.Add(new Position { InstrumentId = instrumentId, Quantity = quantity, AverageCost = averageCost });
        }
    }
}
