using CommunityToolkit.Mvvm.ComponentModel;
using CompressMyWeb.Helpers;
using System.IO;

namespace CompressMyWeb.Models;

public partial class ImageQueueItem : ObservableObject
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string RelativeDirectory { get; set; } = string.Empty;
    public long OriginalSizeBytes { get; set; }
    public string OriginalSizeFormatted => FileSizeFormatter.Format(OriginalSizeBytes);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NewSizeFormatted))]
    [NotifyPropertyChangedFor(nameof(SavingsFormatted))]
    [NotifyPropertyChangedFor(nameof(ReductionPercentage))]
    private long _newSizeBytes;

    public string NewSizeFormatted => NewSizeBytes > 0 ? FileSizeFormatter.Format(NewSizeBytes) : "-";

    [ObservableProperty]
    private string _outputFilePath = string.Empty;

    [ObservableProperty]
    private string _outputFormatDisplay = "-";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(IsProcessing))]
    [NotifyPropertyChangedFor(nameof(IsCompleted))]
    [NotifyPropertyChangedFor(nameof(IsError))]
    [NotifyPropertyChangedFor(nameof(IsPending))]
    private FileStatus _status = FileStatus.Pending;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public long SavingsBytes => (OriginalSizeBytes > NewSizeBytes && NewSizeBytes > 0) ? (OriginalSizeBytes - NewSizeBytes) : 0;

    public string SavingsFormatted => SavingsBytes > 0 ? FileSizeFormatter.Format(SavingsBytes) : "-";

    public string ReductionPercentage
    {
        get
        {
            if (Status != FileStatus.Completed || OriginalSizeBytes == 0 || NewSizeBytes == 0)
                return "-";

            if (NewSizeBytes >= OriginalSizeBytes)
            {
                double increase = ((double)(NewSizeBytes - OriginalSizeBytes) / OriginalSizeBytes) * 100;
                return $"+{increase:F1}%";
            }

            double reduction = ((double)(OriginalSizeBytes - NewSizeBytes) / OriginalSizeBytes) * 100;
            return $"-{reduction:F1}%";
        }
    }

    public string StatusText => Status switch
    {
        FileStatus.Pending => "Pendente",
        FileStatus.Processing => "Processando...",
        FileStatus.Completed => "Concluído",
        FileStatus.Error => "Erro",
        FileStatus.Canceled => "Cancelado",
        _ => "Desconhecido"
    };

    public bool IsPending => Status == FileStatus.Pending;
    public bool IsProcessing => Status == FileStatus.Processing;
    public bool IsCompleted => Status == FileStatus.Completed;
    public bool IsError => Status == FileStatus.Error;

    public static ImageQueueItem FromFile(string filePath)
    {
        var fileInfo = new FileInfo(filePath);
        return new ImageQueueItem
        {
            FilePath = filePath,
            FileName = fileInfo.Name,
            OriginalSizeBytes = fileInfo.Exists ? fileInfo.Length : 0,
            Status = FileStatus.Pending
        };
    }
}
