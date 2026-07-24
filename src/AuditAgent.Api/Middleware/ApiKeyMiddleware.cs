using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace AuditAgent.Api.Middleware;

/// <summary>
/// Middleware de autenticacion por API Key.
/// Los endpoints de escritura (POST) requieren API Key.
/// Los endpoints de lectura (GET) son publicos en modo demo,
/// pero se pueden proteger con la misma config.
/// 
/// Se configura en appsettings.json:
///   "Security": { "ApiKey": "tu-clave-secreta-aqui" }
/// 
/// El cliente envia: X-API-Key: tu-clave-secreta-aqui
/// </summary>
public class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _apiKey;

    public ApiKeyMiddleware(RequestDelegate next, string apiKey)
    {
        _next = next;
        _apiKey = apiKey;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // GET = lectura: no requiere API Key (configurable)
        if (context.Request.Method == "GET")
        {
            await _next(context);
            return;
        }

        // POST = escritura: requiere API Key
        if (!context.Request.Headers.TryGetValue("X-API-Key", out var providedKey) ||
            string.IsNullOrEmpty(providedKey) ||
            !CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(providedKey!),
                System.Text.Encoding.UTF8.GetBytes(_apiKey)))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "API Key invalida o ausente." });
            return;
        }

        await _next(context);
    }
}

/// <summary>
/// Extension method para registrar el middleware facilmente.
/// </summary>
public static class ApiKeyMiddlewareExtensions
{
    public static IApplicationBuilder UseApiKeyAuthentication(
        this IApplicationBuilder builder, string apiKey)
    {
        if (string.IsNullOrEmpty(apiKey))
            throw new ArgumentException("API Key no configurada. Agregala en appsettings.json", nameof(apiKey));

        return builder.UseMiddleware<ApiKeyMiddleware>(apiKey);
    }
}
