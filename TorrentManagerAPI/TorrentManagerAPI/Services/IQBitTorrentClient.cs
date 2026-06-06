namespace TorrentManagerAPI.Services;

public interface IQBitTorrentClient
{
    public Task AddTorrentAsync(Stream torrentFileStream, string torrentFileName, string tag, CancellationToken cancellationToken = default);

    Task<QBitTorrentInfo?> GetTorrentByTagAsync(string tag, CancellationToken cancellationToken = default);

    Task<bool> IsTorrentCompleteAsync(string hash, CancellationToken cancellationToken = default);

    Task<QBitTorrentContent> GetTorrentContentAsync(string hash, CancellationToken cancellationToken = default);

    Task SetTorrentLocationAsync(string hash, string location, CancellationToken cancellationToken = default);

    Task RenameFolderAsync(string hash, string oldPath, string newPath, CancellationToken cancellationToken = default);

    Task RenameFileAsync(string hash, string oldPath, string newPath, CancellationToken cancellationToken = default);
}

public record QBitTorrentInfo(string Hash, string Name, double Progress, string State);
