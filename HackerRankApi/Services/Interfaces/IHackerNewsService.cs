using HackerRankApi.Models;

namespace HackerRankApi.Services.Interfaces;

public interface IHackerNewsService
{
    IEnumerable<HackerRankBestStoryDto> GetBestStoriesAsync(int n);
}