using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

using Kudo.Demo.Desktop.ViewModels;

namespace Kudo.Demo.Desktop.Views;

/// <summary>状态栏：运行态指示灯颜色跟随 StatusText 语义（就绪/运行/暂停/停止）。</summary>
public partial class StatusBarView : UserControl
{
    private static readonly IReadOnlyDictionary<string, IBrush> StatusBrushes = new Dictionary<string, IBrush>
    {
        ["就绪"] = new SolidColorBrush(Color.Parse("#22C55E")),
        ["Ready"] = new SolidColorBrush(Color.Parse("#22C55E")),
        ["运行"] = new SolidColorBrush(Color.Parse("#3B82F6")),
        ["Running"] = new SolidColorBrush(Color.Parse("#3B82F6")),
        ["暂停"] = new SolidColorBrush(Color.Parse("#F59E0B")),
        ["Paused"] = new SolidColorBrush(Color.Parse("#F59E0B")),
        ["已停止"] = new SolidColorBrush(Color.Parse("#9CA3AF")),
        ["Stopped"] = new SolidColorBrush(Color.Parse("#9CA3AF")),
    };

    private static readonly IBrush DefaultStatusBrush = new SolidColorBrush(Color.Parse("#9CA3AF"));

    public StatusBarView()
    {
        InitializeComponent();
        DataContextChanged += HandleDataContextChanged;
    }

    private void HandleDataContextChanged(object? sender, EventArgs args)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(MainWindowViewModel.StatusText))
                {
                    UpdateStatusLight(viewModel.StatusText);
                }
            };
            UpdateStatusLight(viewModel.StatusText);
        }
    }

    private void UpdateStatusLight(string? statusText)
    {
        if (StatusLight is null)
        {
            return;
        }

        var brush = DefaultStatusBrush;
        if (!string.IsNullOrWhiteSpace(statusText))
        {
            foreach (var keyword in StatusBrushes)
            {
                if (statusText.Contains(keyword.Key, StringComparison.Ordinal))
                {
                    brush = keyword.Value;
                    break;
                }
            }
        }

        // 画刷更新走 UI 线程；运行态回调可能来自后台。
        Dispatcher.UIThread.Post(() => StatusLight.Fill = brush);
    }
}
