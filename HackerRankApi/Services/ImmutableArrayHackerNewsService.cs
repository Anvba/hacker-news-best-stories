using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Threading.RateLimiting;
using HackerRankApi.Constants;
using HackerRankApi.Models;
using HackerRankApi.Services.Interfaces;

namespace HackerRankApi.Services;

/// <summary>
/// Provides Hacker News "Best Stories" using an in-memory <see cref="ImmutableArray{T}"/> 
/// to ensure high-performance, thread-safe access for concurrent API requests.
/// </summary>
public class ImmutableArrayHackerNewsService(ILogger<ImmutableArrayHackerNewsService> logger): IHackerNewsService
{
    private const string BestStoriesUri = "beststories.json";
    private const string StoriesUri = "item/{0}.json";
    private const ushort MaxFailedRequestRetries = 5;
    private const int MillisecondsDelay = 5000;
    private const int PermitLimit = 30;
    private const int MaxNumberOfBestStories = 200;

    /// <summary>
    /// Static cache holding the latest version of best stories.
    /// Using ImmutableArray allows multiple threads to read the collection without locking,
    /// while updates are performed via atomic reference swaps.
    /// </summary>
    private static ImmutableArray<HackerRankBestStory> _stories = ImmutableArray.Create<HackerRankBestStory>();
    
    /// <summary>
    /// Retrieves a specified number of stories from the internal cache, 
    /// sorted by score in descending order.
    /// </summary>
    /// <param name="n">The number of stories to retrieve.</param>
    /// <returns>A collection of <see cref="HackerRankBestStoryDto"/>.</returns>
    public IEnumerable<HackerRankBestStoryDto> GetBestStoriesAsync(int n)
    {
        if (_stories.IsEmpty)
        {
            logger.LogInformation("There are no best stories");
            return Enumerable.Empty<HackerRankBestStoryDto>();
        }
        
        // Return a projection of the data sorted by Score descending.
        return _stories
            .OrderByDescending(s => s.Score)
            .Select(s => new HackerRankBestStoryDto(
                s.Title, 
                s.Uri, 
                s.PostedBy, 
                DateTimeOffset.FromUnixTimeSeconds(s.UnixTime).ToString("yyyy-MM-ddTHH:mm:ssK"), 
                s.Score, 
                s.CommentCount))
            .Take(n);
    }

    /// <summary>
    /// Background logic to refresh the story cache. Fetches current IDs and then 
    /// hydrates details in parallel with rate limiting and retry logic.
    /// </summary>
    /// <param name="http">The HttpClient to use for requests.</param>
    /// <param name="configuration">App configuration for parallelism settings.</param>
    /// <param name="logger">Logger for tracking the load process.</param>
    internal static async Task LoadBestStories(HttpClient http, IConfiguration configuration, ILogger logger)
    {
        // Fetch the initial list of top IDs
        var bestStoryIds = await http.GetFromJsonAsync<int[]>(BestStoriesUri,  HackerRankBestStoryJsonContext.Default.Int32Array) ?? Array.Empty<int>();
        
        // ConcurrentBag is used to safely collect story details from multiple threads
        var storyDetails = new ConcurrentBag<HackerRankBestStory>();
        
        // Initial pass to fetch all story details
        var failedRequests = await GetHackerHewsStories(http, bestStoryIds, storyDetails, configuration, logger);

        // Retry loop for transient failures or rate-limiting rejections
        var retry = 0;
        while (failedRequests.Length != 0 && retry < MaxFailedRequestRetries)
        {
            // Wait 5 seconds between retries to allow upstream API rate limits to reset
            await Task.Delay(MillisecondsDelay);
            failedRequests = await GetHackerHewsStories(http, failedRequests, storyDetails, configuration, logger); 
            retry++;
        }
        
        // Atomically replace the old static collection with the new one.
        // This ensures readers always see a consistent, complete version of the data.
        ImmutableInterlocked.InterlockedExchange<HackerRankBestStory>(ref _stories, [..storyDetails.ToArray()]);
        
        logger.LogDebug("{StoryDetailsCount} stories acquired", storyDetails.Count);
    }

    /// <summary>
    /// Helper method to fetch story details for a set of IDs in parallel.
    /// Uses a RateLimiter to prevent overwhelming the Hacker News API.
    /// </summary>
    private static async Task<int[]> GetHackerHewsStories(
        HttpClient http,
        int[] bestStoryIds,
        ConcurrentBag<HackerRankBestStory> storyDetails,
        IConfiguration configuration,
        ILogger logger)
    {
        // Get parallelism degree from configuration or default to system core count
        var maxParallel = configuration.GetValue(
            $"{ConfigurationConstants.HackerNewsSettingsSectionName}:{ConfigSectionOptionsHackerNewsSettings.MaxDegreeOfParallelism}",
            Environment.ProcessorCount);
        
        // Define a rate limit of 30 requests per second to comply with typical API usage guidelines
        var limiter = new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
        {
            PermitLimit = PermitLimit,
            Window = TimeSpan.FromSeconds(1),
            QueueLimit = MaxNumberOfBestStories, 
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        });
        
        var options = new ParallelOptions { MaxDegreeOfParallelism = maxParallel };
        var failedRequests = new ConcurrentBag<int>();
        var storyRequestCounter = 0;

        // Fetch each story detail concurrently
        await Parallel.ForEachAsync(bestStoryIds, options, async (id, ct) => {
            // Respect the rate limiter before starting the request
            using RateLimitLease lease = await limiter.AcquireAsync(permitCount: 1, ct);

            if (!lease.IsAcquired)
            {
                logger.LogDebug("[Rejected] Could not acquire permit for item {id}", id);
                failedRequests.Add(id);
                return;
            }

            Interlocked.Increment(ref storyRequestCounter);
            try
            {
                var story = await http.GetFromJsonAsync<HackerRankBestStory>(
                    string.Format(StoriesUri, id), 
                    HackerRankBestStoryJsonContext.Default.HackerRankBestStory, 
                    ct);
                
                if (story is not null)
                {
                    storyDetails.Add(story);
                }
                else
                {
                    logger.LogDebug("[Failed] Could not acquire story for item {id}", id);
                    failedRequests.Add(id);
                }
            }
            catch (Exception ex)
            {
                // Catch potential network or deserialization errors
                logger.LogDebug(ex, "[Error] Exception while fetching story {id}", id);
                failedRequests.Add(id);
            }
        });
        
        logger.LogDebug("{StoriesRequests} stories requests sent", storyRequestCounter);
        
        var numberOfRequestedStories = failedRequests.Count + storyDetails.Count;
        Debug.Assert(numberOfRequestedStories == bestStoryIds.Length);
        Debug.Assert(numberOfRequestedStories == storyRequestCounter);
        
        return failedRequests.ToArray();
    }
}