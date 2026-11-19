using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Kudo.Avalonia.Designer;
using Avalonia.Animation;

namespace Kudo.Avalonia.Controls;

public sealed class GraphLoadingView : GraphDesignerPanel
{
    public static readonly StyledProperty<bool> IsLoadingProperty =
        AvaloniaProperty.Register<GraphLoadingView, bool>(nameof(IsLoading), true);

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<GraphLoadingView, string?>(nameof(Title));

    public static readonly StyledProperty<string?> MessageProperty =
        AvaloniaProperty.Register<GraphLoadingView, string?>(nameof(Message));

    public static readonly StyledProperty<string?> ErrorMessageProperty =
        AvaloniaProperty.Register<GraphLoadingView, string?>(nameof(ErrorMessage));

    public static readonly StyledProperty<string?> RetryTextProperty =
        AvaloniaProperty.Register<GraphLoadingView, string?>(nameof(RetryText));

    public static readonly StyledProperty<ICommand?> RetryCommandProperty =
        AvaloniaProperty.Register<GraphLoadingView, ICommand?>(nameof(RetryCommand));

    private readonly Border _root;
    private readonly Border _indicator;
    private readonly TextBlock _title;
    private readonly TextBlock _message;
    private readonly Button _retryButton;

    public GraphLoadingView()
    {
        _indicator = CreateSpinner();
        _title = new TextBlock
        {
            FontSize = 14,
            FontWeight = FontWeight.SemiBold,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _message = new TextBlock
        {
            FontSize = 12,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 360,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _retryButton = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            MinWidth = 86,
            MinHeight = 28
        };
        _root = new Border
        {
            Padding = new Thickness(24),
            Child = new StackPanel
            {
                Spacing = 10,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    _indicator,
                    _title,
                    _message,
                    _retryButton
                }
            }
        };
        Content = _root;
        Refresh();
    }

    public bool IsLoading
    {
        get => GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Message
    {
        get => GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public string? ErrorMessage
    {
        get => GetValue(ErrorMessageProperty);
        set => SetValue(ErrorMessageProperty, value);
    }

    public string? RetryText
    {
        get => GetValue(RetryTextProperty);
        set => SetValue(RetryTextProperty, value);
    }

    public ICommand? RetryCommand
    {
        get => GetValue(RetryCommandProperty);
        set => SetValue(RetryCommandProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsLoadingProperty ||
            change.Property == TitleProperty ||
            change.Property == MessageProperty ||
            change.Property == ErrorMessageProperty ||
            change.Property == RetryTextProperty ||
            change.Property == RetryCommandProperty)
        {
            Refresh();
        }
    }

    protected override void OnPaletteChanged()
    {
        _root.Background = ElevatedBrush;
        _title.Foreground = PrimaryTextBrush;
        _message.Foreground = string.IsNullOrWhiteSpace(ErrorMessage)
            ? SecondaryTextBrush
            : ErrorBrush;
    }

    private void Refresh()
    {
        var hasError = !string.IsNullOrWhiteSpace(ErrorMessage);
        _indicator.IsVisible = IsLoading && !hasError;
        _title.Text = Title ?? string.Empty;
        _title.IsVisible = !string.IsNullOrWhiteSpace(_title.Text);
        _message.Text = hasError ? ErrorMessage : Message;
        _message.IsVisible = !string.IsNullOrWhiteSpace(_message.Text);
        _retryButton.Content = RetryText;
        _retryButton.Command = RetryCommand;
        _retryButton.IsVisible = hasError &&
            RetryCommand is not null &&
            !string.IsNullOrWhiteSpace(RetryText);
        OnPaletteChanged();
    }

    /// <summary>自研旋转指示器：环形弧线绕中心旋转，不依赖第三方控件库。</summary>
    private Border CreateSpinner()
    {
        var arc = new global::Avalonia.Controls.Shapes.Path
        {
            Width = 28,
            Height = 28,
            Stretch = Stretch.Uniform,
            Data = Geometry.Parse("M 14 3 A 11 11 0 1 1 3 14"),
            Stroke = AccentBrush,
            StrokeThickness = 2.5,
            RenderTransform = new RotateTransform(0)
        };
        var animation = new Animation
        {
            Duration = TimeSpan.FromSeconds(0.9),
            IterationCount = IterationCount.Infinite,
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0.0),
                    Setters = { new global::Avalonia.Styling.Setter(RotateTransform.AngleProperty, 0d) }
                },
                new KeyFrame
                {
                    Cue = new Cue(1.0),
                    Setters = { new global::Avalonia.Styling.Setter(RotateTransform.AngleProperty, 360d) }
                }
            }
        };
        _ = animation.RunAsync(arc);
        return new Border
        {
            Width = 28,
            Height = 28,
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = arc
        };
    }
}

public sealed class GraphLoadingHost : GraphDesignerPanel
{
    public static readonly StyledProperty<object?> LoadedContentProperty =
        AvaloniaProperty.Register<GraphLoadingHost, object?>(nameof(LoadedContent));

    public static readonly StyledProperty<bool> IsLoadingProperty =
        AvaloniaProperty.Register<GraphLoadingHost, bool>(nameof(IsLoading));

    public static readonly StyledProperty<string?> LoadingTitleProperty =
        AvaloniaProperty.Register<GraphLoadingHost, string?>(nameof(LoadingTitle));

    public static readonly StyledProperty<string?> LoadingMessageProperty =
        AvaloniaProperty.Register<GraphLoadingHost, string?>(nameof(LoadingMessage));

    public static readonly StyledProperty<string?> ErrorMessageProperty =
        AvaloniaProperty.Register<GraphLoadingHost, string?>(nameof(ErrorMessage));

    public static readonly StyledProperty<string?> RetryTextProperty =
        AvaloniaProperty.Register<GraphLoadingHost, string?>(nameof(RetryText));

    public static readonly StyledProperty<ICommand?> RetryCommandProperty =
        AvaloniaProperty.Register<GraphLoadingHost, ICommand?>(nameof(RetryCommand));

    private readonly ContentControl _contentPresenter;
    private readonly GraphLoadingView _loadingView;

    public GraphLoadingHost()
    {
        _contentPresenter = new ContentControl
        {
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch
        };
        _loadingView = new GraphLoadingView
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        Content = new Grid
        {
            Children =
            {
                _contentPresenter,
                _loadingView
            }
        };
        Refresh();
    }

    public object? LoadedContent
    {
        get => GetValue(LoadedContentProperty);
        set => SetValue(LoadedContentProperty, value);
    }

    public bool IsLoading
    {
        get => GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    public string? LoadingTitle
    {
        get => GetValue(LoadingTitleProperty);
        set => SetValue(LoadingTitleProperty, value);
    }

    public string? LoadingMessage
    {
        get => GetValue(LoadingMessageProperty);
        set => SetValue(LoadingMessageProperty, value);
    }

    public string? ErrorMessage
    {
        get => GetValue(ErrorMessageProperty);
        set => SetValue(ErrorMessageProperty, value);
    }

    public string? RetryText
    {
        get => GetValue(RetryTextProperty);
        set => SetValue(RetryTextProperty, value);
    }

    public ICommand? RetryCommand
    {
        get => GetValue(RetryCommandProperty);
        set => SetValue(RetryCommandProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LoadedContentProperty ||
            change.Property == IsLoadingProperty ||
            change.Property == LoadingTitleProperty ||
            change.Property == LoadingMessageProperty ||
            change.Property == ErrorMessageProperty ||
            change.Property == RetryTextProperty ||
            change.Property == RetryCommandProperty)
        {
            Refresh();
        }
    }

    protected override void OnPaletteChanged()
    {
        _loadingView.PanelBrush = PanelBrush;
        _loadingView.ElevatedBrush = ElevatedBrush;
        _loadingView.DividerBrush = DividerBrush;
        _loadingView.PrimaryTextBrush = PrimaryTextBrush;
        _loadingView.SecondaryTextBrush = SecondaryTextBrush;
        _loadingView.AccentBrush = AccentBrush;
        _loadingView.ErrorBrush = ErrorBrush;
    }

    private void Refresh()
    {
        var hasContent = LoadedContent is not null;
        var hasError = !string.IsNullOrWhiteSpace(ErrorMessage);
        var showLoading = IsLoading || hasError || !hasContent;
        _contentPresenter.Content = LoadedContent;
        _contentPresenter.IsVisible = hasContent;
        _contentPresenter.IsEnabled = !showLoading;
        _contentPresenter.Opacity = showLoading && hasContent ? 0.35 : 1;
        _loadingView.IsVisible = showLoading;
        _loadingView.IsLoading = IsLoading && !hasError;
        _loadingView.Title = LoadingTitle;
        _loadingView.Message = LoadingMessage;
        _loadingView.ErrorMessage = ErrorMessage;
        _loadingView.RetryText = RetryText;
        _loadingView.RetryCommand = RetryCommand;
        OnPaletteChanged();
    }
}
