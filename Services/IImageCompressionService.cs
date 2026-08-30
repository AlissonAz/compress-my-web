using CompressMyWeb.Models;
using System.Threading;
using System.Threading.Tasks;

namespace CompressMyWeb.Services;

public interface IImageCompressionService
{
    Task<CompressionResult> CompressAsync(
        ImageQueueItem item,
        CompressionSettings settings,
        CancellationToken cancellationToken = default);
}
