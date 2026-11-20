using Avalonia.Threading;
using Kudo;
using Kudo.Avalonia;
using Kudo.Demo.Desktop.Localization;
using Kudo.Demo.Desktop.Services;
using Kudo.Avalonia.Designer;
using Kudo.Core.Documents;
using CodeWF.Toolkit.Lang;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using MainWindowLanguage = global::Localization.Kudo_Demo_Desktop.Views.MainWindow;

namespace Kudo.Demo.Desktop.ViewModels;

public sealed class MainWindowViewModel : ReactiveObject, IDisposable
{
    private static readonly int[] ZoomPresetValues =
        [10, 25, 50, 75, 100, 125, 150, 200, 250, 300, 400, 500];

    private readonly DemoLocalizationService _localizationService;
    private readonly DemoThemeService _themeService;
    private IMainWindowActions? _actions;
    private string _activeDocumentTitle;
    private string _statusText;
    private string _documentStatusText = string.Empty;
    private string _selectedComponentsText;
    private string _zoomDisplayText = "100%";
    private string _zoomRangeText = "10%–500%";
    private DemoLanguageOption? _selectedLanguage;
    private DemoBrandTheme? _selectedTheme;
    private double _zoomSliderPosition;
    private double _minimumZoom = GraphViewportOptions.Default.MinimumZoom;
    private double _maximumZoom = GraphViewportOptions.Default.MaximumZoom;
    private bool _isWorkspaceAvailable = true;
    private bool _isDraftRestoreCompleted;
    private int _restoredDraftCount;
    private bool _isUpdatingLanguage;
    private bool _isUpdatingTheme;
    private bool _isUpdatingZoom;
    private bool _isDisposed;

    public MainWindowViewModel() : this(new DemoLocalizationService(
        DemoApplicationSettingsStore.CreateDefault(),
        CultureInfo.CurrentUICulture,
        Environment.GetEnvironmentVariable(DemoLocalizationService.CultureOverrideEnvironmentVariable)),
        new DemoThemeService(DemoApplicationSettingsStore.CreateDefault()))
    {
    }

    public MainWindowViewModel(DemoLocalizationService localizationService, DemoThemeService themeService)
    {
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
        _activeDocumentTitle = L(MainWindowLanguage.WorkspaceTitle);
        _statusText = L(MainWindowLanguage.Ready);
        _selectedComponentsText = F(MainWindowLanguage.SelectedComponents, 0, 0);
        ZoomPresets = new ObservableCollection<ZoomPresetOption>();
        UpdateZoomPresets(GraphViewportOptions.Default);
        Languages = new ObservableCollection<DemoLanguageOption>();
        RefreshLanguageOptions();
        Themes = new ObservableCollection<DemoBrandTheme>(DemoBrandThemes.All);
        _selectedTheme = _themeService.Current;
        I18nManager.Instance.CultureChanged += HandleCultureChanged;
        AddNodeCommand = ReactiveCommand.Create<GraphNodeDefinition>(
            definition => _actions?.AddNode(definition));
        DocumentRequestedCommand = ReactiveCommand.Create<GraphDocumentSession>(
            session => _actions?.ActivateDocument(session));
        RenameRequestedCommand = ReactiveCommand.CreateFromTask<GraphDocumentSession>(
            session => _actions?.RenameDocumentAsync(session) ?? Task.CompletedTask);
        DuplicateRequestedCommand = ReactiveCommand.CreateFromTask<GraphDocumentSession>(
            session => _actions?.DuplicateDocumentAsync(session) ?? Task.CompletedTask);
        CloseRequestedCommand = ReactiveCommand.CreateFromTask<GraphDocumentSession>(
            session => _actions?.CloseDocumentAsync(session) ?? Task.CompletedTask);
    }

    public ObservableCollection<ZoomPresetOption> ZoomPresets { get; }

    public ObservableCollection<DemoLanguageOption> Languages { get; }

    public ObservableCollection<DemoBrandTheme> Themes { get; }

    /// <summary>切换品牌主题：应用引擎 + 品牌资产并持久化选择。</summary>
    public DemoBrandTheme? SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (this.RaiseAndSetIfChanged(ref _selectedTheme, value) is null ||
                _isUpdatingTheme ||
                value is null ||
                value.Key == _themeService.Current.Key)
            {
                return;
            }

            _isUpdatingTheme = true;
            try
            {
                _themeService.Apply(value);
            }
            finally
            {
                _isUpdatingTheme = false;
            }
        }
    }

    public DemoLanguageOption? SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (this.RaiseAndSetIfChanged(ref _selectedLanguage, value) is null ||
                _isUpdatingLanguage ||
                value is null)
            {
                return;
            }

            _localizationService.SetPreference(value.Preference);
        }
    }

    public ICommand AddNodeCommand { get; }

    public ICommand DocumentRequestedCommand { get; }

    public ICommand RenameRequestedCommand { get; }

    public ICommand DuplicateRequestedCommand { get; }

    public ICommand CloseRequestedCommand { get; }

    public string ActiveDocumentTitle
    {
        get => _activeDocumentTitle;
        private set => this.RaiseAndSetIfChanged(ref _activeDocumentTitle, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => this.RaiseAndSetIfChanged(ref _statusText, value);
    }

    public string DocumentStatusText
    {
        get => _documentStatusText;
        private set => this.RaiseAndSetIfChanged(ref _documentStatusText, value);
    }

    public string SelectedComponentsText
    {
        get => _selectedComponentsText;
        private set => this.RaiseAndSetIfChanged(ref _selectedComponentsText, value);
    }

    /// <summary>
    /// Gets the current zoom displayed by the status bar's quick-select button.
    /// </summary>
    public string ZoomDisplayText
    {
        get => _zoomDisplayText;
        private set => this.RaiseAndSetIfChanged(ref _zoomDisplayText, value);
    }

    public string ZoomRangeText
    {
        get => _zoomRangeText;
        private set => this.RaiseAndSetIfChanged(ref _zoomRangeText, value);
    }

    public double ZoomSliderPosition
    {
        get => _zoomSliderPosition;
        set
        {
            var normalizedPosition = Math.Clamp(value, 0, 1);
            if (Math.Abs(_zoomSliderPosition - normalizedPosition) <= double.Epsilon)
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _zoomSliderPosition, normalizedPosition);
            if (_isUpdatingZoom)
            {
                return;
            }

            _actions?.SetZoomPercentage(GetZoomAtSliderPosition(normalizedPosition) * 100);
        }
    }

    public bool IsWorkspaceAvailable
    {
        get => _isWorkspaceAvailable;
        private set => this.RaiseAndSetIfChanged(ref _isWorkspaceAvailable, value);
    }

    public bool IsDraftRestoreCompleted
    {
        get => _isDraftRestoreCompleted;
        internal set => this.RaiseAndSetIfChanged(ref _isDraftRestoreCompleted, value);
    }

    public int RestoredDraftCount
    {
        get => _restoredDraftCount;
        internal set => this.RaiseAndSetIfChanged(ref _restoredDraftCount, value);
    }

    public void ZoomOut() => _actions?.ZoomBy(1 / 1.2);

    public void ZoomIn() => _actions?.ZoomBy(1.2);

    public void SelectZoomPreset(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !ZoomPresets.Any(option => string.Equals(option.Text, value, StringComparison.Ordinal)) ||
            !TryParseZoomPresetPercentage(value, out var percentage))
        {
            return;
        }

        _actions?.SetZoomPercentage(percentage);
    }

    internal void Attach(IMainWindowActions actions)
        => _actions = actions ?? throw new ArgumentNullException(nameof(actions));

    internal void UpdateStatus(
        string activeDocumentTitle,
        string statusText,
        string documentStatusText,
        string selectedComponentsText,
        GraphViewport? viewport,
        GraphViewportOptions? viewportOptions)
    {
        ActiveDocumentTitle = activeDocumentTitle;
        StatusText = statusText;
        DocumentStatusText = documentStatusText;
        SelectedComponentsText = selectedComponentsText;
        UpdateZoom(viewport, viewportOptions);
    }

    private void UpdateZoom(
        GraphViewport? viewport,
        GraphViewportOptions? viewportOptions)
    {
        _isUpdatingZoom = true;
        try
        {
            IsWorkspaceAvailable = viewport is not null && viewportOptions is not null;
            var options = viewportOptions ?? GraphViewportOptions.Default;
            _minimumZoom = options.MinimumZoom;
            _maximumZoom = options.MaximumZoom;
            UpdateZoomPresets(options);

            var zoom = viewport?.Zoom ?? 1d;
            ZoomSliderPosition = GetSliderPosition(zoom);
            var formattedZoom = FormatZoomPercentage(zoom * 100);
            ZoomDisplayText = formattedZoom;
        }
        finally
        {
            _isUpdatingZoom = false;
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        (AddNodeCommand as IDisposable)?.Dispose();
        (DocumentRequestedCommand as IDisposable)?.Dispose();
        (RenameRequestedCommand as IDisposable)?.Dispose();
        (DuplicateRequestedCommand as IDisposable)?.Dispose();
        (CloseRequestedCommand as IDisposable)?.Dispose();
        I18nManager.Instance.CultureChanged -= HandleCultureChanged;
        _actions = null;
    }

    private void HandleCultureChanged(object? sender, EventArgs e)
    {
        if (_isDisposed)
        {
            return;
        }

        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(RefreshLanguageOptions, DispatcherPriority.Normal);
            return;
        }

        RefreshLanguageOptions();
        RefreshThemeOptions();
    }

    /// <summary>品牌名随界面语言变化，重建选项集合（保持选中项不变）。</summary>
    private void RefreshThemeOptions()
    {
        var current = _selectedTheme;
        _isUpdatingTheme = true;
        try
        {
            Themes.Clear();
            foreach (var theme in DemoBrandThemes.All)
            {
                Themes.Add(theme);
            }

            _selectedTheme = DemoBrandThemes.All.FirstOrDefault(theme => theme.Key == current?.Key);
            this.RaisePropertyChanged(nameof(SelectedTheme));
        }
        finally
        {
            _isUpdatingTheme = false;
        }
    }

    private void RefreshLanguageOptions()
    {
        if (_isDisposed)
        {
            return;
        }

        _isUpdatingLanguage = true;
        try
        {
            var options = _localizationService.CreateOptions();
            if (Languages.Count == options.Count &&
                Languages.Select(option => option.Preference)
                    .SequenceEqual(options.Select(option => option.Preference)))
            {
                for (var index = 0; index < options.Count; index++)
                {
                    Languages[index] = options[index];
                }
            }
            else
            {
                Languages.Clear();
                foreach (var option in options)
                {
                    Languages.Add(option);
                }
            }

            SelectedLanguage = Languages.First(option => option.Preference == _localizationService.Preference);
        }
        finally
        {
            _isUpdatingLanguage = false;
        }
    }

    private static string L(string key) => GraphLocalization.Get(key);

    private static string F(string key, params object?[] arguments) => GraphLocalization.Format(key, arguments);

    private void UpdateZoomPresets(GraphViewportOptions options)
    {
        var presetTexts = ZoomPresetValues
            .Select(static percentage => (double)percentage)
            .Prepend(options.MinimumZoom * 100)
            .Where(percentage =>
                percentage / 100d >= options.MinimumZoom &&
                percentage / 100d <= options.MaximumZoom)
            .Append(options.MaximumZoom * 100)
            .Distinct()
            .Order()
            .Select(FormatZoomPercentage)
            .ToArray();
        if (ZoomPresets.Select(option => option.Text).SequenceEqual(presetTexts))
        {
            ZoomRangeText = FormatZoomRange(options);
            return;
        }

        ZoomPresets.Clear();
        foreach (var preset in presetTexts)
        {
            ZoomPresets.Add(new ZoomPresetOption(
                preset,
                ReactiveCommand.Create(() => SelectZoomPreset(preset))));
        }

        ZoomRangeText = FormatZoomRange(options);
    }

    private double GetSliderPosition(double zoom)
    {
        var range = _maximumZoom - _minimumZoom;
        if (!double.IsFinite(zoom) || !double.IsFinite(range) || range <= 0)
        {
            return 0;
        }

        return Math.Clamp((zoom - _minimumZoom) / range, 0, 1);
    }

    private double GetZoomAtSliderPosition(double position) =>
        _minimumZoom + (_maximumZoom - _minimumZoom) * position;

    private static bool TryParseZoomPresetPercentage(string value, out double percentage) =>
        double.TryParse(
            value.TrimEnd('%'),
            NumberStyles.Number,
            CultureInfo.CurrentCulture,
            out percentage) &&
        double.IsFinite(percentage) &&
        percentage > 0;

    private static string FormatZoomPercentage(double percentage) =>
        $"{percentage.ToString("0.##", CultureInfo.CurrentCulture)}%";

    private static string FormatZoomRange(GraphViewportOptions options) =>
        $"{FormatZoomPercentage(options.MinimumZoom * 100)}–{FormatZoomPercentage(options.MaximumZoom * 100)}";

}

public sealed class ZoomPresetOption
{
    public ZoomPresetOption(string text, ICommand command)
    {
        Text = text;
        Command = command;
    }

    public string Text { get; }

    public ICommand Command { get; }
}
