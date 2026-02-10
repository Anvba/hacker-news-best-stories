using System.Diagnostics;
using HackerRankApi.Constants;
using HackerRankApi.Models;

namespace HackerRankApi.Middlewares;

/// <summary>
/// Provides extension methods for registering custom API Key authentication middleware.
/// </summary>
public static class ApiKeyMiddleware
{
    private const string SwaggerPageUri = "/swagger";

    /// <summary>
    /// Registers an inline middleware that validates the presence and accuracy of an API Key in request headers.
    /// </summary>
    /// <param name="app">The <see cref="IApplicationBuilder"/> to add the middleware to.</param>
    /// <param name="builder">The <see cref="WebApplicationBuilder"/> used to access configuration settings.</param>
    /// <param name="isDevelopment">A boolean indicating if the application is running in the Development environment.</param>
    public static void RegisterApiKeyMiddleware(this IApplicationBuilder app, WebApplicationBuilder builder, bool isDevelopment)
    {
        app.Use(async (context, next) =>
        {
            // Resolve logger from the request services container
            var logger = context.RequestServices.GetService<ILogger<Program>>();

            // Structured logging scope helps correlate all logs within a single request
            using (logger?.BeginScope("Request {RequestId}, Path {Path}, Method {Method}",
                       context.TraceIdentifier,
                       context.Request.Path,
                       context.Request.Method))
            {
                var sw = Stopwatch.StartNew();
                logger?.LogInformation("Processing request started.");

                // Bypass authentication for Swagger UI if in Development mode.
                // This allows developers to view documentation without manually adding headers.
                if (isDevelopment && context.Request.Path.StartsWithSegments(SwaggerPageUri))
                {
                    await next();
                    return;
                }

                // Check if the specific API Key header exists in the request.
                // ConfigurationConstants.ApiKeyHeaderName typically maps to "X-API-KEY".
                if (!context.Request.Headers.TryGetValue(ConfigurationConstants.ApiKeyHeaderName, out var extractedApiKey))
                {
                    logger?.LogWarning("Unauthorized: API Key missing.");
                    context.Response.StatusCode = 401; // Unauthorized
                    await context.Response.WriteAsJsonAsync
                    (
                        new ErrorResponse("API Key missing"), 
                        HackerRankBestStoryJsonContext.Default.ErrorResponse
                    );
                    return;
                }

                // Validate the extracted key against the value stored in appsettings.json or Environment Variables.
                var apiKey = builder.Configuration.GetValue<string>(ConfigurationConstants.ApiKeyConfigOptionName);
                if (string.IsNullOrEmpty(apiKey) || apiKey != extractedApiKey)
                {
                    logger?.LogWarning("Unauthorized: Invalid API Key provided.");
                    
                    context.Response.StatusCode = 401; // Unauthorized
                    await context.Response.WriteAsJsonAsync
                    (
                        new ErrorResponse("Invalid API Key"), 
                        HackerRankBestStoryJsonContext.Default.ErrorResponse
                    );
                    return;
                }

                // Validation passed. Proceed to the next middleware in the pipeline (e.g., Routing, Controllers).
                await next();
                
                // Logging performance metrics after the response has been generated.
                sw.Stop();
                logger?.LogInformation("Request processed in {ElapsedMs}ms with Status {StatusCode}", sw.ElapsedMilliseconds, context.Response.StatusCode);
            }
        });
    }
}