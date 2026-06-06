namespace TorrentManagerAPI.Models;

public class MovieDownload
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public int Year { get; set; }

    public MediaCategory Category { get; set; }

    public required string TorrentFileName { get; set; }

    public string? QBitTorrentHash { get; set; }

    public DownloadStatus Status { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public string TargetFolderName => $"{Name} ({Year})";
}
