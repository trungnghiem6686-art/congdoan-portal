using CongDoan.Application.Cms;
using CongDoan.Infrastructure.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CongDoan.Infrastructure.Cms;

public sealed class PublishWorker : PeriodicWorker
{
    public PublishWorker(IServiceScopeFactory scopes, ILogger<PublishWorker> logger) : base(scopes, logger) { }

    protected override TimeSpan Interval => TimeSpan.FromSeconds(30);

    protected override Task RunAsync(IServiceProvider services, CancellationToken ct) =>
        services.GetRequiredService<ScheduledPublisher>().RunOnceAsync(ct);
}
