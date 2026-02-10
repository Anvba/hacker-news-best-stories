using HackerRankApi.Constants;

namespace HackerRankApi.Services;

/// <summary>
/// A hosted background service responsible for periodically refreshing the Hacker News "Best Stories" cache.
/// This ensures the application has data ready in memory, reducing latency for incoming requests.
/// </summary>
/// <remarks>
/// Inheriting from <see cref="BackgroundService"/> allows this class to start automatically 
/// when the application host starts and run until it is shut down.
/// </remarks>
public class HostedHackerNewsService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<HostedHackerNewsService> logger) : BackgroundService
{
    /// <summary>
    /// The core execution logic for the background worker.
    /// </summary>
    /// <param name="stoppingToken">Triggered when the host is shutting down.</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Retrieve the cache refresh interval from appsettings.json
        // Using ushort ensures we have a positive integer for the duration.
        var cacheDurationMinutes = configuration.GetValue<ushort>(
            $"{ConfigurationConstants.HackerNewsSettingsSectionName}:{ConfigSectionOptionsHackerNewsSettings.CacheDurationMinutes}");
        
        // PeriodicTimer is preferred over Task.Delay in .NET 6+ because it handles
        // 'drift' better, ensuring the timer ticks accurately relative to the start time.
        using PeriodicTimer timer = new(TimeSpan.FromMinutes(cacheDurationMinutes));

        try
        {
            // The 'do-while' ensures the data is loaded immediately upon startup, 
            // then waits for the timer for subsequent updates.
            do
            {
                logger.LogInformation("Refreshing Hacker News cache at: {time}", DateTimeOffset.Now);

                // Call the static LoadBestStories method to fetch data and update the shared state.
                // We create a named HttpClient specifically for this service's configuration.
                await ImmutableArrayHackerNewsService.LoadBestStories(
                    httpClientFactory.CreateClient(nameof(ImmutableArrayHackerNewsService)), 
                    configuration, 
                    logger);
            } 
            // Wait until the next timer tick or until the application is stopped.
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // This is expected when stoppingToken is triggered; we log it as information rather than an error.
            logger.LogInformation("Hacker News background service is stopping.");
        }
        catch (Exception e)
        {
            // Log critical failures that stop the background worker.
            logger.LogError(e, "An unhandled error occurred while loading best-stories.");
            throw;
        }
    }

    /// <summary>
    /// Handles graceful shutdown of the background service.
    /// </summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogDebug("Stopping HostedHackerNewsService...");
        await base.StopAsync(cancellationToken);
    }
}