using CompressMyWeb.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CompressMyWeb.Services;

public interface IPdfToolService
{
    Task<PdfOperationResult> MergeAsync(
        IReadOnlyList<PdfMergeItem> files,
        string destinationPath,
        CancellationToken cancellationToken = default);

    Task<PdfOperationResult> SplitEachPageAsync(
        string inputPath,
        string outputDirectory,
        string outputPrefix,
        CancellationToken cancellationToken = default);

    Task<PdfOperationResult> SplitRangesAsync(
        string inputPath,
        string outputDirectory,
        string outputPrefix,
        string ranges,
        CancellationToken cancellationToken = default);
}
