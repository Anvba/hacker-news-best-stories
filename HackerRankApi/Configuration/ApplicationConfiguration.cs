namespace HackerRankApi.Configuration;

public static class ApplicationConfiguration
{
    public static void ConfigureSwaggerGen(this WebApplication app)
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }
}