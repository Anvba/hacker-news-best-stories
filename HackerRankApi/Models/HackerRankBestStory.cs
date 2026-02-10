using System.Text.Json.Serialization;

namespace HackerRankApi.Models;

public record HackerRankBestStory(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("url")] string Uri,
    [property: JsonPropertyName("by")] string PostedBy,
    [property: JsonPropertyName("time")] long UnixTime,
    [property: JsonPropertyName("score")] int Score,
    [property: JsonPropertyName("descendants")] int CommentCount);

public record ErrorResponse(
    [property: JsonPropertyName("error")] string Error);
    
public record HackerRankBestStoryDto(string Title, string Uri, string PostedBy, string Time, int Score, int CommentCount);

// JSON Source Generation
[JsonSerializable(typeof(int[]))]
[JsonSerializable(typeof(HackerRankBestStory))]
[JsonSerializable(typeof(IEnumerable<HackerRankBestStoryDto>))]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(object))]
internal partial class HackerRankBestStoryJsonContext : JsonSerializerContext;