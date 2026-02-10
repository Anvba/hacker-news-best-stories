using HackerRankApi.Constants;
using HackerRankApi.Services.Interfaces;
using Polly.CircuitBreaker;

namespace HackerRankApi.Endpoints;

/// <summary>
/// Provides extension methods to configure and map API routes for the HackerRank application.
/// </summary>
public static class ConfigureEndpoints
{
    /// <summary>
    /// Registers the application's HTTP endpoints.
    /// </summary>
    /// <param name="app">The <see cref="WebApplication"/> instance to configure.</param>
    public static void RegisterRoutes(this WebApplication app)
    {
        app.MapGet("/api/best", (int? n, IHackerNewsService service, ILogger<Program> logger) => {
                
                switch (n)
                {
                    // Check if the required parameter 'n' is missing
                    case null:
                        logger.LogWarning("Validation failed for n=NULL");
                        return Results.BadRequest(new
                        {
                            error = "Parameter 'n' is required." 
                        });

                    // Ensure 'n' stays within a reasonable range (1-200)
                    case < 1 or > 200:
                        logger.LogWarning("Validation failed for n={Value}", n.Value);
                        return Results.BadRequest(new 
                        { 
                            error = "Invalid range for 'n'.", 
                            detail = "The number of stories (n) must be between 1 and 200.",
                            receivedValue = n.Value 
                        });
                    default:
                        try 
                        {
                            // Fetch the processed stories from the service layer
                            var result = service.GetBestStoriesAsync(n.Value);
                            return Results.Ok(result);
                        }
                        // Handle Polly Circuit Breaker state. If the downstream Hacker News API 
                        // is failing repeatedly, the circuit opens to prevent further load.
                        catch (HttpIOException ex) when (ex.InnerException is BrokenCircuitException)
                        {
                            logger.LogCritical("Circuit Breaker is OPEN. Downstream API is failing.");
                            // Return 503 Service Unavailable to indicate a temporary issue
                            return Results.StatusCode(503); 
                        }
                        // Catch-all for unexpected failures
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Uncaught exception while fetching best stories for n={N}", n.Value);
                            return Results.Problem(ex.Message);
                        }
                }
            })
            // Apply the rate limiting policy defined in constants to prevent API abuse
            .RequireRateLimiting(ConfigurationConstants.RateLimiterPoliceName)
            // Metadata for Swagger/OpenAPI documentation
            .WithName("GetBestStories")
            .WithDescription("Retrieves the top N best stories from Hacker News, sorted by score.");
    }
}