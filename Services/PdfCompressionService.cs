using CompressMyWeb.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CompressMyWeb.Services;

public sealed class PdfCompressionService : IPdfCompressionService
{
    public async Task<CompressionResult> ConvertImageToPdfAsync(
        ImageQueueItem item,
        CompressionSettings settings,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(item.FilePath))
            {
                return Failure("Arquivo de imagem original não encontrado.");
            }

            string targetDir = GetTargetDirectory(item, settings);
            Directory.CreateDirectory(targetDir);

            string fileName = Path.GetFileNameWithoutExtension(item.FilePath);
            string destinationPath = Path.Combine(targetDir, $"{fileName}{settings.FileSuffix ?? string.Empty}.pdf");
            long originalSize = new FileInfo(item.FilePath).Length;

            using var image = await Image.LoadAsync(item.FilePath, cancellationToken);

            if (settings.StripMetadata)
            {
                image.Metadata.ExifProfile = null;
                image.Metadata.IccProfile = null;
                image.Metadata.XmpProfile = null;
                image.Metadata.IptcProfile = null;
            }

            if (settings.EnableResize && (image.Width > settings.MaxWidth || image.Height > settings.MaxHeight))
            {
                image.Mutate(context => context.Resize(new ResizeOptions
                {
                    Size = new Size(Math.Max(1, settings.MaxWidth), Math.Max(1, settings.MaxHeight)),
                    Mode = ResizeMode.Max
                }));
            }

            // O PDF usa JPEG internamente; transparência é composta sobre branco.
            image.Mutate(context => context.BackgroundColor(Color.White));

            await using var jpegStream = new MemoryStream();
            await image.SaveAsync(jpegStream, new JpegEncoder
            {
                Quality = Math.Clamp(settings.Quality, 1, 100)
            }, cancellationToken);

            byte[] pdf = BuildSingleImagePdf(jpegStream.ToArray(), image.Width, image.Height);
            await File.WriteAllBytesAsync(destinationPath, pdf, cancellationToken);

            if (settings.DeleteOriginal)
            {
                try { File.Delete(item.FilePath); } catch { }
            }

            return new CompressionResult(true, destinationPath, originalSize, pdf.LongLength);
        }
        catch (OperationCanceledException)
        {
            return Failure("Operação cancelada.");
        }
        catch (Exception ex)
        {
            return Failure(ex.Message);
        }
    }

    public async Task<CompressionResult> CompressAsync(
        ImageQueueItem item,
        CompressionSettings settings,
        CancellationToken cancellationToken = default)
    {
        string tempOutputPath = string.Empty;
        string ghostscriptOutputPath = string.Empty;

        try
        {
            if (!File.Exists(item.FilePath))
            {
                return Failure("Arquivo PDF original não encontrado.");
            }

            string targetDir = GetTargetDirectory(item, settings);

            Directory.CreateDirectory(targetDir);

            string fileName = Path.GetFileNameWithoutExtension(item.FilePath);
            string suffix = settings.FileSuffix ?? string.Empty;
            string destinationPath = Path.Combine(targetDir, $"{fileName}{suffix}.pdf");
            bool isSameFile = string.Equals(
                Path.GetFullPath(item.FilePath),
                Path.GetFullPath(destinationPath),
                StringComparison.OrdinalIgnoreCase);

            tempOutputPath = Path.Combine(targetDir, $".{fileName}_{Guid.NewGuid():N}.pdf.tmp");
            long originalSize = new FileInfo(item.FilePath).Length;

            string qpdfInputPath = item.FilePath;
            if (settings.OptimizePdfImages)
            {
                ghostscriptOutputPath = Path.Combine(targetDir, $".{fileName}_{Guid.NewGuid():N}.optimized.pdf.tmp");
                var ghostscriptResult = await RunGhostscriptAsync(
                    item.FilePath,
                    ghostscriptOutputPath,
                    settings.Quality,
                    cancellationToken);

                if (!ghostscriptResult.Success)
                {
                    return Failure(ghostscriptResult.ErrorMessage);
                }

                qpdfInputPath = ghostscriptOutputPath;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = FindQpdfExecutable(),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            ConfigureBundledQpdf(startInfo);

            startInfo.ArgumentList.Add(qpdfInputPath);
            startInfo.ArgumentList.Add("--compress-streams=y");
            startInfo.ArgumentList.Add("--decode-level=generalized");
            startInfo.ArgumentList.Add("--recompress-flate");
            startInfo.ArgumentList.Add("--compression-level=9");
            startInfo.ArgumentList.Add("--object-streams=generate");

            startInfo.ArgumentList.Add(tempOutputPath);

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            Task<string> standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
            Task<string> standardError = process.StandardError.ReadToEndAsync(cancellationToken);

            try
            {
                await process.WaitForExitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }

                throw;
            }

            string outputMessage = await standardOutput;
            string errorMessage = await standardError;

            if (process.ExitCode is not (0 or 3) || !File.Exists(tempOutputPath))
            {
                string details = FirstUsefulLine(errorMessage, outputMessage);
                return Failure(string.IsNullOrWhiteSpace(details)
                    ? $"qpdf encerrou com o código {process.ExitCode}."
                    : details);
            }

            long compressedSize = new FileInfo(tempOutputPath).Length;

            // Nunca entrega um arquivo maior: conserva o original quando ele já é mais eficiente.
            if (compressedSize >= originalSize)
            {
                File.Delete(tempOutputPath);
                tempOutputPath = string.Empty;

                if (!isSameFile)
                {
                    File.Copy(item.FilePath, destinationPath, overwrite: true);
                }
            }
            else
            {
                File.Move(tempOutputPath, destinationPath, overwrite: true);
                tempOutputPath = string.Empty;
            }

            long finalSize = isSameFile && compressedSize >= originalSize
                ? originalSize
                : new FileInfo(destinationPath).Length;

            if (settings.DeleteOriginal && !isSameFile)
            {
                try
                {
                    File.Delete(item.FilePath);
                }
                catch
                {
                    // A saída válida é mantida mesmo que o original não possa ser removido.
                }
            }

            return new CompressionResult(true, destinationPath, originalSize, finalSize);
        }
        catch (Win32Exception)
        {
            return Failure("qpdf não foi encontrado. Instale o qpdf e verifique se o executável está no PATH.");
        }
        catch (OperationCanceledException)
        {
            return Failure("Operação cancelada.");
        }
        catch (Exception ex)
        {
            return Failure(ex.Message);
        }
        finally
        {
            if (!string.IsNullOrEmpty(tempOutputPath) && File.Exists(tempOutputPath))
            {
                try { File.Delete(tempOutputPath); } catch { }
            }

            if (!string.IsNullOrEmpty(ghostscriptOutputPath) && File.Exists(ghostscriptOutputPath))
            {
                try { File.Delete(ghostscriptOutputPath); } catch { }
            }
        }
    }

    private static async Task<(bool Success, string ErrorMessage)> RunGhostscriptAsync(
        string inputPath,
        string outputPath,
        int quality,
        CancellationToken cancellationToken)
    {
        int jpegQuality = Math.Clamp(quality, 1, 100);
        int imageResolution = jpegQuality switch
        {
            <= 60 => 120,
            <= 80 => 150,
            <= 90 => 200,
            _ => 300
        };

        var startInfo = new ProcessStartInfo
        {
            FileName = FindGhostscriptExecutable(),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        ConfigureBundledGhostscript(startInfo);

        string[] arguments =
        [
            "-q", "-dBATCH", "-dNOPAUSE", "-dSAFER",
            "-sDEVICE=pdfwrite", "-dCompatibilityLevel=1.7",
            "-dPreserveAnnots=true", "-dPreserveMarkedContent=true",
            "-dDetectDuplicateImages=true", "-dCompressFonts=true",
            "-dPassThroughJPEGImages=false", "-dPassThroughJPXImages=false",
            "-dDownsampleColorImages=true", "-dColorImageDownsampleType=/Bicubic",
            $"-dColorImageResolution={imageResolution}",
            "-dAutoFilterColorImages=false", "-dColorImageFilter=/DCTEncode",
            "-dDownsampleGrayImages=true", "-dGrayImageDownsampleType=/Bicubic",
            $"-dGrayImageResolution={imageResolution}",
            "-dAutoFilterGrayImages=false", "-dGrayImageFilter=/DCTEncode",
            "-dDownsampleMonoImages=true", "-dMonoImageResolution=300",
            $"-dJPEGQ={jpegQuality}", $"-sOutputFile={outputPath}", inputPath
        ];

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            using var process = new Process { StartInfo = startInfo };
            process.Start();
            Task<string> output = process.StandardOutput.ReadToEndAsync(cancellationToken);
            Task<string> error = process.StandardError.ReadToEndAsync(cancellationToken);

            try
            {
                await process.WaitForExitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                throw;
            }

            string outputMessage = await output;
            string errorMessage = await error;
            if (process.ExitCode != 0 || !File.Exists(outputPath))
            {
                string details = FirstUsefulLine(errorMessage, outputMessage);
                return (false, string.IsNullOrWhiteSpace(details)
                    ? $"Ghostscript encerrou com o código {process.ExitCode}."
                    : details);
            }

            return (true, string.Empty);
        }
        catch (Win32Exception)
        {
            return (false, "Ghostscript não foi encontrado. Instale o pacote ghostscript para usar a compressão de imagens do PDF.");
        }
    }

    private static string FindQpdfExecutable()
    {
        string? configuredPath = Environment.GetEnvironmentVariable("COMPRESSMYWEB_QPDF_PATH");
        if (!string.IsNullOrWhiteSpace(configuredPath)) return configuredPath;

        string bundledName = OperatingSystem.IsWindows() ? "qpdf.exe" : "qpdf";
        string bundledPath = Path.Combine(AppContext.BaseDirectory, "tools", "qpdf", "bin", bundledName);
        return File.Exists(bundledPath) ? bundledPath : "qpdf";
    }

    private static void ConfigureBundledGhostscript(ProcessStartInfo startInfo)
    {
        string root = Path.Combine(AppContext.BaseDirectory, "tools", "ghostscript");
        string executableName = OperatingSystem.IsWindows() ? "gswin64c.exe" : "gs";
        string executable = Path.Combine(root, "bin", executableName);
        if (!string.Equals(startInfo.FileName, executable, StringComparison.OrdinalIgnoreCase)) return;

        string[] searchPaths =
        [
            Path.Combine(root, "Resource", "Init"),
            Path.Combine(root, "Resource"),
            Path.Combine(root, "lib"),
            Path.Combine(root, "fonts")
        ];
        startInfo.Environment["GS_LIB"] = string.Join(Path.PathSeparator, searchPaths);

        string nativeLibraryPath = Path.Combine(root, "lib");
        string existingLibraryPath = startInfo.Environment.TryGetValue("LD_LIBRARY_PATH", out string? value)
            ? value ?? string.Empty
            : string.Empty;
        startInfo.Environment["LD_LIBRARY_PATH"] = string.IsNullOrWhiteSpace(existingLibraryPath)
            ? nativeLibraryPath
            : nativeLibraryPath + Path.PathSeparator + existingLibraryPath;
    }

    private static void ConfigureBundledQpdf(ProcessStartInfo startInfo)
    {
        if (OperatingSystem.IsWindows()) return;

        string root = Path.Combine(AppContext.BaseDirectory, "tools", "qpdf");
        string executable = Path.Combine(root, "bin", "qpdf");
        if (!string.Equals(startInfo.FileName, executable, StringComparison.Ordinal)) return;

        string nativeLibraryPath = Path.Combine(root, "lib");
        string existingLibraryPath = startInfo.Environment.TryGetValue("LD_LIBRARY_PATH", out string? value)
            ? value ?? string.Empty
            : string.Empty;
        startInfo.Environment["LD_LIBRARY_PATH"] = string.IsNullOrWhiteSpace(existingLibraryPath)
            ? nativeLibraryPath
            : nativeLibraryPath + Path.PathSeparator + existingLibraryPath;
    }

    private static string FindGhostscriptExecutable()
    {
        string? configuredPath = Environment.GetEnvironmentVariable("COMPRESSMYWEB_GHOSTSCRIPT_PATH");
        if (!string.IsNullOrWhiteSpace(configuredPath)) return configuredPath;

        string bundledName = OperatingSystem.IsWindows() ? "gswin64c.exe" : "gs";
        string bundledPath = Path.Combine(AppContext.BaseDirectory, "tools", "ghostscript", "bin", bundledName);
        if (File.Exists(bundledPath)) return bundledPath;

        return OperatingSystem.IsWindows() ? "gswin64c.exe" : "gs";
    }

    private static string GetTargetDirectory(ImageQueueItem item, CompressionSettings settings)
    {
        string targetDir = string.IsNullOrWhiteSpace(settings.OutputDirectory)
            ? Path.GetDirectoryName(item.FilePath) ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            : settings.OutputDirectory;

        if (settings.PreserveFolderStructure && !string.IsNullOrWhiteSpace(item.RelativeDirectory))
        {
            targetDir = Path.Combine(targetDir, item.RelativeDirectory);
        }

        return targetDir;
    }

    private static byte[] BuildSingleImagePdf(byte[] jpeg, int pixelWidth, int pixelHeight)
    {
        // 96 pixels por polegada para obter uma dimensão de página previsível.
        double pageWidth = pixelWidth * 72d / 96d;
        double pageHeight = pixelHeight * 72d / 96d;
        string width = pageWidth.ToString("0.###", CultureInfo.InvariantCulture);
        string height = pageHeight.ToString("0.###", CultureInfo.InvariantCulture);
        byte[] content = Encoding.ASCII.GetBytes($"q\n{width} 0 0 {height} 0 0 cm\n/Im0 Do\nQ\n");

        using var output = new MemoryStream();
        var offsets = new List<long> { 0 };

        WriteAscii(output, "%PDF-1.4\n");
        output.Write(new byte[] { (byte)'%', 0xE2, 0xE3, 0xCF, 0xD3, (byte)'\n' });

        WriteObject(output, offsets, 1, "<< /Type /Catalog /Pages 2 0 R >>");
        WriteObject(output, offsets, 2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        WriteObject(output, offsets, 3,
            $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {width} {height}] /Resources << /XObject << /Im0 4 0 R >> >> /Contents 5 0 R >>");

        offsets.Add(output.Position);
        WriteAscii(output, $"4 0 obj\n<< /Type /XObject /Subtype /Image /Width {pixelWidth} /Height {pixelHeight} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length {jpeg.Length} >>\nstream\n");
        output.Write(jpeg);
        WriteAscii(output, "\nendstream\nendobj\n");

        offsets.Add(output.Position);
        WriteAscii(output, $"5 0 obj\n<< /Length {content.Length} >>\nstream\n");
        output.Write(content);
        WriteAscii(output, "endstream\nendobj\n");

        long xrefPosition = output.Position;
        WriteAscii(output, "xref\n0 6\n0000000000 65535 f \n");
        for (int index = 1; index <= 5; index++)
        {
            WriteAscii(output, $"{offsets[index]:D10} 00000 n \n");
        }

        WriteAscii(output, $"trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n{xrefPosition}\n%%EOF\n");
        return output.ToArray();
    }

    private static void WriteObject(Stream output, List<long> offsets, int number, string body)
    {
        offsets.Add(output.Position);
        WriteAscii(output, $"{number} 0 obj\n{body}\nendobj\n");
    }

    private static void WriteAscii(Stream output, string value)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(value);
        output.Write(bytes);
    }

    private static string FirstUsefulLine(params string[] messages)
    {
        foreach (string message in messages)
        {
            using var reader = new StringReader(message);
            while (reader.ReadLine() is { } line)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    return line.Trim();
                }
            }
        }

        return string.Empty;
    }

    private static CompressionResult Failure(string message) =>
        new(false, string.Empty, 0, 0, message);
}
