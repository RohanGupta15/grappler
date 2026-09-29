namespace YtDownloader.Core.Engine;

/// <summary>Reads SHA-256 hashes out of the checksum files published next to engine releases.</summary>
public static class ChecksumFile
{
    /// <summary>Returns the lowercase hex hash for <paramref name="fileName"/>, or null if it isn't listed.</summary>
    public static string? FindSha256(string content, string fileName)
    {
        var lines = content.Split('\n').Select(l => l.Trim()).ToArray();

        // Deno publishes PowerShell Get-FileHash output: "Hash : X" / "Path : ...\file".
        var hashLine = lines.FirstOrDefault(l => l.StartsWith("Hash", StringComparison.Ordinal) && l.Contains(':'));
        var pathLine = lines.FirstOrDefault(l => l.StartsWith("Path", StringComparison.Ordinal) && l.Contains(':'));
        if (hashLine is not null && pathLine is not null)
        {
            var path = pathLine[(pathLine.IndexOf(':') + 1)..].Trim();
            return Path.GetFileName(path.Replace('\\', '/')) == fileName
                ? hashLine[(hashLine.IndexOf(':') + 1)..].Trim().ToLowerInvariant()
                : null;
        }

        // sha256sum style: "<hash>  <file>" (a leading '*' marks binary mode).
        foreach (var line in lines)
        {
            var parts = line.Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && parts[1].TrimStart('*') == fileName)
                return parts[0].ToLowerInvariant();
        }
        return null;
    }

    /// <summary>True when the SHA-256 of the file at <paramref name="path"/> equals <paramref name="expectedHex"/>.</summary>
    public static bool Verify(string path, string expectedHex)
    {
        using var stream = File.OpenRead(path);
        var actual = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream));
        return string.Equals(actual, expectedHex, StringComparison.OrdinalIgnoreCase);
    }
}
