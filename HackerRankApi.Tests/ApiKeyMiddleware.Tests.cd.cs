using HackerRankApi.Constants;
using HackerRankApi.Middlewares;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace HackerRankApi.Tests;

public class ApiKeyMiddlewareTests
{
    private readonly Mock<IApplicationBuilder> _mockApp;
    private readonly Mock<ILogger<Program>> _mockLogger;
    private readonly WebApplicationBuilder _builder;

    public ApiKeyMiddlewareTests()
    {
        _mockApp = new Mock<IApplicationBuilder>();
        _mockLogger = new Mock<ILogger<Program>>();

        // Setup Service Provider for the Logger
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider
            .Setup(x => x.GetService(typeof(ILogger<Program>)))
            .Returns(_mockLogger.Object);

        // WebApplicationBuilder is sealed, so we create a real one for the test
        var builderOptions = new WebApplicationOptions();
        _builder = WebApplication.CreateBuilder(builderOptions);
        
        // Inject our mock configuration into the builder
        _builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            { ConfigurationConstants.ApiKeyConfigOptionName, "Valid-Api-Key" }
        });
    }

    [Fact]
    public async Task Invoke_ValidKey_ReturnsNext()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers[ConfigurationConstants.ApiKeyHeaderName] = "Valid-Api-Key";
        context.RequestServices = CreateMockServiceProvider();
        
        RequestDelegate next = innerContext => 
        {
            innerContext.Response.StatusCode = 200;
            return Task.CompletedTask;
        };

        // Capture the middleware logic
        RequestDelegate? capturedMiddleware = null;
        _mockApp.Setup(x => x.Use(It.IsAny<Func<RequestDelegate, RequestDelegate>>()))
            .Callback<Func<RequestDelegate, RequestDelegate>>(func => 
            {
                capturedMiddleware = func(next);
            });

        // Act
        _mockApp.Object.RegisterApiKeyMiddleware(_builder, false);
        await capturedMiddleware!(context);

        // Assert
        Assert.Equal(200, context.Response.StatusCode);
    }

    [Fact]
    public async Task Invoke_MissingKey_Returns401Unauthorized()
    {
        // Arrange
        var context = new DefaultHttpContext
        {
            // Missing Header
            RequestServices = CreateMockServiceProvider(),
            Response =
            {
                Body = new MemoryStream() // Capture response body
            }
        };

        RequestDelegate next = _ => Task.CompletedTask;
        
        RequestDelegate? capturedMiddleware = null;
        _mockApp.Setup(x => x.Use(It.IsAny<Func<RequestDelegate, RequestDelegate>>()))
            .Callback<Func<RequestDelegate, RequestDelegate>>(func => capturedMiddleware = func(next));

        // Act
        _mockApp.Object.RegisterApiKeyMiddleware(_builder, false);
        await capturedMiddleware!(context);

        // Assert
        Assert.Equal(401, context.Response.StatusCode);
        _mockLogger.VerifyLog(LogLevel.Warning, "Unauthorized: API Key missing.");
    }

    [Fact]
    public async Task Invoke_InvalidKey_Returns401Unauthorized()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers[ConfigurationConstants.ApiKeyHeaderName] = "Wrong-Key";
        context.RequestServices = CreateMockServiceProvider();

        RequestDelegate next = _ => Task.CompletedTask;

        RequestDelegate? capturedMiddleware = null;
        _mockApp.Setup(x => x.Use(It.IsAny<Func<RequestDelegate, RequestDelegate>>()))
            .Callback<Func<RequestDelegate, RequestDelegate>>(func => capturedMiddleware = func(next));

        // Act
        _mockApp.Object.RegisterApiKeyMiddleware(_builder, false);
        await capturedMiddleware!(context);

        // Assert
        Assert.Equal(401, context.Response.StatusCode);
        _mockLogger.VerifyLog(LogLevel.Warning, "Unauthorized: Invalid API Key provided.");
    }

    [Fact]
    public async Task Invoke_SwaggerInDevelopment_BypassesKey()
    {
        var context = new DefaultHttpContext
        {
            Request =
            {
                Path = "/swagger/index.html"
            },
            RequestServices = CreateMockServiceProvider()
        };

        var nextCalled = false;
        RequestDelegate next = _ => 
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        RequestDelegate? capturedMiddleware = null;
        _mockApp.Setup(x => x.Use(It.IsAny<Func<RequestDelegate, RequestDelegate>>()))
            .Callback<Func<RequestDelegate, RequestDelegate>>(func => capturedMiddleware = func(next));

        _mockApp.Object.RegisterApiKeyMiddleware(_builder, true); // IsDevelopment = true
        await capturedMiddleware!(context);

        Assert.True(nextCalled);
    }

    private IServiceProvider CreateMockServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_mockLogger.Object);
        return services.BuildServiceProvider();
    }
}

// Helper Extension to verify Logger calls
public static class LoggerExtensions
{
    public static void VerifyLog<T>(this Mock<ILogger<T>> loggerMock, LogLevel level, string message)
    {
        loggerMock.Verify(
            x => x.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(message)),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }
}