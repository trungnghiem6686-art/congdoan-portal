using CongDoan.Api.Shared;
using CongDoan.Application.Shared;
using CongDoan.Infrastructure.Shared;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<IClock, SystemClock>();

var app = builder.Build();
app.UseMiddleware<RequestIdMiddleware>();
app.UseMiddleware<ExceptionMiddleware>();

app.MapGet("/api/health", () => Results.Ok(ApiEnvelope<string>.Ok("ok")));

app.Run();

public partial class Program;
