using Kudo.Avalonia;
using Kudo.Avalonia.ThreeD;
using ReactiveUI;
using System.ComponentModel;

namespace Kudo.Demo.Desktop.ViewModels;

internal sealed class EmbeddedThreeDNodeViewModel : ReactiveObject, IDisposable
{
    private readonly GraphNodeControlContext _context;
    private GraphThreeDScene _scene = GraphThreeDScene.Default;
    private bool _isAnimationEnabled;
    private bool _isSubscribed;

    public EmbeddedThreeDNodeViewModel(
        GraphNodeControlContext context,
        GraphThreeDRenderMode preferredRenderMode,
        bool allowSoftwareFallback)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        PreferredRenderMode = preferredRenderMode;
        AllowSoftwareFallback = allowSoftwareFallback;
        RefreshScene();
    }

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

    public void Attach()
    {
        if (!_isSubscribed)
        {
            _context.PropertyChanged += HandleContextPropertyChanged;
            _isSubscribed = true;
        }

        RefreshScene();
    }

    public void Detach()
    {
        if (_isSubscribed)
        {
            _context.PropertyChanged -= HandleContextPropertyChanged;
            _isSubscribed = false;
        }

        IsAnimationEnabled = false;
    }

    public void Dispose() => Detach();

    private void HandleContextPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        RefreshScene();

    private void RefreshScene()
    {
        IsAnimationEnabled = _context.IsControlVisible && _isSubscribed;
        Scene = new GraphThreeDScene
        {
            AutoRotate = true,
            Nodes =
            [
                new GraphThreeDNode
                {
                    Id = _context.Node.Id,
                    Title = _context.Node.Name,
                    Type = _context.Node.Type,
                    Position = GraphThreeDPoint.Zero,
                    Size = new GraphThreeDSize(1.8, 1.05, 0.72),
                    Color = new GraphThreeDColor(35, 120, 213),
                    IsSelected = _context.IsSelected
                }
            ],
            SelectedNodeIds = _context.IsSelected ? [_context.Node.Id] : []
        }.Normalize();
    }
}
