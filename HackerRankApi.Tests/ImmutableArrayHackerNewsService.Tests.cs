using System.Collections.Immutable;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using HackerRankApi.Models;
using HackerRankApi.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;

namespace HackerRankApi.Tests;

public class ImmutableArrayHackerNewsServiceTests : IDisposable
{
    private readonly Mock<ILogger<ImmutableArrayHackerNewsService>> _mockLogger;
    private readonly ImmutableArrayHackerNewsService _service;

    public ImmutableArrayHackerNewsServiceTests()
    {
        _mockLogger = new Mock<ILogger<ImmutableArrayHackerNewsService>>();
        _service = new ImmutableArrayHackerNewsService(_mockLogger.Object);
        ResetStaticStories();
    }

    public void Dispose()
    {
        ResetStaticStories();
    }

    /// <summary>
    /// Since _stories is static, we must reset it via reflection to ensure test isolation.
    /// </summary>
    private void ResetStaticStories()
    {
        var field = typeof(ImmutableArrayHackerNewsService)
            .GetField("_stories", BindingFlags.Static | BindingFlags.NonPublic);
        field?.SetValue(null, ImmutableArray<HackerRankBestStory>.Empty);
    }

    [Fact]
    public void GetBestStoriesAsync_WhenEmpty_ReturnsEmptyEnumerable()
    {
        // Act
        var result = _service.GetBestStoriesAsync(10);

        // Assert
        Assert.Empty(result);
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("There are no best stories")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task LoadBestStories_PopulatesInternalState_AndReturnsSortedResults()
    {
        // Arrange
        var storyIds = new[] { 1, 2 };
        var story1 = new HackerRankBestStory("Story Low", "url1", "user1", 1600000000, 10, 5);
        var story2 = new HackerRankBestStory("Story High", "url2", "user2", 1600000000, 100, 20);

        var handlerMock = new Mock<HttpMessageHandler>();
        
        // Setup responses for the IDs list and then each individual story
        handlerMock.Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = JsonContent.Create(storyIds) })
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = JsonContent.Create(story1) })
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = JsonContent.Create(story2) });

        var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("https://hacker-news.firebaseio.com/v0/") };
        var config = new ConfigurationBuilder().Build();
        var logger = new Mock<ILogger>().Object;

        // Act
        await ImmutableArrayHackerNewsService.LoadBestStories(httpClient, config, logger);
        var results = _service.GetBestStoriesAsync(2).ToList();

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Equal("Story High", results[0].Title); // Should be first due to score 100
        Assert.Equal(100, results[0].Score);
        Assert.Equal("Story Low", results[1].Title);
    }

    [Fact]
    public void GetBestStoriesAsync_RespectsRequestedCount()
    {
        // Arrange
        var field = typeof(ImmutableArrayHackerNewsService).GetField("_stories", BindingFlags.Static | BindingFlags.NonPublic);
        var mockStories = ImmutableArray.Create(
            new HackerRankBestStory("S1", "u1", "p1", 123, 10, 1),
            new HackerRankBestStory("S2", "u2", "p2", 124, 20, 2),
            new HackerRankBestStory("S3", "u3", "p3", 125, 30, 3)
        );
        field?.SetValue(null, mockStories);

        // Act
        var result = _service.GetBestStoriesAsync(2);

        // Assert
        Assert.Equal(2, result.Count());
    }
}