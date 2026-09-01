using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Layout;
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

    public async Task ShowMessageAsync(string title, string message)
    {
        await ShowNotificationAsync(title, "✓ Concluído com sucesso", message, Avalonia.Media.Brushes.ForestGreen);
    }

    public async Task ShowErrorAsync(string title, string message)
    {
        await ShowNotificationAsync(title, "⚠ Não foi possível concluir", message, Avalonia.Media.Brushes.Firebrick);
    }

    private static async Task ShowNotificationAsync(string title, string heading, string message, Avalonia.Media.IBrush headingBrush)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 440,
            Height = 210,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = Avalonia.Media.Brushes.White,
            Content = new Border
            {
                Margin = new Thickness(18),
                Padding = new Thickness(20),
                BorderBrush = Avalonia.Media.Brushes.LightGray,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Child = CreateMessageContent(heading, message, headingBrush, out var closeButton)
            }
        };

        closeButton.Click += (_, _) => dialog.Close();
        var owner = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (owner is not null)
        {
            await dialog.ShowDialog(owner);
        }
        else
        {
            dialog.Show();
        }
    }

    private static StackPanel CreateMessageContent(string heading, string message, Avalonia.Media.IBrush headingBrush, out Button closeButton)
    {
        closeButton = new Button
        {
            Content = "Fechar",
            Padding = new Thickness(20, 8),
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var panel = new StackPanel { Spacing = 14 };
        panel.Children.Add(new TextBlock
        {
            Text = heading,
            FontSize = 19,
            FontWeight = Avalonia.Media.FontWeight.Bold,
            Foreground = headingBrush
        });
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        panel.Children.Add(closeButton);
        return panel;
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
