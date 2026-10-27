using System.Text.Json;
using CongDoan.Application.Shared;

namespace CongDoan.Api.Shared;

public static class ErrorWriter
{
    public static async Task WriteAsync(HttpContext ctx, int status, string code, string message)
    {
        // A policy with several schemes challenges each of them; only the first one writes.
        if (ctx.Response.HasStarted) return;
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/json; charset=utf-8";
        ctx.Response.Headers[RequestIdMiddleware.Header] = ctx.TraceIdentifier;
        var envelope = ApiEnvelope<object>.Fail(new ApiError(code, message, ctx.TraceIdentifier));
        await JsonSerializer.SerializeAsync(ctx.Response.Body, envelope, ExceptionMiddleware.Json);
    }
}
