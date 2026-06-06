namespace TorrentManagerAPI.Services;

public record QBitTorrentFile(string Name, long Size, double Progress);

public record QBitTorrentContent(string SavePath, string ContentPath, IReadOnlyList<QBitTorrentFile> Files);
