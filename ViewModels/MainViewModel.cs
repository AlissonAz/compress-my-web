using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CompressMyWeb.Helpers;
using CompressMyWeb.Models;
using CompressMyWeb.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CompressMyWeb.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly IImageCompressionService _compressionService;
    private readonly IPdfCompressionService _pdfCompressionService;
    private readonly IDialogService _dialogService;
    private CancellationTokenSource? _cancellationTokenSource;

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".tiff", ".tif", ".gif", ".webp", ".pdf"
    };

    public IReadOnlyList<OutputFormatOption> OutputFormats { get; } =
    [
        new(OutputFormat.Webp, "WebP"),
        new(OutputFormat.Jpeg, "JPEG / JPG"),
        new(OutputFormat.Png, "PNG"),
        new(OutputFormat.Pdf, "PDF"),
        new(OutputFormat.Original, "Manter formato original")
    ];

    [ObservableProperty]
    private ObservableCollection<ImageQueueItem> _queueItems = new();

    [ObservableProperty]
    private CompressionSettings _settings = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStart))]
    [NotifyPropertyChangedFor(nameof(CanModifyQueue))]
    private bool _isProcessing;

    [ObservableProperty]
    private int _processedCount;

    [ObservableProperty]
    private int _totalToProcess;

    [ObservableProperty]
    private string _currentFileName = string.Empty;

    [ObservableProperty]
    private double _overallProgress;

    [ObservableProperty]
    private string _statusMessage = "Pronto. Arraste imagens ou PDFs, ou clique em Adicionar.";

    [ObservableProperty]
    private string _totalSavedFormatted = "0 B";

    [ObservableProperty]
    private string _totalReductionPercentage = "0%";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStart))]
    private bool _hasItems;

    [ObservableProperty]
    private bool _showAdvancedSettings;

    public bool CanStart => !IsProcessing && QueueItems.Any(i => i.Status == FileStatus.Pending || i.Status == FileStatus.Error);
    public bool CanModifyQueue => !IsProcessing;

    public MainViewModel() : this(new ImageCompressionService(), new PdfCompressionService(), new AvaloniaDialogService())
    {
    }

    public MainViewModel(IImageCompressionService compressionService, IDialogService dialogService)
        : this(compressionService, new PdfCompressionService(), dialogService)
    {
    }

    public MainViewModel(
        IImageCompressionService compressionService,
        IPdfCompressionService pdfCompressionService,
        IDialogService dialogService)
    {
        _compressionService = compressionService;
        _pdfCompressionService = pdfCompressionService;
        _dialogService = dialogService;

        Settings.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(CompressionSettings.OutputFormat))
            {
                UpdateQueueOutputFormats();
            }
        };

        // Diretório padrão para os arquivos convertidos
        string defaultPictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        Settings.OutputDirectory = Path.Combine(string.IsNullOrEmpty(defaultPictures) ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) : defaultPictures, "imagens_comprimidas");
        ApplyPreset("Web");
    }

    [RelayCommand]
    public async Task AddFilesAsync()
    {
        var extensions = SupportedExtensions.ToArray();
        var files = await _dialogService.PickFilesAsync("Selecionar Imagens e PDFs", extensions);
        AddPaths(files);
    }

    [RelayCommand]
    public async Task AddFolderAsync()
    {
        var folder = await _dialogService.PickFolderAsync("Selecionar Pasta com Imagens");
        if (!string.IsNullOrWhiteSpace(folder))
        {
            AddPaths(new[] { folder });
        }
    }

    [RelayCommand]
    public async Task SelectOutputFolderAsync()
    {
        var folder = await _dialogService.PickFolderAsync("Selecionar Pasta de Destino");
        if (!string.IsNullOrWhiteSpace(folder))
        {
            Settings.OutputDirectory = folder;
        }
    }

    [RelayCommand]
    public void OpenOutputFolder()
    {
        _dialogService.OpenFolderInExplorer(Settings.OutputDirectory);
    }

    [RelayCommand]
    public void ClearQueue()
    {
        if (IsProcessing) return;
        QueueItems.Clear();
        UpdateStats();
        StatusMessage = "Fila limpa.";
    }

    [RelayCommand]
    public void RemoveItem(ImageQueueItem? item)
    {
        if (item != null && !IsProcessing)
        {
            QueueItems.Remove(item);
            UpdateStats();
        }
    }

    [RelayCommand]
    public void ApplyPreset(string preset)
    {
        switch (preset)
        {
            case "Web": // Recomendado para Web padrão
                Settings.Quality = 80;
                Settings.OptimizePdfImages = true;
                Settings.IsLossless = false;
                Settings.EnableResize = true;
                Settings.MaxWidth = 1920;
                Settings.MaxHeight = 1080;
                break;
            case "HighQuality": // Alta fidelidade
                Settings.Quality = 90;
                Settings.OptimizePdfImages = true;
                Settings.IsLossless = false;
                Settings.EnableResize = false;
                break;
            case "Lossless": // Sem perdas (gráficos, logos, prints)
                Settings.Quality = 100;
                Settings.OptimizePdfImages = false;
                Settings.IsLossless = true;
                Settings.EnableResize = false;
                break;
            case "MaxCompression": // Máxima redução (thumbnails, e-commerce pesado)
                Settings.Quality = 60;
                Settings.OptimizePdfImages = true;
                Settings.IsLossless = false;
                Settings.EnableResize = true;
                Settings.MaxWidth = 1280;
                Settings.MaxHeight = 720;
                break;
        }
    }

    [RelayCommand]
    public void ToggleAdvancedSettings()
    {
        ShowAdvancedSettings = !ShowAdvancedSettings;
    }

    public void AddPaths(IEnumerable<string> paths)
    {
        var existingPaths = new HashSet<string>(QueueItems.Select(q => q.FilePath), StringComparer.OrdinalIgnoreCase);

        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path)) continue;

            if (Directory.Exists(path))
            {
                try
                {
                    string rootDir = Path.GetFullPath(path);
                    var files = Directory.EnumerateFiles(rootDir, "*.*", SearchOption.AllDirectories)
                        .Where(f => SupportedExtensions.Contains(Path.GetExtension(f)));

                    foreach (var file in files)
                    {
                        if (!existingPaths.Contains(file))
                        {
                            var item = ImageQueueItem.FromFile(file);
                            string? fileDir = Path.GetDirectoryName(Path.GetFullPath(file));
                            if (!string.IsNullOrEmpty(fileDir) && fileDir.Length > rootDir.Length)
                            {
                                item.RelativeDirectory = Path.GetRelativePath(rootDir, fileDir);
                            }
                            QueueItems.Add(item);
                            existingPaths.Add(file);
                        }
                    }
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Erro ao ler pasta: {ex.Message}";
                }
            }
            else if (File.Exists(path) && SupportedExtensions.Contains(Path.GetExtension(path)))
            {
                if (!existingPaths.Contains(path))
                {
                    QueueItems.Add(ImageQueueItem.FromFile(path));
                    existingPaths.Add(path);
                }
            }
        }

        UpdateStats();
        UpdateQueueOutputFormats();
    }

    [RelayCommand]
    public async Task StartConversionAsync()
    {
        if (IsProcessing) return;

        var itemsToProcess = QueueItems.Where(i => i.Status == FileStatus.Pending || i.Status == FileStatus.Error).ToList();
        if (!itemsToProcess.Any())
        {
            StatusMessage = "Nenhuma imagem pendente na fila.";
            return;
        }

        IsProcessing = true;
        _cancellationTokenSource = new CancellationTokenSource();
        var token = _cancellationTokenSource.Token;

        TotalToProcess = itemsToProcess.Count;
        ProcessedCount = 0;
        OverallProgress = 0;

        StatusMessage = $"Iniciando processamento de {TotalToProcess} arquivo(s)...";

        int successCount = 0;
        int errorCount = 0;

        try
        {
            // Processamento sequencial (1 a 1)
            for (int i = 0; i < itemsToProcess.Count; i++)
            {
                if (token.IsCancellationRequested)
                {
                    StatusMessage = "Processo cancelado pelo usuário.";
                    break;
                }

                var item = itemsToProcess[i];
                item.Status = FileStatus.Processing;
                CurrentFileName = item.FileName;
                StatusMessage = $"Convertendo ({i + 1}/{TotalToProcess}): {item.FileName}...";

                bool isPdf = string.Equals(Path.GetExtension(item.FilePath), ".pdf", StringComparison.OrdinalIgnoreCase);
                CompressionResult result;

                result = isPdf
                    ? await _pdfCompressionService.CompressAsync(item, Settings, token)
                    : Settings.OutputFormat == OutputFormat.Pdf
                        ? await _pdfCompressionService.ConvertImageToPdfAsync(item, Settings, token)
                        : await _compressionService.CompressAsync(item, Settings, token);

                if (result.Success)
                {
                    item.Status = FileStatus.Completed;
                    item.OutputFilePath = result.OutputPath;
                    item.NewSizeBytes = result.NewSizeBytes;
                    successCount++;
                }
                else
                {
                    item.Status = FileStatus.Error;
                    item.ErrorMessage = result.ErrorMessage ?? "Falha na conversão";
                    errorCount++;
                }

                ProcessedCount = i + 1;
                OverallProgress = ((double)ProcessedCount / TotalToProcess) * 100;
                UpdateStats();
            }

            if (!token.IsCancellationRequested)
            {
                StatusMessage = $"Concluído! {successCount} arquivo(s) processado(s) com sucesso" + (errorCount > 0 ? $", {errorCount} com erro." : ".");
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Conversão cancelada.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erro inesperado: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
            CurrentFileName = string.Empty;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            UpdateStats();
        }
    }

    [RelayCommand]
    public void CancelConversion()
    {
        if (IsProcessing && _cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
        {
            _cancellationTokenSource.Cancel();
            StatusMessage = "Cancelando conversão...";
        }
    }

    private void UpdateStats()
    {
        HasItems = QueueItems.Count > 0;

        long totalOriginal = QueueItems.Where(i => i.Status == FileStatus.Completed).Sum(i => i.OriginalSizeBytes);
        long totalNew = QueueItems.Where(i => i.Status == FileStatus.Completed).Sum(i => i.NewSizeBytes);

        long saved = (totalOriginal > totalNew && totalNew > 0) ? (totalOriginal - totalNew) : 0;
        TotalSavedFormatted = FileSizeFormatter.Format(saved);

        if (totalOriginal > 0 && totalNew > 0)
        {
            double reduction = ((double)(totalOriginal - totalNew) / totalOriginal) * 100;
            TotalReductionPercentage = $"{reduction:F1}%";
        }
        else
        {
            TotalReductionPercentage = "0%";
        }

        OnPropertyChanged(nameof(CanStart));
    }

    private void UpdateQueueOutputFormats()
    {
        string selectedFormat = OutputFormats.First(option => option.Value == Settings.OutputFormat).Label;

        foreach (var item in QueueItems)
        {
            item.OutputFormatDisplay = string.Equals(
                Path.GetExtension(item.FilePath),
                ".pdf",
                StringComparison.OrdinalIgnoreCase)
                ? "PDF"
                : selectedFormat;
        }
    }
}

public sealed record OutputFormatOption(OutputFormat Value, string Label);
