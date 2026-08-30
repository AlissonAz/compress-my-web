using CompressMyWeb.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CompressMyWeb.Services;

public class ImageCompressionService : IImageCompressionService
{
    public async Task<CompressionResult> CompressAsync(
        ImageQueueItem item,
        CompressionSettings settings,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(item.FilePath))
            {
                return new CompressionResult(false, string.Empty, 0, 0, "Arquivo original não encontrado.");
            }

            string targetDir = string.IsNullOrWhiteSpace(settings.OutputDirectory)
                ? Path.GetDirectoryName(item.FilePath) ?? Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
                : settings.OutputDirectory;

            // Se solicitado, preserva a subpasta relativa da origem
            if (settings.PreserveFolderStructure && !string.IsNullOrWhiteSpace(item.RelativeDirectory))
            {
                targetDir = Path.Combine(targetDir, item.RelativeDirectory);
            }

            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(item.FilePath);
            string suffix = settings.FileSuffix ?? string.Empty;
            string extension = GetOutputExtension(item.FilePath, settings.OutputFormat);
            string destinationFilePath = Path.Combine(targetDir, $"{fileNameWithoutExt}{suffix}{extension}");

            // Se a saída sobrescrever a própria entrada, grava primeiro em arquivo temporário.
            bool isSameFile = string.Equals(
                Path.GetFullPath(item.FilePath),
                Path.GetFullPath(destinationFilePath),
                StringComparison.OrdinalIgnoreCase);

            string tempOutputPath = isSameFile
                ? Path.Combine(targetDir, $"{fileNameWithoutExt}{suffix}_{Guid.NewGuid():N}.tmp")
                : destinationFilePath;

            var originalFileInfo = new FileInfo(item.FilePath);
            long originalSize = originalFileInfo.Length;

            using (var image = await Image.LoadAsync(item.FilePath, cancellationToken))
            {
                // Remoção de metadados EXIF/GPS/ICC para privacidade e economia
                if (settings.StripMetadata)
                {
                    image.Metadata.ExifProfile = null;
                    image.Metadata.IccProfile = null;
                    image.Metadata.XmpProfile = null;
                    image.Metadata.IptcProfile = null;
                }

                if (settings.EnableResize)
                {
                    int maxW = Math.Max(1, settings.MaxWidth);
                    int maxH = Math.Max(1, settings.MaxHeight);

                    if (image.Width > maxW || image.Height > maxH)
                    {
                        image.Mutate(ctx => ctx.Resize(new ResizeOptions
                        {
                            Size = new Size(maxW, maxH),
                            Mode = ResizeMode.Max
                        }));
                    }
                }

                IImageEncoder encoder = GetEncoder(image, settings);

                // JPEG não suporta transparência. Compõe pixels transparentes sobre branco.
                if (settings.OutputFormat == OutputFormat.Jpeg)
                {
                    image.Mutate(ctx => ctx.BackgroundColor(Color.White));
                }

                await using (var outputStream = new FileStream(tempOutputPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
                {
                    await image.SaveAsync(outputStream, encoder, cancellationToken);
                }
            }

            if (isSameFile)
            {
                File.Move(tempOutputPath, destinationFilePath, true);
            }

            var newFileInfo = new FileInfo(destinationFilePath);
            long newSize = newFileInfo.Length;

            // Se solicitado e a conversão foi bem-sucedida, exclui o arquivo original se não for o mesmo arquivo
            if (settings.DeleteOriginal && !isSameFile)
            {
                try
                {
                    File.Delete(item.FilePath);
                }
                catch
                {
                    // Não falha a conversão se não conseguir deletar o original
                }
            }

            return new CompressionResult(true, destinationFilePath, originalSize, newSize);
        }
        catch (OperationCanceledException)
        {
            return new CompressionResult(false, string.Empty, 0, 0, "Operação cancelada.");
        }
        catch (Exception ex)
        {
            return new CompressionResult(false, string.Empty, 0, 0, ex.Message);
        }
    }

    private static string GetOutputExtension(string sourcePath, OutputFormat outputFormat) => outputFormat switch
    {
        OutputFormat.Webp => ".webp",
        OutputFormat.Jpeg => ".jpg",
        OutputFormat.Png => ".png",
        OutputFormat.Pdf => throw new NotSupportedException("PDF aceita somente arquivos PDF existentes."),
        OutputFormat.Original => Path.GetExtension(sourcePath).ToLowerInvariant(),
        _ => throw new NotSupportedException($"Formato de saída não suportado: {outputFormat}")
    };

    private static IImageEncoder GetEncoder(Image image, CompressionSettings settings)
    {
        int quality = Math.Clamp(settings.Quality, 1, 100);

        return settings.OutputFormat switch
        {
            OutputFormat.Webp => new WebpEncoder
            {
                Quality = quality,
                FileFormat = settings.IsLossless ? WebpFileFormatType.Lossless : WebpFileFormatType.Lossy
            },
            OutputFormat.Jpeg => new JpegEncoder { Quality = quality },
            OutputFormat.Png => new PngEncoder(),
            OutputFormat.Pdf => throw new NotSupportedException("PDF aceita somente arquivos PDF existentes."),
            OutputFormat.Original => GetOriginalFormatEncoder(image),
            _ => throw new NotSupportedException($"Formato de saída não suportado: {settings.OutputFormat}")
        };
    }

    private static IImageEncoder GetOriginalFormatEncoder(Image image)
    {
        IImageFormat? originalFormat = image.Metadata.DecodedImageFormat;
        if (originalFormat == null)
        {
            throw new NotSupportedException("Não foi possível identificar o formato original da imagem.");
        }

        return image.Configuration.ImageFormatsManager.GetEncoder(originalFormat);
    }
}
