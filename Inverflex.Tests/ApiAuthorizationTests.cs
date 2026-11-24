using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Inverflex.Model;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Inverflex.Tests;

public sealed class ApiAuthorizationTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    public ApiAuthorizationTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task AdminCanCreateUsersAndInstruments()
    {
        using var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/v1/auth/token", new { username = "admin", password = "Demo123!" });
        login.EnsureSuccessStatusCode();
        var token = await login.Content.ReadFromJsonAsync<TokenDto>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.AccessToken);

        var username = $"client-{Guid.NewGuid():N}";
        var name = "Cliente nuevo";
        var createUser = await client.PostAsJsonAsync("/api/v1/admin/users", new { name, username, password = "NewPassword123!", role = UserRole.Cliente });
        Assert.Equal(HttpStatusCode.Created, createUser.StatusCode);
        using var user = await System.Text.Json.JsonDocument.ParseAsync(await createUser.Content.ReadAsStreamAsync());
        Assert.Equal(name, user.RootElement.GetProperty("name").GetString());
        Assert.Equal(username, user.RootElement.GetProperty("username").GetString());
        Assert.True(user.RootElement.GetProperty("id").GetInt32() > 0);
        Assert.False(user.RootElement.TryGetProperty("passwordHash", out _));

        var symbol = $"T{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var sector = $"Sector-{Guid.NewGuid():N}";
        var createInstrument = await client.PostAsJsonAsync("/api/v1/admin/instruments", new
        {
            symbol,
            name = "Test Instrument",
            type = InstrumentType.Accion,
            sector,
            referencePrice = 123.45m,
            currency = "USD"
        });
        Assert.Equal(HttpStatusCode.Created, createInstrument.StatusCode);
        using var instrument = await System.Text.Json.JsonDocument.ParseAsync(await createInstrument.Content.ReadAsStreamAsync());
        Assert.Equal(symbol, instrument.RootElement.GetProperty("symbol").GetString());
        Assert.Equal(123.45m, instrument.RootElement.GetProperty("referencePrice").GetDecimal());

        var catalog = await client.GetStringAsync($"/api/v1/instruments?pageSize=100&sector={Uri.EscapeDataString(sector)}");
        Assert.Contains(symbol, catalog);
    }

    [Fact]
    public async Task AdminEndpointRejectsAnonymousRequest()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/admin/users");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ClientTokenCannotAccessAdminEndpoint()
    {
        using var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/v1/auth/token", new { username = "cliente1", password = "Demo123!" });
        login.EnsureSuccessStatusCode();
        var token = await login.Content.ReadFromJsonAsync<TokenDto>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.AccessToken);
        var response = await client.GetAsync("/api/v1/admin/users");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task LowValuePurchaseIsCreatedAsExecutedThroughApi()
    {
        using var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/v1/auth/token", new { username = "JOBANDO", password = "Demo123!" });
        login.EnsureSuccessStatusCode();
        var token = await login.Content.ReadFromJsonAsync<TokenDto>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.AccessToken);

        var response = await client.PostAsJsonAsync("/api/v1/orders", new { instrumentId = "AAPL", side = OrderSide.Compra, quantity = 1m });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var order = await System.Text.Json.JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.True(order.RootElement.GetProperty("totalAmount").GetDecimal() < 50_000m);
        Assert.Equal(nameof(OrderStatus.Ejecutada), order.RootElement.GetProperty("status").GetString());
        Assert.Matches(@"^\d{2}-\d{2}-\d{4} \d{2}:\d{2}:\d{2}$", order.RootElement.GetProperty("createdAt").GetString());
    }

    [Fact]
    public async Task AdvisorTokenCanAccessAdvisorEndpoints()
    {
        using var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/v1/auth/token", new { username = "MAMORALES", password = "Demo123!" });
        login.EnsureSuccessStatusCode();
        var token = await login.Content.ReadFromJsonAsync<TokenDto>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.AccessToken);
        var response = await client.GetAsync("/api/v2/advisor/orders/pending");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var orders = await System.Text.Json.JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Contains(orders.RootElement.EnumerateArray(), order =>
            order.GetProperty("status").GetString() == "PendienteAprobacion");
    }

    [Fact]
    public async Task SwaggerPublishesRoleGroupsAndProblemDetailsSchema()
    {
        using var client = _factory.CreateClient();
        var clientDocs = await client.GetStringAsync("/swagger/cliente/swagger.json");
        var advisorDocs = await client.GetStringAsync("/swagger/asesor/swagger.json");
        var adminDocs = await client.GetStringAsync("/swagger/admin/swagger.json");
        Assert.Contains("application/problem+json", clientDocs);
        Assert.Contains("api/v{version}", clientDocs);
        Assert.Contains("approve", advisorDocs);
        Assert.Contains("assignments", adminDocs);

        foreach (var docs in new[] { clientDocs, advisorDocs, adminDocs })
        {
            Assert.Contains("/api/v{version}/auth/token", docs);
            Assert.Contains("/api/v{version}/auth/revoke", docs);
        }
    }

    private sealed record TokenDto(string AccessToken);
}

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _keepAlive = new($"Data Source=inverflex-test-{Guid.NewGuid():N};Mode=Memory;Cache=Shared");
    private readonly string _database;

    public ApiFactory()
    {
        _database = _keepAlive.ConnectionString;
        _keepAlive.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Investments"] = _database,
            ["ConnectionStrings:Redis"] = "localhost:6379",
            ["Jwt:SigningKey"] = "local-demo-only-change-before-deployment-32-bytes-min",
            ["Jwt:Issuer"] = "InverFlex",
            ["Jwt:Audience"] = "InverFlex.Clients"
        }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDistributedCache>();
            services.AddDistributedMemoryCache();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _keepAlive.Dispose();
    }
}
