using Avalonia;
using Avalonia.Controls;

namespace Kudo.Avalonia.ThreeD;

public sealed class GraphThreeDView : ContentControl
{
    public static readonly StyledProperty<GraphThreeDScene> SceneProperty =
        AvaloniaProperty.Register<GraphThreeDView, GraphThreeDScene>(
            nameof(Scene),
            GraphThreeDScene.Default);

    public static readonly StyledProperty<GraphThreeDRenderMode> PreferredRenderModeProperty =
        AvaloniaProperty.Register<GraphThreeDView, GraphThreeDRenderMode>(
            nameof(PreferredRenderMode),
            GraphThreeDRenderMode.Auto);

    public static readonly StyledProperty<bool> AllowSoftwareFallbackProperty =
        AvaloniaProperty.Register<GraphThreeDView, bool>(
            nameof(AllowSoftwareFallback),
            true);

    public static readonly StyledProperty<bool> IsAnimationEnabledProperty =
        AvaloniaProperty.Register<GraphThreeDView, bool>(
            nameof(IsAnimationEnabled),
            true);

    public static readonly DirectProperty<GraphThreeDView, GraphThreeDActiveRenderer> ActiveRendererProperty =
        AvaloniaProperty.RegisterDirect<GraphThreeDView, GraphThreeDActiveRenderer>(
            nameof(ActiveRenderer),
            owner => owner.ActiveRenderer);

    public static readonly DirectProperty<GraphThreeDView, bool> IsSoftwareFallbackActiveProperty =
        AvaloniaProperty.RegisterDirect<GraphThreeDView, bool>(
            nameof(IsSoftwareFallbackActive),
            owner => owner.IsSoftwareFallbackActive);

    public static readonly DirectProperty<GraphThreeDView, bool> IsRendererInitializedProperty =
        AvaloniaProperty.RegisterDirect<GraphThreeDView, bool>(
            nameof(IsRendererInitialized),
            owner => owner.IsRendererInitialized);

    public static readonly DirectProperty<GraphThreeDView, Exception?> RendererExceptionProperty =
        AvaloniaProperty.RegisterDirect<GraphThreeDView, Exception?>(
            nameof(RendererException),
            owner => owner.RendererException);

    private GraphThreeDActiveRenderer _activeRenderer = GraphThreeDActiveRenderer.None;
    private bool _isSoftwareFallbackActive;
    private bool _isRendererInitialized;
    private Exception? _rendererException;
    private GraphThreeDOpenGlControl? _openGlControl;

    public GraphThreeDView()
    {
        ClipToBounds = true;
        RebuildRenderer();
    }

    public GraphThreeDScene Scene
    {
        get => GetValue(SceneProperty);
        set => SetValue(SceneProperty, value ?? GraphThreeDScene.Default);
    }

    public GraphThreeDRenderMode PreferredRenderMode
    {
        get => GetValue(PreferredRenderModeProperty);
        set => SetValue(PreferredRenderModeProperty, value);
    }

    public bool AllowSoftwareFallback
    {
        get => GetValue(AllowSoftwareFallbackProperty);
        set => SetValue(AllowSoftwareFallbackProperty, value);
    }

    public bool IsAnimationEnabled
    {
        get => GetValue(IsAnimationEnabledProperty);
        set => SetValue(IsAnimationEnabledProperty, value);
    }

    public GraphThreeDActiveRenderer ActiveRenderer
    {
        get => _activeRenderer;
        private set => SetAndRaise(ActiveRendererProperty, ref _activeRenderer, value);
    }

    public bool IsSoftwareFallbackActive
    {
        get => _isSoftwareFallbackActive;
        private set => SetAndRaise(IsSoftwareFallbackActiveProperty, ref _isSoftwareFallbackActive, value);
    }

    public bool IsRendererInitialized
    {
        get => _isRendererInitialized;
        private set => SetAndRaise(IsRendererInitializedProperty, ref _isRendererInitialized, value);
    }

    public Exception? RendererException
    {
        get => _rendererException;
        private set => SetAndRaise(RendererExceptionProperty, ref _rendererException, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SceneProperty)
        {
            ApplyScene();
        }
        else if (change.Property == IsAnimationEnabledProperty)
        {
            ApplyAnimationEnabled();
        }
        else if (change.Property == PreferredRenderModeProperty ||
                 change.Property == AllowSoftwareFallbackProperty)
        {
            RebuildRenderer();
        }
    }

    private void RebuildRenderer()
    {
        DetachOpenGlControl();
        RendererException = null;
        IsRendererInitialized = false;
        if (PreferredRenderMode == GraphThreeDRenderMode.Software)
        {
            UseSoftwareRenderer(isFallback: false);
            return;
        }

        _openGlControl = new GraphThreeDOpenGlControl
        {
            Scene = Scene,
            IsAnimationEnabled = IsAnimationEnabled
        };
        _openGlControl.InitializationFailed += HandleOpenGlInitializationFailed;
        _openGlControl.OpenGlInitialized += HandleOpenGlInitialized;
        Content = _openGlControl;
        ActiveRenderer = GraphThreeDActiveRenderer.OpenGl;
        IsSoftwareFallbackActive = false;
    }

    private void ApplyScene()
    {
        if (Content is GraphThreeDSoftwareControl software)
        {
            software.Scene = Scene;
        }
        else if (Content is GraphThreeDOpenGlControl openGl)
        {
            openGl.Scene = Scene;
        }
    }

    private void ApplyAnimationEnabled()
    {
        if (Content is GraphThreeDSoftwareControl software)
        {
            software.IsAnimationEnabled = IsAnimationEnabled;
        }
        else if (Content is GraphThreeDOpenGlControl openGl)
        {
            openGl.IsAnimationEnabled = IsAnimationEnabled;
        }
    }

    private void HandleOpenGlInitializationFailed(
        object? sender,
        GraphThreeDOpenGlFailureEventArgs e)
    {
        if (!ReferenceEquals(sender, _openGlControl))
        {
            return;
        }

        DetachOpenGlControl();
        RendererException = e.Exception;
        if (AllowSoftwareFallback)
        {
            UseSoftwareRenderer(isFallback: true);
        }
        else
        {
            Content = null;
            ActiveRenderer = GraphThreeDActiveRenderer.None;
            IsSoftwareFallbackActive = false;
            IsRendererInitialized = false;
        }
    }

    private void HandleOpenGlInitialized(object? sender, EventArgs e)
    {
        if (ReferenceEquals(sender, _openGlControl))
        {
            IsRendererInitialized = true;
        }
    }

    private void UseSoftwareRenderer(bool isFallback)
    {
        Content = new GraphThreeDSoftwareControl
        {
            Scene = Scene,
            IsAnimationEnabled = IsAnimationEnabled
        };
        ActiveRenderer = GraphThreeDActiveRenderer.Software;
        IsSoftwareFallbackActive = isFallback;
        IsRendererInitialized = true;
    }

    private void DetachOpenGlControl()
    {
        if (_openGlControl is null)
        {
            return;
        }

        _openGlControl.InitializationFailed -= HandleOpenGlInitializationFailed;
        _openGlControl.OpenGlInitialized -= HandleOpenGlInitialized;
        _openGlControl = null;
    }
}

