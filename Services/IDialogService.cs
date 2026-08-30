using System.Threading.Tasks;

namespace CompressMyWeb.Services;

public interface IDialogService
{
    Task<string[]> PickFilesAsync(string title, string[]? extensions = null);
    Task<string?> PickFolderAsync(string title);
    void OpenFolderInExplorer(string folderPath);
}
