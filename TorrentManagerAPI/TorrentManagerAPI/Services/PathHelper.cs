using System.Text.RegularExpressions;

namespace TorrentManagerAPI.Services;

public static partial class PathHelper
{
    private static readonly Regex InvalidFileNameChars = InvalidFileNameCharsRegex();

    public static string SanitizeFolderName(string name)
    {
        var sanitized = InvalidFileNameChars.Replace(name, string.Empty).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "Unknown" : sanitized;
    }

    [GeneratedRegex(@"[\\/:*?""<>|]")]
    private static partial Regex InvalidFileNameCharsRegex();
}
