using System;

namespace CompressMyWeb.Helpers;

public static class FileSizeFormatter
{
    private static readonly string[] SizeSuffixes = { "B", "KB", "MB", "GB", "TB" };

    public static string Format(long bytes)
    {
        if (bytes < 0) return "0 B";
        if (bytes == 0) return "0 B";

        int mag = (int)Math.Log(bytes, 1024);
        if (mag >= SizeSuffixes.Length) mag = SizeSuffixes.Length - 1;

        decimal adjustedSize = (decimal)bytes / (1L << (mag * 10));

        if (mag == 0)
            return $"{adjustedSize:n0} {SizeSuffixes[mag]}";

        return $"{adjustedSize:n2} {SizeSuffixes[mag]}";
    }
}
