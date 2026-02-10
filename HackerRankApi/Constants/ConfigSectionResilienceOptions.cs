namespace HackerRankApi.Constants;

public static class ConfigSectionResilienceOptions
{
    public const string RetryCount = "RetryCount";
    
    public const string BackoffSeconds = "BackoffSeconds";
    
    public const string CircuitBreakerThreshold = "MaxRetCircuitBreakerThresholdryInterval";
    
    public const string SamplingDurationSeconds = "SamplingDurationSeconds";
}