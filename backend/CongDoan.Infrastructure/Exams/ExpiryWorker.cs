using CongDoan.Application.Exams;
using CongDoan.Infrastructure.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CongDoan.Infrastructure.Exams;

public sealed class ExpiryWorker : PeriodicWorker
{
    public ExpiryWorker(IServiceScopeFactory scopes, ILogger<ExpiryWorker> logger) : base(scopes, logger) { }

    protected override TimeSpan Interval => TimeSpan.FromSeconds(10);

    protected override Task RunAsync(IServiceProvider services, CancellationToken ct) =>
        services.GetRequiredService<ExpirySweeper>().RunOnceAsync(ct);
}
