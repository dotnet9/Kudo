using Avalonia.Controls;
using Kudo.Avalonia;
using Kudo.Demo.Desktop.Views;
using Kudo.Avalonia.Designer;
using Kudo.Avalonia.ThreeD;
using ReactiveUI;

namespace Kudo.Demo.Desktop.ViewModels;

internal sealed class ThreeDDemoDocumentViewModel : ReactiveObject, IDisposable
{
    private GraphThreeDScene _scene = GraphThreeDScene.Default;
    private bool _isAnimationEnabled = true;
    private bool _isDisposed;

    public ThreeDDemoDocumentViewModel(GraphDocumentView documentView)
    {
        DocumentView = documentView ?? throw new ArgumentNullException(nameof(documentView));
        var options = GraphThreeDRenderOptions.FromCurrentProcess();
        PreferredRenderMode = options.RenderMode;
        AllowSoftwareFallback = options.AllowSoftwareFallback;
        DocumentView.Canvas.NodeControls.Register("transform", CreateEmbeddedThreeDControl);
        DocumentView.Canvas.SelectionChanged += HandleCanvasChanged;
        DocumentView.Canvas.GraphChanged += HandleCanvasChanged;
        RefreshScene();
    }

    public GraphDocumentView DocumentView { get; }

    public GraphThreeDRenderMode PreferredRenderMode { get; }

    public bool AllowSoftwareFallback { get; }

    public GraphThreeDScene Scene
    {
        get => _scene;
        private set => this.RaiseAndSetIfChanged(ref _scene, value);
    }

    public bool IsAnimationEnabled
    {
        get => _isAnimationEnabled;
        private set => this.RaiseAndSetIfChanged(ref _isAnimationEnabled, value);
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        DocumentView.Canvas.SelectionChanged -= HandleCanvasChanged;
        DocumentView.Canvas.GraphChanged -= HandleCanvasChanged;
        DocumentView.Canvas.NodeControls.Unregister("transform");
        IsAnimationEnabled = false;
    }

    private void HandleCanvasChanged(object? sender, EventArgs e) => RefreshScene();

    private void RefreshScene()
    {
        if (_isDisposed)
        {
            return;
        }

        Scene = GraphThreeDSceneBuilder.FromDocument(
            DocumentView.Canvas.Document,
            DocumentView.Canvas.SelectedNodeIds,
            DocumentView.Canvas.SelectedEdgeIds);
    }

    private Control CreateEmbeddedThreeDControl(GraphNodeControlContext context) =>
        new EmbeddedThreeDNodeView
        {
            DataContext = new EmbeddedThreeDNodeViewModel(
                context,
                PreferredRenderMode,
                AllowSoftwareFallback)
        };
}
