using CompressMyWeb.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CompressMyWeb.Services;

public sealed class PdfToolService : IPdfToolService
{
    private static readonly Regex PageRangePattern = new(@"^(?<start>[1-9]\d*)(?:-(?<end>[1-9]\d*))?$", RegexOptions.Compiled);

    public async Task<PdfOperationResult> SplitRangesAsync(
        string inputPath,
        string outputDirectory,
        string outputPrefix,
        string ranges,
        CancellationToken cancellationToken = default)
    {
        var temporaryFiles = new List<string>();
        try
        {
            if (!File.Exists(inputPath)) return Failure("Arquivo PDF original não encontrado.");
            if (string.IsNullOrWhiteSpace(outputDirectory)) return Failure("Pasta de destino inválida.");
            if (!IsValidOutputPrefix(outputPrefix)) return Failure("Informe um prefixo de arquivo válido.");

            Directory.CreateDirectory(outputDirectory);
            int pageCount = await GetPageCountAsync(inputPath, cancellationToken);
            var parsedRanges = ParseRanges(ranges, pageCount);
            if (parsedRanges.Count == 0) return Failure("Informe ao menos um intervalo de páginas.");

            var destinations = new List<string>();
            foreach (var range in parsedRanges)
            {
                string destination = Path.Combine(outputDirectory, $"{outputPrefix}-parte-{range.FileLabel}.pdf");
                if (File.Exists(destination)) return Failure($"Já existe o arquivo {Path.GetFileName(destination)} na pasta de destino.");
                destinations.Add(destination);
            }

            for (int index = 0; index < parsedRanges.Count; index++)
            {
                string temporaryPath = Path.Combine(outputDirectory, $".{outputPrefix}_{Guid.NewGuid():N}.tmp.pdf");
                temporaryFiles.Add(temporaryPath);
                var startInfo = CreateQpdfStartInfo();
                startInfo.ArgumentList.Add(inputPath);
                startInfo.ArgumentList.Add("--pages");
                startInfo.ArgumentList.Add(".");
                startInfo.ArgumentList.Add(parsedRanges[index].Argument);
                startInfo.ArgumentList.Add("--");
                startInfo.ArgumentList.Add(temporaryPath);

                var execution = await RunQpdfAsync(startInfo, cancellationToken);
                if (!execution.Success) return Failure(execution.ErrorMessage);
            }

            for (int index = 0; index < temporaryFiles.Count; index++)
            {
                File.Move(temporaryFiles[index], destinations[index]);
            }
            temporaryFiles.Clear();
            return new PdfOperationResult(true, outputDirectory);
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
            foreach (string temporaryFile in temporaryFiles)
            {
                if (File.Exists(temporaryFile))
                {
                    try { File.Delete(temporaryFile); } catch { }
                }
            }
        }
    }

    public async Task<PdfOperationResult> SplitEachPageAsync(
        string inputPath,
        string outputDirectory,
        string outputPrefix,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(inputPath)) return Failure("Arquivo PDF original não encontrado.");
            if (string.IsNullOrWhiteSpace(outputDirectory)) return Failure("Pasta de destino inválida.");
            if (!IsValidOutputPrefix(outputPrefix))
            {
                return Failure("Informe um prefixo de arquivo válido.");
            }

            Directory.CreateDirectory(outputDirectory);
            int pageCount = await GetPageCountAsync(inputPath, cancellationToken);
            if (pageCount < 1) return Failure("O PDF não possui páginas que possam ser divididas.");

            for (int page = 1; page <= pageCount; page++)
            {
                string candidate = Path.Combine(outputDirectory, $"{outputPrefix}-{page}.pdf");
                if (File.Exists(candidate)) return Failure($"Já existe o arquivo {Path.GetFileName(candidate)} na pasta de destino.");
            }

            var startInfo = CreateQpdfStartInfo();
            startInfo.ArgumentList.Add(inputPath);
            startInfo.ArgumentList.Add("--split-pages=1");
            startInfo.ArgumentList.Add(Path.Combine(outputDirectory, $"{outputPrefix}-%d.pdf"));

            var execution = await RunQpdfAsync(startInfo, cancellationToken);
            if (!execution.Success) return Failure(execution.ErrorMessage);
            return new PdfOperationResult(true, outputDirectory);
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

    public async Task<PdfOperationResult> MergeAsync(
        IReadOnlyList<PdfMergeItem> files,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        string temporaryPath = string.Empty;

        try
        {
            if (files.Count < 2)
            {
                return Failure("Adicione pelo menos dois arquivos PDF.");
            }

            if (files.Any(file => !File.Exists(file.FilePath)))
            {
                return Failure("Um dos arquivos selecionados não foi encontrado.");
            }

            string fullDestinationPath = Path.GetFullPath(destinationPath);
            fullDestinationPath = GetAvailableFilePath(fullDestinationPath);

            string? destinationDirectory = Path.GetDirectoryName(fullDestinationPath);
            if (string.IsNullOrWhiteSpace(destinationDirectory))
            {
                return Failure("Pasta de destino inválida.");
            }

            Directory.CreateDirectory(destinationDirectory);
            temporaryPath = Path.Combine(destinationDirectory, $".{Path.GetFileNameWithoutExtension(fullDestinationPath)}_{Guid.NewGuid():N}.tmp.pdf");

            var startInfo = CreateQpdfStartInfo();
            startInfo.ArgumentList.Add("--empty");
            startInfo.ArgumentList.Add("--pages");
            foreach (var file in files)
            {
                startInfo.ArgumentList.Add(file.FilePath);
                startInfo.ArgumentList.Add("1-z");
            }
            startInfo.ArgumentList.Add("--");
            startInfo.ArgumentList.Add(temporaryPath);

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
            if (process.ExitCode is not (0 or 3) || !File.Exists(temporaryPath))
            {
                string details = FirstUsefulLine(errorMessage, outputMessage);
                return Failure(string.IsNullOrWhiteSpace(details) ? $"qpdf encerrou com o código {process.ExitCode}." : details);
            }

            File.Move(temporaryPath, fullDestinationPath);
            temporaryPath = string.Empty;
            return new PdfOperationResult(true, fullDestinationPath);
        }
        catch (Win32Exception)
        {
            return Failure("qpdf não foi encontrado. Instale o qpdf ou use a versão offline do aplicativo.");
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
            if (!string.IsNullOrEmpty(temporaryPath) && File.Exists(temporaryPath))
            {
                try { File.Delete(temporaryPath); } catch { }
            }
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

    private static bool IsValidOutputPrefix(string outputPrefix) =>
        !string.IsNullOrWhiteSpace(outputPrefix) &&
        outputPrefix.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
        string.Equals(outputPrefix, Path.GetFileName(outputPrefix), StringComparison.Ordinal);

    private static string GetAvailableFilePath(string requestedPath)
    {
        if (!File.Exists(requestedPath)) return requestedPath;

        string? directory = Path.GetDirectoryName(requestedPath);
        string fileName = Path.GetFileNameWithoutExtension(requestedPath);
        string extension = Path.GetExtension(requestedPath);
        for (int suffix = 1; ; suffix++)
        {
            string candidate = Path.Combine(directory ?? string.Empty, $"{fileName}-{suffix}{extension}");
            if (!File.Exists(candidate)) return candidate;
        }
    }

    private static List<PageRange> ParseRanges(string ranges, int pageCount)
    {
        var result = new List<PageRange>();
        var selectedPages = new HashSet<int>();
        foreach (string token in ranges.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            Match match = PageRangePattern.Match(token);
            if (!match.Success) throw new InvalidOperationException($"Intervalo inválido: {token}.");

            int start = int.Parse(match.Groups["start"].Value);
            int end = match.Groups["end"].Success ? int.Parse(match.Groups["end"].Value) : start;
            if (end < start || end > pageCount)
            {
                throw new InvalidOperationException($"O intervalo {token} está fora das {pageCount} página(s) do PDF.");
            }
            for (int page = start; page <= end; page++)
            {
                if (!selectedPages.Add(page)) throw new InvalidOperationException($"A página {page} foi informada mais de uma vez.");
            }

            result.Add(new PageRange(start, end));
        }
        return result;
    }

    private static ProcessStartInfo CreateQpdfStartInfo()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = FindQpdfExecutable(),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        ConfigureBundledQpdf(startInfo);
        return startInfo;
    }

    private static async Task<int> GetPageCountAsync(string inputPath, CancellationToken cancellationToken)
    {
        var startInfo = CreateQpdfStartInfo();
        startInfo.ArgumentList.Add("--show-npages");
        startInfo.ArgumentList.Add(inputPath);
        var execution = await RunQpdfAsync(startInfo, cancellationToken);
        if (!execution.Success) throw new InvalidOperationException(execution.ErrorMessage);
        return int.TryParse(execution.Output.Trim(), out int pageCount) ? pageCount : 0;
    }

    private static async Task<(bool Success, string Output, string ErrorMessage)> RunQpdfAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken)
    {
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
            if (process.ExitCode is not (0 or 3))
            {
                string details = FirstUsefulLine(errorMessage, outputMessage);
                return (false, outputMessage, string.IsNullOrWhiteSpace(details) ? $"qpdf encerrou com o código {process.ExitCode}." : details);
            }
            return (true, outputMessage, string.Empty);
        }
        catch (Win32Exception)
        {
            return (false, string.Empty, "qpdf não foi encontrado. Instale o qpdf ou use a versão offline do aplicativo.");
        }
    }

    private static void ConfigureBundledQpdf(ProcessStartInfo startInfo)
    {
        if (OperatingSystem.IsWindows()) return;

        string root = Path.Combine(AppContext.BaseDirectory, "tools", "qpdf");
        string executable = Path.Combine(root, "bin", "qpdf");
        if (!string.Equals(startInfo.FileName, executable, StringComparison.Ordinal)) return;

        string nativeLibraryPath = Path.Combine(root, "lib");
        string existingLibraryPath = startInfo.Environment.TryGetValue("LD_LIBRARY_PATH", out string? value) ? value ?? string.Empty : string.Empty;
        startInfo.Environment["LD_LIBRARY_PATH"] = string.IsNullOrWhiteSpace(existingLibraryPath)
            ? nativeLibraryPath
            : nativeLibraryPath + Path.PathSeparator + existingLibraryPath;
    }

    private static string FirstUsefulLine(params string[] messages)
    {
        foreach (string message in messages)
        {
            using var reader = new StringReader(message);
            while (reader.ReadLine() is { } line)
            {
                if (!string.IsNullOrWhiteSpace(line)) return line.Trim();
            }
        }

        return string.Empty;
    }

    private static PdfOperationResult Failure(string message) => new(false, string.Empty, message);

    private sealed record PageRange(int Start, int End)
    {
        public string Argument => Start == End ? Start.ToString() : $"{Start}-{End}";
        public string FileLabel => Argument;
    }
}
