using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TKCode.Arc;
using Kudo.Demo.Desktop.Behaviors;
using Kudo.Demo.Desktop.Services;
using Kudo.Demo.Desktop.ViewModels;
using Kudo.Core.Documents;

namespace Kudo.Demo.Desktop.Views;

/// <summary>命令面板条目：标题 + 快捷键提示 + 执行委托。</summary>
public sealed record PaletteCommand(string Title, string Gesture, Action Run);

public partial class MainWindow : ArcWindow
{
    private IReadOnlyList<PaletteCommand> _paletteCommands = [];

    public MainWindow() : this(DemoApplicationSettingsStore.CreateDefault())
    {
    }

    public MainWindow(
        MainWindowViewModel viewModel,
        DemoApplicationSettingsStore settingsStore) : this(
        settingsStore,
        null,
        null,
        viewModel)
    {
    }

    public MainWindow(
        DemoApplicationSettingsStore settingsStore,
        GraphDocumentDraftStore? draftStore = null,
        TimeSpan? draftAutosaveDelay = null) : this(
            settingsStore,
            draftStore,
            draftAutosaveDelay,
            null)
    {
    }

    private readonly DemoApplicationSettingsStore _settingsStore;

    private MainWindow(
        DemoApplicationSettingsStore settingsStore,
        GraphDocumentDraftStore? draftStore,
        TimeSpan? draftAutosaveDelay,
        MainWindowViewModel? viewModel)
    {
        _settingsStore = settingsStore;
        InitializeComponent();
        DataContext = viewModel ?? DataContext;
        if (this.FindControl<global::Avalonia.Controls.Button>("TitleBarSettingsButton") is { } settingsButton)
        {
            settingsButton.Click += HandleSettingsClicked;
        }

        // App 在构造完成后才注入 VM，故经 DataContextChanged 惰性订阅。
        DataContextChanged += HandleDataContextChangedForPalette;

        MainWindowBehavior.Attach(
            this,
            settingsStore,
            draftStore,
            draftAutosaveDelay);

        // 命令面板：Ctrl+K 唤出。Tunnel 路由让按键先于子控件标记已处理到达窗口。
        this.AddHandler(
            global::Avalonia.Input.InputElement.KeyDownEvent,
            HandlePaletteHotKey,
            global::Avalonia.Interactivity.RoutingStrategies.Tunnel |
            global::Avalonia.Interactivity.RoutingStrategies.Bubble);

        if (this.FindControl<global::Avalonia.Controls.TextBox>("CommandPaletteQuery") is { } paletteQuery)
        {
            paletteQuery.KeyDown += HandlePaletteQueryKeyDown;
            paletteQuery.TextChanged += (_, _) => RefreshPaletteList(paletteQuery.Text ?? string.Empty);
        }

        if (this.FindControl<global::Avalonia.Controls.ListBox>("CommandPaletteList") is { } paletteList)
        {
            paletteList.DoubleTapped += HandlePaletteListExecute;
        }

        // 首启欢迎卡：窗口加载后按持久化状态决定是否展示。
        Loaded += (_, _) => ShowWelcomeCardIfFirstRun();

    }

    /// <summary>Ctrl+K 唤出命令面板；Esc 在面板打开时关闭。</summary>
    private void HandlePaletteHotKey(object? sender, KeyEventArgs args)
    {
        if (args.Key == Key.K && args.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            args.Handled = true;
            if (DataContext is MainWindowViewModel viewModel)
            {
                viewModel.IsCommandPaletteOpen = !viewModel.IsCommandPaletteOpen;
            }
        }
        else if (args.Key == Key.Escape && CommandPaletteHost.IsVisible)
        {
            args.Handled = true;
            if (DataContext is MainWindowViewModel viewModel)
            {
                viewModel.IsCommandPaletteOpen = false;
            }
        }
    }

    /// <summary>DataContext 注入/切换后重新订阅 VM 属性变化（App 在构造完成后才注入 VM）。</summary>
    private void HandleDataContextChangedForPalette(object? sender, EventArgs args)
    {
        if (sender is MainWindow window && window.DataContext is MainWindowViewModel viewModel)
        {
            viewModel.PropertyChanged -= HandleMainWindowPropertyChanged;
            viewModel.PropertyChanged += HandleMainWindowPropertyChanged;
        }
    }

    /// <summary>命令面板可见性变化：打开时构建命令清单并聚焦查询框。</summary>
    private void HandleMainWindowPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(MainWindowViewModel.IsCommandPaletteOpen) &&
            DataContext is MainWindowViewModel { IsCommandPaletteOpen: true })
        {
            Dispatcher.UIThread.Post(() =>
            {
                _paletteCommands = BuildPaletteCommands();
                var query = this.FindControl<global::Avalonia.Controls.TextBox>("CommandPaletteQuery");
                if (query is not null)
                {
                    query.Text = string.Empty;
                }

                RefreshPaletteList(string.Empty);
                var listProbe = this.FindControl<global::Avalonia.Controls.ListBox>("CommandPaletteList");
                Dispatcher.UIThread.Post(() =>
                {
                    var boundsLine = listProbe is null ? "listProbe null" : $"listBounds={listProbe.Bounds} items={(listProbe.ItemsSource as System.Collections.ICollection)?.Count.ToString() ?? "-"} first={(listProbe.ItemsSource is System.Collections.IList collection && collection.Count > 0 ? collection[0]?.GetType().FullName + "/" + (collection[0] as PaletteCommand)?.Title ?? "?" : "-")}";
                    File.WriteAllText(Path.Combine(Path.GetTempPath(), "kudo_palette_list.txt"), boundsLine);
                }, global::Avalonia.Threading.DispatcherPriority.Background);
                query?.Focus();
            });
        }
    }

    /// <summary>打开设置窗口（主题 / 语言 / 关于），以主窗口为属主。</summary>
    private void OpenSettings()
    {
        var settings = new SettingsWindow
        {
            DataContext = DataContext
        };
        settings.Show(this);
    }

    private void HandleSettingsClicked(object? sender, RoutedEventArgs args) => OpenSettings();

    /// <summary>构建命令清单：画布命令 + 视图缩放 + 三品牌主题切换（兼快捷键速查）。</summary>
    private IReadOnlyList<PaletteCommand> BuildPaletteCommands()
    {
        var ribbon = this.FindControl<MainRibbonView>(MainWindowControlNames.Ribbon)?.DataContext as MainRibbonViewModel;
        var vm = DataContext as MainWindowViewModel;
        var commands = new List<PaletteCommand>();
        if (ribbon is not null)
        {
            commands.Add(new PaletteCommand("新建画布", "Ctrl+N", () => ribbon.CreateGraph()));
            commands.Add(new PaletteCommand("打开画布文件", "Ctrl+O", () => ribbon.OpenGraphCommand.Execute(null)));
            commands.Add(new PaletteCommand("保存画布", "Ctrl+S", () => ribbon.SaveGraphCommand.Execute(null)));
            commands.Add(new PaletteCommand("画布另存为", "", () => ribbon.SaveGraphAsCommand.Execute(null)));
            commands.Add(new PaletteCommand("撤销", "Ctrl+Z", () => ribbon.ExecuteDocumentEditAction(MainWindowDocumentEditAction.Undo)));
            commands.Add(new PaletteCommand("重做", "Ctrl+Y", () => ribbon.ExecuteDocumentEditAction(MainWindowDocumentEditAction.Redo)));
            commands.Add(new PaletteCommand("复制选中", "Ctrl+C", () => ribbon.ClipboardCommand.Execute(MainWindowClipboardAction.Copy)));
            commands.Add(new PaletteCommand("剪切选中", "Ctrl+X", () => ribbon.ClipboardCommand.Execute(MainWindowClipboardAction.Cut)));
            commands.Add(new PaletteCommand("粘贴", "Ctrl+V", () => ribbon.ClipboardCommand.Execute(MainWindowClipboardAction.Paste)));
            commands.Add(new PaletteCommand("启动运行", "F5", () => ribbon.StartRuntimeCommand.Execute(null)));
            commands.Add(new PaletteCommand("暂停运行", "", () => ribbon.PauseRuntimeCommand.Execute(null)));
            commands.Add(new PaletteCommand("停止运行", "Shift+F5", () => ribbon.StopRuntimeCommand.Execute(null)));
            commands.Add(new PaletteCommand("重置运行", "", () => ribbon.ResetRuntimeCommand.Execute(null)));
            commands.Add(new PaletteCommand("切换节点断点", "F9", () => ribbon.ToggleBreakpointCommand.Execute(null)));
            commands.Add(new PaletteCommand("刷新运行状态", "", () => ribbon.RefreshRuntimeCommand.Execute(null)));
            commands.Add(new PaletteCommand("关闭当前画布", "Ctrl+W", () => ribbon.CloseActiveDocumentCommand.Execute(null)));
        }

        if (vm is not null)
        {
            commands.Add(new PaletteCommand("缩放 50%", "", () => vm.SelectZoomPreset("50%")));
            commands.Add(new PaletteCommand("缩放 100%", "Ctrl+1", () => vm.SelectZoomPreset("100%")));
            commands.Add(new PaletteCommand("缩放 150%", "", () => vm.SelectZoomPreset("150%")));
            commands.Add(new PaletteCommand("缩放 200%", "", () => vm.SelectZoomPreset("200%")));
            commands.Add(new PaletteCommand("缩放 300%", "", () => vm.SelectZoomPreset("300%")));
            commands.Add(new PaletteCommand("主题：翡翠", "", () => vm.SelectedTheme = DemoBrandThemes.Emerald));
            commands.Add(new PaletteCommand("主题：琥珀", "", () => vm.SelectedTheme = DemoBrandThemes.Amber));
            commands.Add(new PaletteCommand("主题：石墨", "", () => vm.SelectedTheme = DemoBrandThemes.Graphite));
            commands.Add(new PaletteCommand("切换小地图", "", () => vm.ToggleMinimap()));
        }

        return commands;
    }

    private void RefreshPaletteList(string filter)
    {
        if (this.FindControl<global::Avalonia.Controls.ListBox>("CommandPaletteList") is not { } list)
        {
            return;
        }

        var items = string.IsNullOrWhiteSpace(filter)
            ? _paletteCommands
            : _paletteCommands.Where(command =>
                command.Title.Contains(filter.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        list.ItemsSource = items;
        if (items.Count > 0)
        {
            list.SelectedIndex = 0;
        }
    }

    private void ExecutePaletteCommand(PaletteCommand? command)
    {
        if (command is null || DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        viewModel.IsCommandPaletteOpen = false;
        command.Run();
    }

    private void HandlePaletteQueryKeyDown(object? sender, KeyEventArgs args)
    {
        var list = this.FindControl<global::Avalonia.Controls.ListBox>("CommandPaletteList");
        var items = list?.ItemsSource as IReadOnlyList<PaletteCommand>;
        switch (args.Key)
        {
            case Key.Escape when DataContext is MainWindowViewModel viewModel:
                args.Handled = true;
                viewModel.IsCommandPaletteOpen = false;
                break;
            case Key.Enter:
                args.Handled = true;
                if (list?.SelectedItem is PaletteCommand selected)
                {
                    ExecutePaletteCommand(selected);
                }
                else if (items is { Count: > 0 })
                {
                    ExecutePaletteCommand(items[0]);
                }
                break;
            case Key.Down when list is not null && items is not null:
                args.Handled = true;
                if (list.SelectedIndex < items.Count - 1)
                {
                    list.SelectedIndex++;
                }
                break;
            case Key.Up when list is not null && list.SelectedIndex > 0:
                args.Handled = true;
                list.SelectedIndex--;
                break;
        }
    }

    private void HandlePaletteListExecute(object? sender, TappedEventArgs args)
    {
        if (sender is global::Avalonia.Controls.ListBox list && list.SelectedItem is PaletteCommand command)
        {
            ExecutePaletteCommand(command);
        }
    }

    private void HandlePaletteBackdropPressed(object? sender, PointerPressedEventArgs args)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.IsCommandPaletteOpen = false;
        }

        args.Handled = true;
    }

    /// <summary>首启欢迎卡：仅未展示过时出现，任意交互即收起并记忆。</summary>
    private void ShowWelcomeCardIfFirstRun()
    {
        if (!_settingsStore.LoadWelcomed())
        {
            WelcomeCardHost.IsVisible = true;
        }
    }

    private void DismissWelcomeCard()
    {
        WelcomeCardHost.IsVisible = false;
        _settingsStore.TrySaveWelcomed(true);
    }

    private void HandleWelcomeBackdropPressed(object? sender, PointerPressedEventArgs args)
    {
        DismissWelcomeCard();
        args.Handled = true;
    }

    private void HandleWelcomeStart(object? sender, RoutedEventArgs args)
    {
        DismissWelcomeCard();
    }

    /// <summary>打开示例画布（默认订单风控样例）。</summary>
    private void HandleWelcomeOpenSample(object? sender, RoutedEventArgs args)
    {
        DismissWelcomeCard();
        var ribbon = this.FindControl<MainRibbonView>(MainWindowControlNames.Ribbon)?.DataContext as MainRibbonViewModel;
        var sample = ribbon?.SampleTasks.FirstOrDefault(item => item.Id == "order-risk-control") ??
                     ribbon?.SampleTasks.FirstOrDefault();
        sample?.Command.Execute(sample);
    }
}
