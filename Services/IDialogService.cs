using System.Threading.Tasks;

namespace CompressMyWeb.Services;

public interface IDialogService
{
    Task<string[]> PickFilesAsync(string title, string[]? extensions = null);
    Task<string?> PickFolderAsync(string title);
    Task ShowMessageAsync(string title, string message);
    Task ShowErrorAsync(string title, string message);
    void OpenFolderInExplorer(string folderPath);
}
