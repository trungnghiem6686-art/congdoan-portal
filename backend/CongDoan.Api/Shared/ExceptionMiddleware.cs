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
        catch (RowErrorsException ex) when (!context.Response.HasStarted)
        {
            context.Response.Clear();
            await ErrorWriter.WriteAsync(context, ex.Status, ex.Code, ex.Message, new { errors = ex.Errors });
        }
        catch (AppException ex) when (!context.Response.HasStarted)
        {
            context.Response.Clear();
            await ErrorWriter.WriteAsync(context, ex.Status, ex.Code, ex.Message);
        }
        catch (Exception ex) when (!context.Response.HasStarted && IsConcurrency(ex))
        {
            context.Response.Clear();
            await ErrorWriter.WriteAsync(context, StatusCodes.Status409Conflict, "concurrency_conflict",
                "Dữ liệu đã được người khác thay đổi. Vui lòng tải lại trước khi lưu.");
        }
        catch (Exception ex) when (!context.Response.HasStarted)
        {
            _logger.LogError(ex, "Unhandled exception for request {RequestId}", context.TraceIdentifier);
            context.Response.Clear();
            await ErrorWriter.WriteAsync(context, StatusCodes.Status500InternalServerError, "internal_error",
                "Đã có lỗi xảy ra. Vui lòng thử lại sau.");
        }
    }

    // Matched by name so the Api layer does not need a direct EF Core dependency here.
    private static bool IsConcurrency(Exception ex) => ex.GetType().Name == "DbUpdateConcurrencyException";
}
