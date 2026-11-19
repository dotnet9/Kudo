using System.ComponentModel;
using Avalonia.Media;
using Kudo.Avalonia.Designer;
using ReactiveUI;

namespace Kudo.Demo.Desktop.ViewModels;

public sealed class EdgeStylesDemoDocumentViewModel : ReactiveObject, IDisposable
{
    private GraphEdgeStylePreset _selectedStylePreset;
    private DemoCanvasTheme _selectedTheme = DemoCanvasTheme.Light;
    private string? _selectedEdgeId;
    private bool _isSynchronizingSelection;
    private bool _isDisposed;

    public EdgeStylesDemoDocumentViewModel(GraphDocumentView documentView)
    {
        DocumentView = documentView ?? throw new ArgumentNullException(nameof(documentView));
        StylePresets = Enum.GetValues<GraphEdgeStylePreset>();
        CanvasThemes = Enum.GetValues<DemoCanvasTheme>();
        DocumentView.Canvas.SelectionChanged += HandleCanvasChanged;
        DocumentView.Canvas.GraphChanged += HandleCanvasChanged;
        ApplyCanvasTheme(_selectedTheme);
        DocumentView.Canvas.SelectedEdgeId = DocumentView.Canvas.Document?.Edges.FirstOrDefault()?.Id;
        SynchronizeSelection();
    }

    public GraphDocumentView DocumentView { get; }

    public IReadOnlyList<GraphEdgeStylePreset> StylePresets { get; }

    public IReadOnlyList<DemoCanvasTheme> CanvasThemes { get; }

    public GraphEdgeStylePreset SelectedStylePreset
    {
        get => _selectedStylePreset;
        set
        {
            if (this.RaiseAndSetIfChanged(ref _selectedStylePreset, value) == value &&
                !_isSynchronizingSelection &&
                FindSelectedEdge() is { } edge)
            {
                edge.StylePreset = value;
            }
        }
    }

    public DemoCanvasTheme SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (this.RaiseAndSetIfChanged(ref _selectedTheme, value) == value)
            {
                ApplyCanvasTheme(value);
            }
        }
    }

    public string? SelectedEdgeId
    {
        get => _selectedEdgeId;
        private set => this.RaiseAndSetIfChanged(ref _selectedEdgeId, value);
    }

    public bool HasSelectedEdge => SelectedEdgeId is not null;

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        DocumentView.Canvas.SelectionChanged -= HandleCanvasChanged;
        DocumentView.Canvas.GraphChanged -= HandleCanvasChanged;
    }

    private void HandleCanvasChanged(object? sender, EventArgs e) => SynchronizeSelection();

    private void SynchronizeSelection()
    {
        if (_isDisposed)
        {
            return;
        }

        _isSynchronizingSelection = true;
        try
        {
            var edge = FindSelectedEdge();
            SelectedEdgeId = edge?.Id;
            SelectedStylePreset = edge?.StylePreset ?? GraphEdgeStylePreset.Default;
            this.RaisePropertyChanged(nameof(HasSelectedEdge));
        }
        finally
        {
            _isSynchronizingSelection = false;
        }
    }

    private EdgeModel? FindSelectedEdge()
    {
        var selectedEdgeId = DocumentView.Canvas.SelectedEdgeId;
        return string.IsNullOrWhiteSpace(selectedEdgeId)
            ? null
            : DocumentView.Canvas.Document?.Edges.FirstOrDefault(edge => edge.Id == selectedEdgeId);
    }

    private void ApplyCanvasTheme(DemoCanvasTheme theme)
    {
        var palette = theme switch
        {
            DemoCanvasTheme.Dark => new DemoCanvasPalette(
                "#111827", "#1F2937", "#64748B", "#F8FAFC", "#CBD5E1", "#1E293B", "#334155"),
            DemoCanvasTheme.Blueprint => new DemoCanvasPalette(
                "#0B2A4A", "#123B63", "#5EA8E5", "#F0F9FF", "#BAE6FD", "#164E7A", "#2673A8"),
            _ => new DemoCanvasPalette(
                "#F9FBFE", "#FFFFFF", "#BFCFE1", "#1F2A37", "#708196", "#E8EEF6", "#CFDBE8")
        };
        var canvas = DocumentView.Canvas;
        canvas.CanvasBackgroundBrush = CreateBrush(palette.CanvasBackground);
        canvas.NodeFillBrush = CreateBrush(palette.NodeFill);
        canvas.NodeStrokeBrush = CreateBrush(palette.NodeStroke);
        canvas.TitleBrush = CreateBrush(palette.Title);
        canvas.PortTextBrush = CreateBrush(palette.PortText);
        canvas.GridMinorBrush = CreateBrush(palette.GridMinor);
        canvas.GridMajorBrush = CreateBrush(palette.GridMajor);
    }

    private static IBrush CreateBrush(string color) => new SolidColorBrush(Color.Parse(color));

    private sealed record DemoCanvasPalette(
        string CanvasBackground,
        string NodeFill,
        string NodeStroke,
        string Title,
        string PortText,
        string GridMinor,
        string GridMajor);
}

public enum DemoCanvasTheme
{
    [Description("浅色主题")]
    Light,
    [Description("深色主题")]
    Dark,
    [Description("蓝图主题")]
    Blueprint
}
