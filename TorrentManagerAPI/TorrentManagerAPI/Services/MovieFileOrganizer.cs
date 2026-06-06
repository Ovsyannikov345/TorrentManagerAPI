using Microsoft.Extensions.Options;
using TorrentManagerAPI.Configuration;
using TorrentManagerAPI.Models;

namespace TorrentManagerAPI.Services;

public interface IMovieFileOrganizer
{
    Task OrganizeAsync(
        MovieDownload download,
        string torrentHash,
        QBitTorrentContent content,
        CancellationToken cancellationToken = default);
}

public class MovieFileOrganizer(
    IQBitTorrentClient qBitTorrentClient,
    IOptions<StorageOptions> storageOptions,
    ILogger<MovieFileOrganizer> logger) : IMovieFileOrganizer
{
    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mkv", ".mp4", ".avi", ".mov", ".wmv", ".m4v", ".ts", ".webm", ".mpg", ".mpeg"
    };

    public async Task OrganizeAsync(
        MovieDownload download,
        string torrentHash,
        QBitTorrentContent content,
        CancellationToken cancellationToken = default)
    {
        var moviesPath = storageOptions.Value.MoviesPath;

        var folderName = download.TargetFolderName;

        var files = GetCompletedFiles(content);

        var rootFolder = GetRootFolder(files);

        await qBitTorrentClient.SetTorrentLocationAsync(torrentHash, moviesPath, cancellationToken);

        await qBitTorrentClient.RenameFolderAsync(torrentHash, rootFolder, folderName, cancellationToken);

        var movieFile = SelectMovieFile(files);
        var targetMoviePath = $"{folderName}/{folderName}{Path.GetExtension(movieFile.Name)}";

        await qBitTorrentClient.RenameFileAsync(
            torrentHash,
            movieFile.Name,
            targetMoviePath,
            cancellationToken);
    }

    private static string GetRootFolder(IReadOnlyList<QBitTorrentFile> files)
    {
        var rootFolder = files[0].Name.Split('/')[0];
        if (string.IsNullOrWhiteSpace(rootFolder))
        {
            throw new InvalidOperationException("Torrent does not contain a root folder.");
        }

        return rootFolder;
    }

    private static QBitTorrentFile SelectMovieFile(IReadOnlyList<QBitTorrentFile> files)
    {
        var movieFile = files
            .Where(f => VideoExtensions.Contains(Path.GetExtension(f.Name)))
            .Where(f => !f.Name.Contains("sample", StringComparison.OrdinalIgnoreCase))
            .MaxBy(f => f.Size);

        if (movieFile is null)
        {
            throw new InvalidOperationException("No movie file found in the torrent folder.");
        }

        return movieFile;
    }

    private static List<QBitTorrentFile> GetCompletedFiles(QBitTorrentContent content)
    {
        var files = content.Files
            .Where(f => f.Progress >= 1.0)
            .ToList();

        if (files.Count == 0)
        {
            throw new InvalidOperationException("Torrent has no fully downloaded files.");
        }

        return files;
    }
}
