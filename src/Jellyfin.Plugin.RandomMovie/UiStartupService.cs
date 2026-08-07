using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.RandomMovie;

public class UiStartupService : IHostedService
{
    private readonly IApplicationPaths _applicationPaths;
    private readonly UiInjector _uiInjector;
    private readonly ILogger<UiStartupService> _logger;
    private volatile bool _injected;

    public UiStartupService(
        IApplicationPaths applicationPaths,
        ILogger<UiStartupService> logger,
        ILoggerFactory loggerFactory)
    {
        _applicationPaths = applicationPaths;
        _logger = logger;
        _uiInjector = new UiInjector(loggerFactory.CreateLogger<UiInjector>());
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_injected)
        {
            return Task.CompletedTask;
        }

        _injected = true;
        return Task.Run(() =>
        {
            if (string.IsNullOrEmpty(_applicationPaths.WebPath))
            {
                _logger.LogWarning("RandomMovie: web directory path is empty, cannot inject UI.");
                return;
            }

            _uiInjector.Inject(_applicationPaths.WebPath);
        }, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}