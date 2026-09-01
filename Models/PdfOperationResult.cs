namespace CompressMyWeb.Models;

public sealed record PdfOperationResult(bool Success, string OutputPath, string? ErrorMessage = null);
