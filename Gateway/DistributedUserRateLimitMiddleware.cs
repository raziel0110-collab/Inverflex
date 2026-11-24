using StackExchange.Redis;

namespace Inverflex.Gateway;

public sealed class DistributedUserRateLimitMiddleware(RequestDelegate next, IConnectionMultiplexer redis, ILogger<DistributedUserRateLimitMiddleware> logger)
{
    private const string IncrementScript = "local n=redis.call('INCR',KEYS[1]); if n==1 then redis.call('EXPIRE',KEYS[1],60); end; return n";

    public async Task InvokeAsync(HttpContext context)
    {
        var isTokenEndpoint = HttpMethods.IsPost(context.Request.Method) && context.Request.Path.Value?.EndsWith("/auth/token", StringComparison.OrdinalIgnoreCase) == true;
        if (!context.Request.Path.StartsWithSegments("/api") || isTokenEndpoint)
        {
            await next(context);
            return;
        }
        if (context.User.Identity?.IsAuthenticated != true)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }
        var userId = context.User.FindFirst("userId")?.Value;
        if (string.IsNullOrWhiteSpace(userId))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }
        var minute = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 60;
        var key = $"inverflex:rate:{userId}:{minute}";
        try
        {
            var count = (long)await redis.GetDatabase().ScriptEvaluateAsync(IncrementScript, [key]);
            context.Response.Headers["RateLimit-Limit"] = "200";
            context.Response.Headers["RateLimit-Remaining"] = Math.Max(0, 200 - count).ToString();
            if (count > 200)
            {
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                return;
            }
        }
        catch (RedisException exception)
        {
            logger.LogError(exception, "Redis no está disponible para el límite distribuido.");
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return;
        }
        await next(context);
    }
}
