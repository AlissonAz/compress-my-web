using CompressMyWeb.Helpers;
using System.IO;

namespace CompressMyWeb.Models;

public sealed record PdfMergeItem(string FilePath, string FileName, long SizeBytes)
{
    public string SizeFormatted => FileSizeFormatter.Format(SizeBytes);

    public static PdfMergeItem FromFile(string filePath)
    {
        var file = new FileInfo(filePath);
        return new PdfMergeItem(file.FullName, file.Name, file.Exists ? file.Length : 0);
    }
}
