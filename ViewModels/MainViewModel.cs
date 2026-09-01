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
    private readonly IPdfToolService _pdfToolService;
    private readonly IDialogService _dialogService;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _conversionDefaultsConfigured;

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

    [ObservableProperty]
    private ObservableCollection<PdfMergeItem> _mergeItems = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanMerge))]
    private bool _isMerging;

    [ObservableProperty]
    private string _mergeOutputDirectory = string.Empty;

    [ObservableProperty]
    private string _mergeOutputFileName = "pdf-unido.pdf";

    [ObservableProperty]
    private string _mergeStatusMessage = "Adicione pelo menos dois arquivos PDF na ordem desejada.";

    private CancellationTokenSource? _mergeCancellationTokenSource;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSplit))]
    private string _splitInputPath = string.Empty;

    [ObservableProperty]
    private string _splitOutputDirectory = string.Empty;

    [ObservableProperty]
    private string _splitOutputPrefix = "pagina";

    public IReadOnlyList<PdfSplitModeOption> SplitModes { get; } =
    [
        new(PdfSplitMode.EachPage, "Cada página em um PDF"),
        new(PdfSplitMode.Ranges, "Intervalos personalizados")
    ];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSplitByRanges))]
    private PdfSplitMode _splitMode = PdfSplitMode.EachPage;

    [ObservableProperty]
    private string _splitRanges = "1-3,5,8-10";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSplit))]
    private bool _isSplitting;

    [ObservableProperty]
    private string _splitStatusMessage = "Selecione um PDF para separar cada página em um arquivo.";

    private CancellationTokenSource? _splitCancellationTokenSource;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHome))]
    [NotifyPropertyChangedFor(nameof(IsWorkspace))]
    [NotifyPropertyChangedFor(nameof(IsFeaturePlaceholder))]
    [NotifyPropertyChangedFor(nameof(IsMergePage))]
    [NotifyPropertyChangedFor(nameof(StartProcessingButtonText))]
    [NotifyPropertyChangedFor(nameof(ProcessingButtonText))]
    [NotifyPropertyChangedFor(nameof(PageTitle))]
    [NotifyPropertyChangedFor(nameof(FeatureMessage))]
    private AppPage _currentPage = AppPage.Home;

    public bool IsHome => CurrentPage == AppPage.Home;
    public bool IsWorkspace => CurrentPage is AppPage.Compress or AppPage.Convert;
    public bool IsFeaturePlaceholder => CurrentPage == AppPage.SplitPdf;
    public bool IsMergePage => CurrentPage == AppPage.MergePdf;
    public bool MergeHasItems => MergeItems.Count > 0;
    public bool MergeIsEmpty => MergeItems.Count == 0;
    public string PageTitle => CurrentPage switch
    {
        AppPage.Convert => "Converter arquivos",
        AppPage.MergePdf => "Unir PDF",
        AppPage.SplitPdf => "Dividir PDF",
        _ => "Comprimir arquivos"
    };
    public string FeatureMessage => CurrentPage == AppPage.MergePdf
        ? "A ferramenta para unir PDFs será adicionada no próximo passo."
        : "A ferramenta para dividir PDFs será adicionada no próximo passo.";
    public bool CanMerge => !IsMerging && MergeItems.Count >= 2;
    public bool CanSplit => !IsSplitting && File.Exists(SplitInputPath);
    public bool IsSplitByRanges => SplitMode == PdfSplitMode.Ranges;
    public string StartProcessingButtonText => CurrentPage == AppPage.Convert ? "🔄 Iniciar conversão" : "⚡ Iniciar compressão";
    public string ProcessingButtonText => CurrentPage == AppPage.Convert ? "⏳ Convertendo... (cancelar)" : "⏳ Comprimindo... (cancelar)";

    public bool CanStart => !IsProcessing && QueueItems.Any(i => i.Status == FileStatus.Pending || i.Status == FileStatus.Error);
    public bool CanModifyQueue => !IsProcessing;

    public MainViewModel() : this(new ImageCompressionService(), new PdfCompressionService(), new PdfToolService(), new AvaloniaDialogService())
    {
    }

    public MainViewModel(IImageCompressionService compressionService, IDialogService dialogService)
        : this(compressionService, new PdfCompressionService(), new PdfToolService(), dialogService)
    {
    }

    public MainViewModel(
        IImageCompressionService compressionService,
        IPdfCompressionService pdfCompressionService,
        IDialogService dialogService)
        : this(compressionService, pdfCompressionService, new PdfToolService(), dialogService)
    {
    }

    public MainViewModel(
        IImageCompressionService compressionService,
        IPdfCompressionService pdfCompressionService,
        IPdfToolService pdfToolService,
        IDialogService dialogService)
    {
        _compressionService = compressionService;
        _pdfCompressionService = pdfCompressionService;
        _pdfToolService = pdfToolService;
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
        Settings.OutputDirectory = Path.Combine(string.IsNullOrEmpty(defaultPictures) ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) : defaultPictures, "Arquivos Compress-my-web");
        MergeOutputDirectory = Settings.OutputDirectory;
        SplitOutputDirectory = Settings.OutputDirectory;
        MergeItems.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(CanMerge));
            OnPropertyChanged(nameof(MergeHasItems));
            OnPropertyChanged(nameof(MergeIsEmpty));
        };
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
                Settings.EnableResize = false;
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

    [RelayCommand]
    public void GoHome()
    {
        if (!IsProcessing)
        {
            CurrentPage = AppPage.Home;
        }
    }

    [RelayCommand]
    public void NavigateTo(string? destination)
    {
        if (IsProcessing) return;

        CurrentPage = destination switch
        {
            "Compress" => AppPage.Compress,
            "Convert" => AppPage.Convert,
            "MergePdf" => AppPage.MergePdf,
            "SplitPdf" => AppPage.SplitPdf,
            _ => AppPage.Home
        };

        if (CurrentPage == AppPage.Convert && !_conversionDefaultsConfigured)
        {
            ApplyPreset("Lossless");
            _conversionDefaultsConfigured = true;
        }
    }

    [RelayCommand]
    public async Task AddMergeFilesAsync()
    {
        var files = await _dialogService.PickFilesAsync("Selecionar PDFs para unir", [".pdf"]);
        var existingPaths = new HashSet<string>(MergeItems.Select(item => item.FilePath), StringComparer.OrdinalIgnoreCase);

        foreach (string path in files)
        {
            if (File.Exists(path) && string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase) && existingPaths.Add(path))
            {
                MergeItems.Add(PdfMergeItem.FromFile(path));
            }
        }

        if (MergeItems.Count > 0) MergeStatusMessage = $"{MergeItems.Count} PDF(s) na ordem de união.";
    }

    [RelayCommand]
    public async Task SelectMergeOutputFolderAsync()
    {
        var folder = await _dialogService.PickFolderAsync("Selecionar pasta de destino");
        if (!string.IsNullOrWhiteSpace(folder)) MergeOutputDirectory = folder;
    }

    [RelayCommand]
    public void OpenMergeOutputFolder() => _dialogService.OpenFolderInExplorer(MergeOutputDirectory);

    [RelayCommand]
    public void RemoveMergeItem(PdfMergeItem? item)
    {
        if (item is not null && !IsMerging) MergeItems.Remove(item);
    }

    [RelayCommand]
    public void ClearMergeItems()
    {
        if (!IsMerging) MergeItems.Clear();
    }

    [RelayCommand]
    public void MoveMergeItemUp(PdfMergeItem? item) => MoveMergeItem(item, -1);

    [RelayCommand]
    public void MoveMergeItemDown(PdfMergeItem? item) => MoveMergeItem(item, 1);

    [RelayCommand]
    public async Task MergePdfsAsync()
    {
        if (!CanMerge) return;

        string fileName = MergeOutputFileName.Trim();
        if (string.IsNullOrWhiteSpace(fileName) || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || !string.Equals(fileName, Path.GetFileName(fileName), StringComparison.Ordinal))
        {
            MergeStatusMessage = "Informe um nome de arquivo PDF válido.";
            await _dialogService.ShowErrorAsync("Nome de arquivo inválido", MergeStatusMessage);
            return;
        }
        if (!fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) fileName += ".pdf";
        if (string.IsNullOrWhiteSpace(MergeOutputDirectory))
        {
            MergeStatusMessage = "Selecione uma pasta de destino.";
            await _dialogService.ShowErrorAsync("Pasta de destino inválida", MergeStatusMessage);
            return;
        }

        IsMerging = true;
        _mergeCancellationTokenSource = new CancellationTokenSource();
        MergeStatusMessage = "Unindo PDFs...";
        try
        {
            var result = await _pdfToolService.MergeAsync(MergeItems.ToList(), Path.Combine(MergeOutputDirectory, fileName), _mergeCancellationTokenSource.Token);
            if (result.Success)
            {
                string outputName = Path.GetFileName(result.OutputPath);
                MergeStatusMessage = $"PDF unido com sucesso: {outputName}";
                await _dialogService.ShowMessageAsync("PDF unido", $"O arquivo {outputName} foi criado na pasta selecionada.");
            }
            else
            {
                MergeStatusMessage = $"Não foi possível unir os PDFs: {result.ErrorMessage}";
                await _dialogService.ShowErrorAsync("Erro ao unir PDFs", result.ErrorMessage ?? "O qpdf não conseguiu processar os arquivos selecionados.");
            }
        }
        finally
        {
            IsMerging = false;
            _mergeCancellationTokenSource.Dispose();
            _mergeCancellationTokenSource = null;
        }
    }

    [RelayCommand]
    public void CancelMerge()
    {
        if (IsMerging && _mergeCancellationTokenSource is { IsCancellationRequested: false })
        {
            _mergeCancellationTokenSource.Cancel();
            MergeStatusMessage = "Cancelando união...";
        }
    }

    [RelayCommand]
    public async Task SelectSplitFileAsync()
    {
        var files = await _dialogService.PickFilesAsync("Selecionar PDF para dividir", [".pdf"]);
        string? file = files.FirstOrDefault(path => File.Exists(path) && string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase));
        if (file is null) return;

        SplitInputPath = file;
        SplitOutputPrefix = Path.GetFileNameWithoutExtension(file) + "-pagina";
        SplitStatusMessage = "Pronto para dividir cada página em um PDF separado.";
    }

    [RelayCommand]
    public async Task SelectSplitOutputFolderAsync()
    {
        var folder = await _dialogService.PickFolderAsync("Selecionar pasta de destino");
        if (!string.IsNullOrWhiteSpace(folder)) SplitOutputDirectory = folder;
    }

    [RelayCommand]
    public void OpenSplitOutputFolder() => _dialogService.OpenFolderInExplorer(SplitOutputDirectory);

    [RelayCommand]
    public async Task SplitPdfAsync()
    {
        if (!CanSplit) return;
        if (string.IsNullOrWhiteSpace(SplitOutputDirectory))
        {
            SplitStatusMessage = "Selecione uma pasta de destino.";
            return;
        }

        IsSplitting = true;
        _splitCancellationTokenSource = new CancellationTokenSource();
        SplitStatusMessage = "Dividindo PDF...";
        try
        {
            var result = SplitMode == PdfSplitMode.EachPage
                ? await _pdfToolService.SplitEachPageAsync(SplitInputPath, SplitOutputDirectory, SplitOutputPrefix.Trim(), _splitCancellationTokenSource.Token)
                : await _pdfToolService.SplitRangesAsync(SplitInputPath, SplitOutputDirectory, SplitOutputPrefix.Trim(), SplitRanges, _splitCancellationTokenSource.Token);
            SplitStatusMessage = result.Success
                ? SplitMode == PdfSplitMode.EachPage
                    ? "PDF dividido com sucesso. Um arquivo foi criado para cada página."
                    : "PDF dividido com sucesso. Um arquivo foi criado para cada intervalo."
                : $"Não foi possível dividir o PDF: {result.ErrorMessage}";
            if (result.Success)
            {
                string resultDescription = SplitMode == PdfSplitMode.EachPage
                    ? "Um arquivo foi criado para cada página."
                    : "Um arquivo foi criado para cada intervalo informado.";
                await _dialogService.ShowMessageAsync("PDF dividido", resultDescription);
            }
        }
        finally
        {
            IsSplitting = false;
            _splitCancellationTokenSource.Dispose();
            _splitCancellationTokenSource = null;
        }
    }

    [RelayCommand]
    public void CancelSplit()
    {
        if (IsSplitting && _splitCancellationTokenSource is { IsCancellationRequested: false })
        {
            _splitCancellationTokenSource.Cancel();
            SplitStatusMessage = "Cancelando divisão...";
        }
    }

    private void MoveMergeItem(PdfMergeItem? item, int direction)
    {
        if (item is null || IsMerging) return;
        int index = MergeItems.IndexOf(item);
        int targetIndex = index + direction;
        if (index >= 0 && targetIndex >= 0 && targetIndex < MergeItems.Count) MergeItems.Move(index, targetIndex);
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
                if (successCount > 0 && errorCount == 0)
                {
                    string actionName = CurrentPage == AppPage.Convert ? "conversão" : "compressão";
                    await _dialogService.ShowMessageAsync(
                        "Processamento concluído",
                        $"A {actionName} de {successCount} arquivo(s) foi concluída com sucesso.");
                }
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

public enum AppPage
{
    Home,
    Compress,
    Convert,
    MergePdf,
    SplitPdf
}

public enum PdfSplitMode
{
    EachPage,
    Ranges
}

public sealed record PdfSplitModeOption(PdfSplitMode Value, string Label);
