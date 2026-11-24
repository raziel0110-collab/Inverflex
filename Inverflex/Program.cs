using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Inverflex.Data;
using Inverflex.Repositories;
using Inverflex.Services;
using Inverflex.Queries;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);
var jwtKey = builder.Configuration["Jwt:SigningKey"] ?? throw new InvalidOperationException("Configure Jwt:SigningKey with at least 32 characters.");
if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
    throw new InvalidOperationException("Jwt:SigningKey must contain at least 32 bytes.");

builder.Services.AddControllers(options =>
{
    options.Filters.Add<DeprecatedEndpointFilter>();
    options.ModelBinderProviders.Insert(0, new UtcDateTimeOffsetModelBinderProvider());
}).AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new UtcDateTimeOffsetJsonConverter()));
var databaseProvider = builder.Configuration["Database:Provider"] ?? "Sqlite";
var databaseConnection = builder.Configuration.GetConnectionString("Investments") ?? throw new InvalidOperationException("Configure ConnectionStrings:Investments.");
builder.Services.AddDbContext<InvestmentDbContext>(options =>
{
    if (databaseProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase)) options.UseSqlite(databaseConnection);
    else if (databaseProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase)) options.UseSqlServer(databaseConnection);
    else throw new InvalidOperationException($"Unsupported database provider '{databaseProvider}'.");
});
if (builder.Environment.IsDevelopment())
    builder.Services.AddDistributedMemoryCache();
else
{
    var redisConfig = builder.Configuration.GetSection("Redis");
    var redisEndpoint = redisConfig["Endpoint"] ?? builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
    var redisPassword = redisConfig["Password"] ?? Environment.GetEnvironmentVariable("REDIS_PASSWORD");
    var redisSsl = redisConfig.GetValue<bool>("Ssl");

    var redisOptions = $"{redisEndpoint},ssl={redisSsl},abortConnect={redisConfig.GetValue("AbortOnConnectFail", true)},connectTimeout={redisConfig.GetValue("ConnectTimeout", 5000)},syncTimeout={redisConfig.GetValue("SyncTimeout", 5000)}";
    if (!string.IsNullOrWhiteSpace(redisPassword))
        redisOptions += $",password={redisPassword}";

    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisOptions;
        options.InstanceName = "inverflex:";
    });
}

builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IOrderQueries, OrderQueries>();
builder.Services.AddScoped<IAppUserRepository, AppUserRepository>();
builder.Services.AddScoped<IInstrumentRepository, InstrumentRepository>();
builder.Services.AddScoped<IPortfolioRepository, PortfolioRepository>();
builder.Services.AddScoped<IInvestmentOrderRepository, InvestmentOrderRepository>();
builder.Services.AddScoped<IPriceAlertRepository, PriceAlertRepository>();
builder.Services.AddScoped<IAdvisorAssignmentRepository, AdvisorAssignmentRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();

builder.Services.AddScoped<IMarketPriceService, MockMarketPriceService>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<PortfolioService>();
builder.Services.AddScoped<PortfolioAccessService>();
builder.Services.AddScoped<InstrumentCatalogService>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<AlertEvaluationService>();
builder.Services.AddHostedService<AlertEvaluationWorker>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"],
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:SigningKey"]!)),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30),
        NameClaimType = "userId",
        RoleClaimType = "role"
    };
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var tokenId = context.SecurityToken?.Id;
            if (string.IsNullOrWhiteSpace(tokenId)) { context.Fail("Token sin identificador."); return; }
            var cache = context.HttpContext.RequestServices.GetRequiredService<IDistributedCache>();
            if (await cache.GetStringAsync($"revoked:{tokenId}", context.HttpContext.RequestAborted) is not null)
                context.Fail("Token revocado.");
        }
    };
});
builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    foreach (var group in new[] { "cliente", "asesor", "admin" })
        options.SwaggerDoc(group, new OpenApiInfo { Title = $"InverFlex API - {group}", Version = "v1", Description = "Errores: 400 solicitud inválida, 401 token inválido, 403 acceso denegado, 404 recurso inexistente, 409 conflicto de estado, 429 límite excedido, 503 servicio de precios no disponible." });
    options.DocInclusionPredicate((name, description) =>
        string.Equals(description.GroupName, name, StringComparison.OrdinalIgnoreCase)
        || (description.ActionDescriptor.RouteValues.TryGetValue("controller", out var controller)
            && string.Equals(controller, "Auth", StringComparison.OrdinalIgnoreCase)));
    options.OperationFilter<ApiErrorResponsesOperationFilter>();
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { Name = "Authorization", Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT", In = ParameterLocation.Header });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement { [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = Array.Empty<string>() });
});

var app = builder.Build();
await DatabaseSeeder.InitializeAsync(app.Services);
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/cliente/swagger.json", "Cliente");
        options.SwaggerEndpoint("/swagger/asesor/swagger.json", "Asesor");
        options.SwaggerEndpoint("/swagger/admin/swagger.json", "Admin");
    });
}
app.UseHttpsRedirection();
app.UseMiddleware<ApiExceptionMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

public partial class Program { }
