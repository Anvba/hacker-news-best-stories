using HackerRankApi.Constants;
using HackerRankApi.Models;
using HackerRankApi.Services;
using HackerRankApi.Services.Interfaces;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing.Constraints;
using Microsoft.OpenApi;

namespace HackerRankApi.Configuration;

public static class ServiceConfiguration
{
    public static void ConfigureApiRateLimiting(this WebApplicationBuilder builder)
    {
        var rateSettings = builder.Configuration.GetSection(ConfigurationConstants.RateLimitSettingsSectionName);
        
        builder.Services.AddRateLimiter(options =>
        {
            options.AddFixedWindowLimiter(ConfigurationConstants.RateLimiterPoliceName, opt =>
            {
                opt.PermitLimit = rateSettings.GetValue(ConfigSectionRateLimitSettings.PermitLimit, 100);
                opt.Window = TimeSpan.FromSeconds(rateSettings.GetValue(ConfigSectionRateLimitSettings.WindowSeconds, 60));
                opt.QueueLimit = rateSettings.GetValue(ConfigSectionRateLimitSettings.QueueLimit, 10);
            });
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        });
    }

    public static void ConfigureHackerNewsHttpClientResilienceOptions(this WebApplicationBuilder builder)
    {
        var hnSettings = builder.Configuration.GetSection(ConfigurationConstants.HackerNewsSettingsSectionName);
        var resSettings = builder.Configuration.GetSection(ConfigurationConstants.ResilienceOptionsSectionName);

        builder.Services.AddTransient<IHackerNewsService, ImmutableArrayHackerNewsService>();
        builder.Services.AddHttpClient(nameof(ImmutableArrayHackerNewsService), client => {
                client.BaseAddress = new Uri(hnSettings.GetValue<string>(ConfigSectionOptionsHackerNewsSettings.BaseUrl) ?? ConfigurationConstants.DefaultHackerNewsBaseUrl);
            })
            .AddStandardResilienceHandler(options => {
                options.Retry.MaxRetryAttempts = resSettings.GetValue(ConfigSectionResilienceOptions.RetryCount, 3);
                options.Retry.Delay = TimeSpan.FromSeconds(resSettings.GetValue(ConfigSectionResilienceOptions.BackoffSeconds, 2));
                options.CircuitBreaker.FailureRatio = resSettings.GetValue(ConfigSectionResilienceOptions.CircuitBreakerThreshold, 0.5);
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(resSettings.GetValue(ConfigSectionResilienceOptions.SamplingDurationSeconds, 30));
                options.CircuitBreaker.MinimumThroughput = 10; 
            });
    }

    public static void ConfigureJsonOptions(this WebApplicationBuilder builder)
    {
        builder.Services.ConfigureHttpJsonOptions(options => {
            options.SerializerOptions.TypeInfoResolverChain.Insert(0, HackerRankBestStoryJsonContext.Default);
        });
    }

    public static void ConfigureRoutingOptions(this WebApplicationBuilder builder)
    {
        builder.Services.Configure<RouteOptions>(options =>
            options.SetParameterPolicy<RegexInlineRouteConstraint>("regex"));
    }

    public static void ConfigureSwaggerOptions(this WebApplicationBuilder builder)
    {
        const string securityDefinitionName = "ApiKey";
        
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = "Hacker News Ultra-Fast API", Version = "v1" });

            // Define the API Key security scheme
            c.AddSecurityDefinition(securityDefinitionName, new OpenApiSecurityScheme
            {
                Description = $"API Key needed to access the endpoints. {ConfigurationConstants.ApiKeyHeaderName}: Your_Key",
                In = ParameterLocation.Header,
                Name = ConfigurationConstants.ApiKeyHeaderName,
                Type = SecuritySchemeType.ApiKey,
                Scheme = ConfigurationConstants.SecurityDefinitionApiKeySchemaName
            });

            // Apply the security requirement globally in Swagger UI
            c.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecuritySchemeReference
                    (
                        securityDefinitionName,
                        document
                    ),
                    []
                }
            });
        });
    }
}
