using Microsoft.EntityFrameworkCore;
using Serilog;
using TorrentManagerAPI.Configuration;
using TorrentManagerAPI.Data;
using TorrentManagerAPI.Endpoints;
using TorrentManagerAPI.Services;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.Services.AddOpenApi();
    builder.Services.Configure<QBitTorrentOptions>(builder.Configuration.GetSection(QBitTorrentOptions.SectionName));
    builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection(StorageOptions.SectionName));

    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

    builder.Services.AddHealthChecks()
        .AddDbContextCheck<AppDbContext>();

    builder.Services.AddHttpClient<IQBitTorrentClient, QBitTorrentClient>();
    builder.Services.AddSingleton<IMovieFileOrganizer, MovieFileOrganizer>();
    builder.Services.AddHostedService<DownloadMonitorService>();

    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi("/api/openapi/{documentName}.json");
    }

    app.MapHealthEndpoints();
    app.MapMovieEndpoints();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
