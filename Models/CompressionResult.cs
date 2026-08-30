namespace CompressMyWeb.Models;

public record CompressionResult(
    bool Success,
    string OutputPath,
    long OriginalSizeBytes,
    long NewSizeBytes,
    string? ErrorMessage = null
);
