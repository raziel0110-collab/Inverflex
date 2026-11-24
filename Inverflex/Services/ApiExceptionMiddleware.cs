using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace Inverflex.Services;

public sealed class ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try { await next(context); }
        catch (Exception exception) when (!context.Response.HasStarted)
        {
            var (status, title) = exception switch
            {
                KeyNotFoundException => (StatusCodes.Status404NotFound, exception.Message),
                UnauthorizedAccessException => (StatusCodes.Status403Forbidden, exception.Message),
                ArgumentException => (StatusCodes.Status400BadRequest, exception.Message),
                InvalidOperationException => (StatusCodes.Status409Conflict, exception.Message),
                _ => (StatusCodes.Status500InternalServerError, "Ocurrió un error inesperado.")
            };
            if (status == StatusCodes.Status500InternalServerError) logger.LogError(exception, "Error no controlado procesando {Path}.", context.Request.Path);
            context.Response.StatusCode = status;
            context.Response.ContentType = "application/problem+json";
            var detail = context.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment() ? exception.Message : null;
            await context.Response.WriteAsync(JsonSerializer.Serialize(new ProblemDetails { Status = status, Title = title, Detail = detail, Instance = context.Request.Path }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        }
    }
}
