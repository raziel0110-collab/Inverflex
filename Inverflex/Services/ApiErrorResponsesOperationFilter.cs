using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Inverflex.Services;

public sealed class ApiErrorResponsesOperationFilter : IOperationFilter
{
    private static readonly IReadOnlyDictionary<string, string> Errors = new Dictionary<string, string>
    {
        ["400"] = "Invalid request or query parameters.",
        ["401"] = "Missing, expired, invalid, or revoked JWT.",
        ["403"] = "The authenticated role or client assignment does not allow this operation.",
        ["404"] = "The requested resource does not exist.",
        ["409"] = "State or concurrency conflict, including an already processed order.",
        ["429"] = "The per-user gateway limit of 200 requests per minute was exceeded.",
        ["500"] = "Unexpected server error.",
        ["503"] = "A required distributed dependency or market-price service is unavailable."
    };

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        foreach (var error in Errors)
            operation.Responses.TryAdd(error.Key, new OpenApiResponse
            {
                Description = error.Value,
                Content =
                {
                    ["application/problem+json"] = new OpenApiMediaType { Schema = ProblemSchema() }
                }
            });
    }

    private static OpenApiSchema ProblemSchema() => new()
    {
        Type = "object",
        Required = new HashSet<string> { "status", "title" },
        Properties = new Dictionary<string, OpenApiSchema>
        {
            ["type"] = new() { Type = "string", Description = "URI que identifica el tipo de problema." },
            ["title"] = new() { Type = "string", Description = "Resumen breve del problema." },
            ["status"] = new() { Type = "integer", Format = "int32" },
            ["detail"] = new() { Type = "string", Description = "Detalle seguro para el cliente." },
            ["instance"] = new() { Type = "string", Description = "Ruta de la solicitud fallida." }
        }
    };
}
