using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CompressMyWeb.Services;

public class AvaloniaDialogService : IDialogService
{
    private IStorageProvider? GetStorageProvider()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow?.StorageProvider;
        }
        return null;
    }

    public async Task<string[]> PickFilesAsync(string title, string[]? extensions = null)
    {
        var storage = GetStorageProvider();
        if (storage == null) return Array.Empty<string>();

        var options = new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = true
        };

        if (extensions != null && extensions.Length > 0)
        {
            options.FileTypeFilter = new List<FilePickerFileType>
            {
                new("Imagens Suportadas")
                {
                    Patterns = extensions.Select(ext => $"*{ext}").ToList()
                },
                new("Todos os Arquivos")
                {
                    Patterns = new List<string> { "*.*" }
                }
            };
        }

        var results = await storage.OpenFilePickerAsync(options);
        return results.Select(f => f.Path.LocalPath).ToArray();
    }

    public async Task<string?> PickFolderAsync(string title)
    {
        var storage = GetStorageProvider();
        if (storage == null) return null;

        var options = new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        };

        var results = await storage.OpenFolderPickerAsync(options);
        var folder = results.FirstOrDefault();
        return folder?.Path.LocalPath;
    }

    public void OpenFolderInExplorer(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath)) return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = folderPath,
                UseShellExecute = true
            });
        }
        catch
        {
            // Tratamento silencioso caso o gerenciador padrão do Linux/Mint não suporte UseShellExecute direto
            try
            {
                Process.Start("xdg-open", folderPath);
            }
            catch { }
        }
    }
}
