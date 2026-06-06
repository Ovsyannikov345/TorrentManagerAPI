namespace TorrentManagerAPI.Endpoints;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this WebApplication app)
    {
        app.MapGroup("/api")
            .WithTags("Health")
            .MapHealthChecks("/health");
    }
}
