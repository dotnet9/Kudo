using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Kudo.Demo.Desktop.ViewModels;
using TKCode.Arc;

namespace Kudo.Demo.Desktop.Views;

/// <summary>设置窗口：主题 / 语言 / 关于。绑定主窗口同一 ViewModel，改动即时生效。</summary>
public partial class SettingsWindow : ArcWindow
{
    public SettingsWindow()
    {
        InitializeComponent();
    }

    private void HandleOpenRepository(object? sender, RoutedEventArgs args)
    {
        var start = new ProcessStartInfo("https://github.com/dotnet9/Kudo")
        {
            UseShellExecute = true
        };
        Process.Start(start);
    }
}
