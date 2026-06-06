using Microsoft.EntityFrameworkCore;
using TorrentManagerAPI.Data;
using TorrentManagerAPI.Models;

namespace TorrentManagerAPI.Services;

public class DownloadMonitorService(
    IServiceScopeFactory scopeFactory,
    IQBitTorrentClient qBitTorrentClient,
    IMovieFileOrganizer movieFileOrganizer,
    ILogger<DownloadMonitorService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessActiveDownloadsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error while monitoring downloads.");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task ProcessActiveDownloadsAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var activeDownloads = await dbContext.MovieDownloads
            .Where(d => d.Status == DownloadStatus.Queued
                        || d.Status == DownloadStatus.Downloading
                        || d.Status == DownloadStatus.Organizing)
            .ToListAsync(cancellationToken);

        foreach (var download in activeDownloads)
        {
            await ProcessDownloadAsync(download, dbContext, cancellationToken);
        }
    }

    private async Task ProcessDownloadAsync(
        MovieDownload download,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        try
        {
            if (download.Status == DownloadStatus.Queued)
            {
                await LinkTorrentHashAsync(download, cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            if (download.Status == DownloadStatus.Downloading)
            {
                if (string.IsNullOrEmpty(download.QBitTorrentHash))
                {
                    await MarkFailedAsync(download, dbContext, "Torrent hash is missing.", cancellationToken);
                    return;
                }

                var isComplete = await qBitTorrentClient.IsTorrentCompleteAsync(download.QBitTorrentHash, cancellationToken);
                if (!isComplete)
                {
                    return;
                }

                download.Status = DownloadStatus.Organizing;
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            if (download.Status == DownloadStatus.Organizing)
            {
                await OrganizeDownloadAsync(download, cancellationToken);

                download.Status = DownloadStatus.Completed;
                download.CompletedAt = DateTime.UtcNow;
                download.ErrorMessage = null;
                await dbContext.SaveChangesAsync(cancellationToken);

                logger.LogInformation(
                    "Completed organization for '{Name}' ({Year}) in folder '{Folder}'",
                    download.Name,
                    download.Year,
                    download.TargetFolderName);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to process download {DownloadId}", download.Id);
            await MarkFailedAsync(download, dbContext, ex.Message, cancellationToken);
        }
    }

    private async Task LinkTorrentHashAsync(MovieDownload download, CancellationToken cancellationToken)
    {
        var tag = GetTag(download.Id);
        var torrent = await qBitTorrentClient.GetTorrentByTagAsync(tag, cancellationToken);

        if (torrent is null)
        {
            return;
        }

        download.QBitTorrentHash = torrent.Hash;
        download.Status = DownloadStatus.Downloading;
    }

    private async Task OrganizeDownloadAsync(MovieDownload download, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(download.QBitTorrentHash))
        {
            throw new InvalidOperationException("Torrent hash is missing.");
        }

        var content = await qBitTorrentClient.GetTorrentContentAsync(download.QBitTorrentHash, cancellationToken);

        await movieFileOrganizer.OrganizeAsync(download, download.QBitTorrentHash, content, cancellationToken);
    }

    private static async Task MarkFailedAsync(
        MovieDownload download,
        AppDbContext dbContext,
        string message,
        CancellationToken cancellationToken)
    {
        download.Status = DownloadStatus.Failed;
        download.ErrorMessage = message;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public static string GetTag(Guid downloadId) => $"tma-{downloadId:N}";
}
