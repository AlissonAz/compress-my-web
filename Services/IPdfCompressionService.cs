using CompressMyWeb.Models;
using System.Threading;
using System.Threading.Tasks;

namespace CompressMyWeb.Services;

public interface IPdfCompressionService
{
    Task<CompressionResult> CompressAsync(
        ImageQueueItem item,
        CompressionSettings settings,
        CancellationToken cancellationToken = default);

    Task<CompressionResult> ConvertImageToPdfAsync(
        ImageQueueItem item,
        CompressionSettings settings,
        CancellationToken cancellationToken = default);
}
