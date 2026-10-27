using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CongDoan.Infrastructure.Shared;

/// <summary>Runs one scoped job on a fixed interval; a failing tick is logged and retried on the next one.</summary>
public abstract class PeriodicWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger _logger;

    protected PeriodicWorker(IServiceScopeFactory scopes, ILogger logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected abstract TimeSpan Interval { get; }

    protected abstract Task RunAsync(IServiceProvider services, CancellationToken ct);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var scope = _scopes.CreateScope();
                await RunAsync(scope.ServiceProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Worker} tick failed", GetType().Name);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
