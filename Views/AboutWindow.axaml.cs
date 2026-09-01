using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Reflection;

namespace CompressMyWeb.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        string version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.6.0";
        VersionText.Text = $"Versão {version}";
    }

    private void CloseButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
