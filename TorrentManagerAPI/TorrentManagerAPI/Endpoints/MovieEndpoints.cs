using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TorrentManagerAPI.Configuration;
using TorrentManagerAPI.Data;
using TorrentManagerAPI.Models;
using TorrentManagerAPI.Services;

namespace TorrentManagerAPI.Endpoints;

public static class MovieEndpoints
{
    public static void MapMovieEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/movies").WithTags("Movies");

        group.MapPost("/downloads", CreateMovieDownloadAsync)
            .DisableAntiforgery()
            .Accepts<IFormFile>("multipart/form-data");

        group.MapGet("/downloads", GetMovieDownloadsAsync);

        group.MapGet("/downloads/{id:guid}", GetMovieDownloadByIdAsync);
    }

    private static async Task<IResult> CreateMovieDownloadAsync(
        HttpRequest request,
        AppDbContext dbContext,
        IQBitTorrentClient qBitTorrentClient,
        IOptions<StorageOptions> storageOptions,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
        {
            return Results.BadRequest(new { error = "Request must be multipart/form-data." });
        }

        var form = await request.ReadFormAsync(cancellationToken);

        if (!TryGetRequiredField(form, "name", out var name))
        {
            return Results.BadRequest(new { error = "Field 'name' is required." });
        }

        if (!int.TryParse(form["year"], out var year) || year < 1800 || year > DateTimeOffset.Now.Year)
        {
            return Results.BadRequest(new { error = "Field 'year' must be a valid year." });
        }

        if (!Enum.TryParse<MediaCategory>(form["category"], ignoreCase: true, out var category)
            || category != MediaCategory.Movie)
        {
            return Results.BadRequest(new { error = "Field 'category' must be 'Movie'." });
        }

        var torrentFile = form.Files.GetFile("torrentFile");
        if (torrentFile is null || torrentFile.Length == 0)
        {
            return Results.BadRequest(new { error = "Field 'torrentFile' is required." });
        }

        if (!string.Equals(Path.GetExtension(torrentFile.FileName), ".torrent", StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new { error = "torrentFile must have a .torrent extension." });
        }

        using var fileStream = torrentFile.OpenReadStream();

        var download = new MovieDownload
        {
            Name = name.Trim(),
            Year = year,
            Category = category,
            TorrentFileName = torrentFile.FileName,
            Status = DownloadStatus.Queued,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.MovieDownloads.Add(download);
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var tag = DownloadMonitorService.GetTag(download.Id);
            await qBitTorrentClient.AddTorrentAsync(fileStream, torrentFile.FileName, tag, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to add torrent for download {DownloadId}", download.Id);

            download.Status = DownloadStatus.Failed;
            download.ErrorMessage = ex.Message;
            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Problem(
                detail: "The download was saved but qBittorrent rejected the torrent.",
                statusCode: StatusCodes.Status502BadGateway);
        }

        return Results.Created($"/api/movies/downloads/{download.Id}", MapToResponse(download));
    }

    private static async Task<IResult> GetMovieDownloadsAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        var downloads = await dbContext.MovieDownloads
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => MapToResponse(d))
            .ToListAsync(cancellationToken);

        return Results.Ok(downloads);
    }

    private static async Task<IResult> GetMovieDownloadByIdAsync(
        Guid id,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var download = await dbContext.MovieDownloads.FindAsync([id], cancellationToken);
        return download is null ? Results.NotFound() : Results.Ok(MapToResponse(download));
    }

    private static bool TryGetRequiredField(IFormCollection form, string key, out string value)
    {
        value = form[key].ToString().Trim();
        return !string.IsNullOrWhiteSpace(value);
    }

    private static object MapToResponse(MovieDownload download) => new
    {
        download.Id,
        download.Name,
        download.Year,
        Category = download.Category.ToString(),
        download.TorrentFileName,
        download.QBitTorrentHash,
        Status = download.Status.ToString(),
        TargetFolder = download.TargetFolderName,
        download.ErrorMessage,
        download.CreatedAt,
        download.CompletedAt
    };
}
