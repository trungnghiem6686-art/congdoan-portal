using CongDoan.Api.Identity;
using CongDoan.Api.Shared;
using CongDoan.Application.Shared;
using CongDoan.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Runs as a Windows Service when started by the service manager (no-op otherwise).
builder.Host.UseWindowsService();
// Rolling daily log files when Logging:File:Path is set (e.g. D:\congdoan-data\logs\api-.log); levels follow Logging:LogLevel.
if (builder.Configuration["Logging:File:Path"] is { Length: > 0 } logPath)
{
    builder.Logging.AddSerilog(new Serilog.LoggerConfiguration()
        .MinimumLevel.Verbose()
        .WriteTo.File(logPath, rollingInterval: Serilog.RollingInterval.Day, retainedFileCountLimit: 30,
            fileSizeLimitBytes: 50 * 1024 * 1024, rollOnFileSizeLimit: true)
        .CreateLogger(), dispose: true);
}

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddPortalAuth(builder.Configuration);
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        o.JsonSerializerOptions.Converters.Add(new CongDoan.Api.Shared.UtcDateTimeConverter());
    })
    .ConfigureApiBehaviorOptions(o => o.InvalidModelStateResponseFactory = ctx =>
    {
        var message = string.Join(" ", ctx.ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).Where(m => m.Length > 0));
        return new BadRequestObjectResult(ApiEnvelope<object>.Fail(new ApiError("validation_error",
            message.Length > 0 ? message : "Dữ liệu gửi lên không hợp lệ.", ctx.HttpContext.TraceIdentifier)));
    });

// IIS (ARR) terminates TLS and forwards to Kestrel on localhost: trust its X-Forwarded-* headers only.
builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
    o.ForwardLimit = 1;
    foreach (var ip in builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? Array.Empty<string>())
        o.KnownProxies.Add(System.Net.IPAddress.Parse(ip));
});

var app = builder.Build();

if (app.Configuration.GetValue("Database:MigrateOnStartup", false))
    await MigrationRunner.MigrateAllAsync(app.Services);
await MigrationRunner.SeedAsync(app.Services, app.Configuration);

app.UseForwardedHeaders();
app.UseMiddleware<RequestIdMiddleware>();
app.Use(async (ctx, next) =>
{
    var h = ctx.Response.Headers;
    h.XContentTypeOptions = "nosniff";
    h["Referrer-Policy"] = "strict-origin-when-cross-origin";
    h.XFrameOptions = "SAMEORIGIN";
    await next();
});
app.UseMiddleware<ExceptionMiddleware>();
app.UseAntiforgeryForApi();
app.UseRateLimiter();
app.UsePasswordChangeGate();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/health", () => Results.Ok(ApiEnvelope<string>.Ok("ok")));
app.MapControllers();

app.Run();

public partial class Program;
