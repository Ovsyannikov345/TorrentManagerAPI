namespace TorrentManagerAPI.Configuration;

public class StorageOptions
{
    public const string SectionName = "Storage";

    public required string MoviesPath { get; set; }
}
