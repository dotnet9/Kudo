using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Kudo.Avalonia;
using Kudo.Core.Runtime;
using CodeWF.Toolkit.Lang;
using CallStackLanguage = global::Localization.Kudo_Avalonia.Designer.GraphCallStackView;

namespace Kudo.Avalonia.Designer;

public sealed class GraphCallStackView : GraphDesignerPanel
{
    private readonly TextBlock _contextText;
    private readonly StackPanel _frames = new() { Spacing = 5 };
    private IReadOnlyList<GraphRuntimeStackFrame> _currentFrames = [];
    private Border? _root;
    private IGraphRuntimeSession? _session;

    public GraphCallStackView()
    {
        _contextText = new TextBlock
        {
            Text = L(CallStackLanguage.NoActiveCanvas),
            FontSize = 11,
            Foreground = SecondaryTextBrush
        };
        _root = new Border
        {
            Background = PanelBrush,
            Child = new ScrollViewer
            {
                Content = new StackPanel
                {
                    Margin = new Thickness(10),
                    Spacing = 8,
                    Children =
                    {
                        _contextText,
                        _frames
                    }
                }
            }
        };
        Content = _root;
        Refresh(null);
    }

    public IGraphRuntimeSession? Session => _session;

    public IReadOnlyList<GraphRuntimeStackFrame> CurrentFrames => _currentFrames;

    public int FrameCount => _currentFrames.Count;

    public event Action<GraphRuntimeStackFrame>? FrameRequested;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        I18nManager.Instance.CultureChanged += HandleCultureChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        I18nManager.Instance.CultureChanged -= HandleCultureChanged;
        base.OnDetachedFromVisualTree(e);
    }

    public void Refresh(IGraphRuntimeSession? session)
    {
        _session = session;
        _frames.Children.Clear();
        _currentFrames = session?.CurrentSnapshot.CallStack.ToArray() ?? [];
        var documentTitle = session?.Document.Title;
        _contextText.Text = session is null
            ? L(CallStackLanguage.NoActiveCanvas)
            : F(
                CallStackLanguage.Summary,
                string.IsNullOrWhiteSpace(documentTitle)
                    ? L(CallStackLanguage.UntitledCanvas)
                    : documentTitle,
                FrameCount);
        if (_currentFrames.Count == 0)
        {
            _frames.Children.Add(new TextBlock
            {
                Text = L(CallStackLanguage.EmptyHint),
                TextWrapping = TextWrapping.Wrap,
                Foreground = SecondaryTextBrush
            });
            return;
        }

        for (var index = 0; index < _currentFrames.Count; index++)
        {
            _frames.Children.Add(CreateFrameButton(_currentFrames[index], index));
        }
    }

    public void RequestFrame(GraphRuntimeStackFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        FrameRequested?.Invoke(frame);
    }

    protected override void OnPaletteChanged()
    {
        if (_root is not null)
        {
            _root.Background = PanelBrush;
        }

        _contextText.Foreground = SecondaryTextBrush;
        Refresh(_session);
    }

    private Control CreateFrameButton(GraphRuntimeStackFrame frame, int index)
    {
        var detail = string.IsNullOrWhiteSpace(frame.Detail)
            ? F(CallStackLanguage.NodeDetail, frame.NodeId)
            : F(CallStackLanguage.NodeDetailWithContext, frame.NodeId, frame.Detail);
        var frameDetail = new StackPanel
        {
            Spacing = 1,
            Children =
            {
                new TextBlock
                {
                    Text = frame.DisplayName,
                    Foreground = PrimaryTextBrush,
                    FontWeight = FontWeight.SemiBold
                },
                new TextBlock
                {
                    Text = detail,
                    FontSize = 11,
                    Foreground = SecondaryTextBrush
                }
            }
        };
        var content = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("32,*"),
            Children =
            {
                new TextBlock
                {
                    Text = $"#{index + 1}",
                    Foreground = AccentBrush,
                    FontWeight = FontWeight.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center
                },
                frameDetail
            }
        };
        Grid.SetColumn(frameDetail, 1);
        var button = new Button
        {
            Tag = frame,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(8, 6),
            Content = content
        };
        button.Click += (_, _) => RequestFrame(frame);
        return button;
    }

    private void HandleCultureChanged(object? sender, EventArgs e) => Refresh(_session);

    private static string L(string key) => GraphLocalization.Get(key);

    private static string F(string key, params object?[] arguments) =>
        GraphLocalization.Format(key, arguments);
}
