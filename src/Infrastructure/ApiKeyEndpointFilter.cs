using Microsoft.Extensions.Options;
using SalesWorkflow.Configuration;
using System.Security.Cryptography;
using System.Text;

namespace SalesWorkflow.Infrastructure;

/// <summary>
/// Endpoint filter that enforces API key authentication via the <c>X-Api-Key</c> header.
/// Returns 401 if the header is missing or the value does not match the configured key.
/// Uses constant-time comparison to prevent timing attacks.
/// </summary>
public class ApiKeyEndpointFilter(IOptions<BackOfficeSettings> options) : IEndpointFilter
{
    private const string ApiKeyHeader = "X-Api-Key";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var configuredKey = options.Value.ApiKey;
        if (string.IsNullOrEmpty(configuredKey))
        {
            // Back-office key not configured — deny all requests to be safe
            return Results.Problem(
                detail: "Back-office API key is not configured on the server.",
                statusCode: 503,
                title: "Service Unavailable");
        }

        if (!context.HttpContext.Request.Headers.TryGetValue(ApiKeyHeader, out var requestKey)
            || string.IsNullOrEmpty(requestKey))
        {
            return Results.Json(
                new { error = "Unauthorized", message = "X-Api-Key header is required." },
                statusCode: 401);
        }

        var configuredBytes = Encoding.UTF8.GetBytes(configuredKey!);
        var requestBytes = Encoding.UTF8.GetBytes(requestKey.ToString());

        // Pad both to equal length before FixedTimeEquals to avoid length-leak
        var maxLen = Math.Max(configuredBytes.Length, requestBytes.Length);
        var a = new byte[maxLen];
        var b = new byte[maxLen];
        configuredBytes.CopyTo(a, 0);
        requestBytes.CopyTo(b, 0);

        if (!CryptographicOperations.FixedTimeEquals(a, b))
        {
            return Results.Json(
                new { error = "Unauthorized", message = "Invalid API key." },
                statusCode: 401);
        }

        return await next(context);
    }
}
