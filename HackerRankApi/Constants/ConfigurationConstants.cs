namespace HackerRankApi.Constants;

public static class ConfigurationConstants
{
    public const string DefaultHackerNewsBaseUrl = "https://hacker-news.firebaseio.com/v0/";
    
    public const string RateLimiterPoliceName = "fixed";
    
    public const string ApiKeyHeaderName = "X-API-KEY";
    
    public const string ApiKeyConfigOptionName = "ApiKey";
    
    public const string SecurityDefinitionApiKeySchemaName = "ApiKeySchema";
    
    public const string HackerNewsSettingsSectionName = "HackerNewsSettings";
    
    public const string ResilienceOptionsSectionName = "ResilienceOptions";
    
    public const string RateLimitSettingsSectionName = "RateLimitSettings";
}