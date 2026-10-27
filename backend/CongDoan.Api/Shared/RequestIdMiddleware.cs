using System.Text.RegularExpressions;

namespace CongDoan.Api.Shared;

/// <summary>Assigns X-Request-Id (reusing a safe incoming value) and exposes it as TraceIdentifier.</summary>
public sealed partial class RequestIdMiddleware
{
    public const string Header = "X-Request-Id";
    private readonly RequestDelegate _next;

    public RequestIdMiddleware(RequestDelegate next) => _next = next;

    public Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[Header].ToString();
        var id = SafeId().IsMatch(incoming) ? incoming : Guid.NewGuid().ToString("N");
        context.TraceIdentifier = id;
        context.Response.Headers[Header] = id;
        return _next(context);
    }

    [GeneratedRegex("^[A-Za-z0-9-]{1,64}$")]
    private static partial Regex SafeId();
}
