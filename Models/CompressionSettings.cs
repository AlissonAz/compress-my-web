using CommunityToolkit.Mvvm.ComponentModel;

namespace CompressMyWeb.Models;

public partial class CompressionSettings : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWebpOutput))]
    [NotifyPropertyChangedFor(nameof(IsPdfOutput))]
    [NotifyPropertyChangedFor(nameof(SupportsQuality))]
    private OutputFormat _outputFormat = OutputFormat.Webp;

    public bool IsWebpOutput => OutputFormat == OutputFormat.Webp;
    public bool IsPdfOutput => OutputFormat == OutputFormat.Pdf;
    public bool SupportsQuality => OutputFormat is OutputFormat.Webp or OutputFormat.Jpeg;

    [ObservableProperty]
    private string _outputDirectory = string.Empty;

    [ObservableProperty]
    private int _quality = 80;

    [ObservableProperty]
    private bool _isLossless = false;

    [ObservableProperty]
    private bool _enableResize = false;

    [ObservableProperty]
    private int _maxWidth = 1920;

    [ObservableProperty]
    private int _maxHeight = 1080;

    [ObservableProperty]
    private bool _deleteOriginal = false;

    [ObservableProperty]
    private bool _preserveFolderStructure = true;

    [ObservableProperty]
    private bool _stripMetadata = true;

    [ObservableProperty]
    private bool _optimizePdfImages = false;

    [ObservableProperty]
    private string _fileSuffix = string.Empty;
}
