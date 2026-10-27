using CongDoan.Api.Identity;
using CongDoan.Api.Shared;
using CongDoan.Application.Shared;
using CongDoan.Infrastructure;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddPortalAuth(builder.Configuration);
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(o => o.InvalidModelStateResponseFactory = ctx =>
    {
        var message = string.Join(" ", ctx.ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).Where(m => m.Length > 0));
        return new BadRequestObjectResult(ApiEnvelope<object>.Fail(new ApiError("validation_error",
            message.Length > 0 ? message : "Dữ liệu gửi lên không hợp lệ.", ctx.HttpContext.TraceIdentifier)));
    });

var app = builder.Build();

if (app.Configuration.GetValue("Database:MigrateOnStartup", false))
    await MigrationRunner.MigrateAllAsync(app.Services);
await MigrationRunner.SeedAsync(app.Services, app.Configuration);

app.UseMiddleware<RequestIdMiddleware>();
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
