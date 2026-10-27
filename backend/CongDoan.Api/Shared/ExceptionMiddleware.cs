using System.Text.Json;
using CongDoan.Application.Shared;

namespace CongDoan.Api.Shared;

/// <summary>Turns unhandled exceptions into the standard envelope; never leaks messages or stack traces.</summary>
public sealed class ExceptionMiddleware
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex) when (!context.Response.HasStarted)
        {
            _logger.LogError(ex, "Unhandled exception for request {RequestId}", context.TraceIdentifier);
            context.Response.Clear();
            context.Response.Headers[RequestIdMiddleware.Header] = context.TraceIdentifier;
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json; charset=utf-8";
            var envelope = ApiEnvelope<object>.Fail(new ApiError("internal_error",
                "Đã có lỗi xảy ra. Vui lòng thử lại sau.", context.TraceIdentifier));
            await JsonSerializer.SerializeAsync(context.Response.Body, envelope, Json);
        }
    }
}
