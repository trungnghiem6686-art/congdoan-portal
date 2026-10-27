using System.Text.Json;
using CongDoan.Api.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace CongDoan.Shared.Tests;

public class ExceptionMiddlewareTests
{
    [Fact]
    public async Task ExceptionMiddleware_ReturnsEnvelopeWithRequestId_NoStackTrace()
    {
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();
        var requestId = new RequestIdMiddleware(async c =>
        {
            var errors = new ExceptionMiddleware(_ => throw new InvalidOperationException("secret detail"),
                NullLogger<ExceptionMiddleware>.Instance);
            await errors.InvokeAsync(c);
        });

        await requestId.InvokeAsync(ctx);

        Assert.Equal(500, ctx.Response.StatusCode);
        var headerId = ctx.Response.Headers["X-Request-Id"].ToString();
        Assert.False(string.IsNullOrWhiteSpace(headerId));
        ctx.Response.Body.Position = 0;
        var body = await new StreamReader(ctx.Response.Body).ReadToEndAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
        var error = doc.RootElement.GetProperty("error");
        Assert.Equal("internal_error", error.GetProperty("code").GetString());
        Assert.Equal(headerId, error.GetProperty("requestId").GetString());
        Assert.DoesNotContain("secret detail", body);
        Assert.DoesNotContain("at CongDoan", body);
    }

    [Fact]
    public async Task RequestIdMiddleware_Reuses_Incoming_Header()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["X-Request-Id"] = "abc123";
        var mw = new RequestIdMiddleware(_ => Task.CompletedTask);

        await mw.InvokeAsync(ctx);

        Assert.Equal("abc123", ctx.Response.Headers["X-Request-Id"].ToString());
        Assert.Equal("abc123", ctx.TraceIdentifier);
    }
}
