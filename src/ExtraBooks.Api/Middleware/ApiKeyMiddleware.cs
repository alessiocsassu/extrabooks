using System.Security.Cryptography;
using System.Text;

namespace ExtraBooks.Api.Middleware;

public sealed class ApiKeyMiddleware
{
    private const string HeaderName = "X-Api-Key";
    private readonly RequestDelegate _next;
    private readonly byte[] _expectedKey;

    public ApiKeyMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _expectedKey = Encoding.UTF8.GetBytes(configuration["ApiKey"]!);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await _next(context);
            return;
        }

        var provided = context.Request.Headers[HeaderName].ToString();
        var providedBytes = Encoding.UTF8.GetBytes(provided);
        if (!CryptographicOperations.FixedTimeEquals(providedBytes, _expectedKey))
        {
            await Results.Problem(statusCode: StatusCodes.Status401Unauthorized,
                title: "API key mancante o non valida").ExecuteAsync(context);
            return;
        }

        await _next(context);
    }
}
