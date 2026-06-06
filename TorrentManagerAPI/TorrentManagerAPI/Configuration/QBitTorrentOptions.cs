namespace TorrentManagerAPI.Configuration;

public class QBitTorrentOptions
{
    public const string SectionName = "QBitTorrent";

    public required string BaseUrl { get; set; }

    public required string Username { get; set; }

    public required string Password { get; set; }
}
