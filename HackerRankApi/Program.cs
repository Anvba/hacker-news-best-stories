using HackerRankApi.Configuration;
using HackerRankApi.Endpoints;
using HackerRankApi.Middlewares;
using HackerRankApi.Services;

var builder = WebApplication.CreateSlimBuilder(args);

builder.ConfigureJsonOptions();
builder.ConfigureRoutingOptions();
builder.ConfigureApiRateLimiting();
builder.ConfigureHackerNewsHttpClientResilienceOptions();
builder.ConfigureSwaggerOptions();

builder.Services.AddProblemDetails();
builder.Services.AddHostedService<HostedHackerNewsService>();

builder.Logging.AddSimpleConsole(options => {
    options.IncludeScopes = true;
});

var app = builder.Build();

app.UseRateLimiter();

var isDevelopment = app.Environment.IsDevelopment();

if (isDevelopment)
{
    app.ConfigureSwaggerGen();
}

app.RegisterApiKeyMiddleware(builder, isDevelopment);
app.RegisterRoutes();

app.Run();