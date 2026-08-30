using Avalonia.Controls;
using Avalonia.Input;
using CompressMyWeb.ViewModels;
using System.Linq;

namespace CompressMyWeb.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains(DataFormats.Files))
        {
            e.DragEffects = DragDropEffects.Copy;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains(DataFormats.Files))
        {
            var files = e.Data.GetFiles();
            if (files != null)
            {
                var paths = files.Select(f => f.Path.LocalPath).ToList();
                if (DataContext is MainViewModel vm)
                {
                    vm.AddPaths(paths);
                }
            }
        }
    }

    private async void AboutButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await new AboutWindow().ShowDialog(this);
    }
}
