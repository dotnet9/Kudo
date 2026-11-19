// Property panel facade: resolves the current target and dispatches to built-in or extension property editors.
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Kudo.Avalonia;
using Kudo.Core.Documents;
using CodeWF.Toolkit.Lang;
using System.Windows.Input;
using static Kudo.Avalonia.Designer.GraphPropertyEditorControls;
using PropertiesLanguage = global::Localization.Kudo_Avalonia.Designer.GraphPropertiesView;

namespace Kudo.Avalonia.Designer;

public sealed record GraphNodeBackgroundImageRequest(
    GraphPropertyEditorContext EditorContext,
    string NodeId);

public sealed class GraphPropertyEditorContext
{
    private readonly Func<IGraphCommand, bool> _executeCommand;
    private readonly Func<IGraphCommand, bool> _previewCommand;
    private readonly Func<IGraphCommand, bool> _commitPreviewCommand;
    private readonly Action<string> _setMessage;
    private readonly Func<string, GraphNodeDefinition?>? _resolveNodeDefinition;

    public GraphPropertyEditorContext(
        GraphDocumentSession session,
        IEnumerable<string>? selectedNodeIds = null,
        string? primaryNodeId = null,
        string? selectedEdgeId = null,
        IReadOnlyCollection<string>? breakpointNodeIds = null,
        Action<IGraphCommand>? executeCommand = null,
        Action<string>? setMessage = null,
        bool isReadOnly = false,
        Func<IGraphCommand, bool>? tryExecuteCommand = null,
        string? selectedAnnotationId = null,
        IEnumerable<string>? selectedGroupIds = null,
        Func<string, GraphNodeDefinition?>? resolveNodeDefinition = null,
        Func<IGraphCommand, bool>? tryPreviewCommand = null,
        Func<IGraphCommand, bool>? tryCommitPreviewCommand = null)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));
        SelectedNodeIds = (selectedNodeIds ?? []).ToArray();
        PrimaryNodeId = primaryNodeId;
        SelectedEdgeId = selectedEdgeId;
        SelectedAnnotationId = selectedAnnotationId;
        SelectedGroupIds = (selectedGroupIds ?? []).ToArray();
        BreakpointNodeIds = breakpointNodeIds ?? Array.Empty<string>();
        IsReadOnly = isReadOnly;
        _resolveNodeDefinition = resolveNodeDefinition;
        _executeCommand = tryExecuteCommand ?? (command =>
        {
            if (executeCommand is not null)
            {
                executeCommand(command);
            }
            else
            {
                session.ExecuteCommand(command);
            }

            return true;
        });
        _previewCommand = tryPreviewCommand ?? (command =>
        {
            command.Execute(session.Document);
            return true;
        });
        _commitPreviewCommand = tryCommitPreviewCommand ?? (command =>
        {
            session.RecordExecutedCommand(command);
            return true;
        });
        _setMessage = setMessage ?? (_ => { });
    }

    public GraphDocumentSession Session { get; }

    public IReadOnlyList<string> SelectedNodeIds { get; }

    public string? PrimaryNodeId { get; }

    public string? SelectedEdgeId { get; }

    public string? SelectedAnnotationId { get; }

    public IReadOnlyList<string> SelectedGroupIds { get; }

    public IReadOnlyCollection<string> BreakpointNodeIds { get; }

    public GraphCanvasSettings CanvasSettings => Session.Document.CanvasSettings;

    public bool IsReadOnly { get; }

    public GraphPropertyContext PropertyContext => GraphPropertyContextResolver.Resolve(
        Session.Document,
        SelectedNodeIds,
        PrimaryNodeId,
        SelectedEdgeId,
        SelectedAnnotationId,
        SelectedGroupIds);

    public bool Execute(IGraphCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (IsReadOnly)
        {
            return false;
        }

        return _executeCommand(command);
    }

    public bool TryPreview(IGraphCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return !IsReadOnly && _previewCommand(command);
    }

    public bool TryCommitPreview(IGraphCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return !IsReadOnly && _commitPreviewCommand(command);
    }

    public void SetMessage(string message) => _setMessage(message);

    public GraphNodeDefinition? ResolveNodeDefinition(NodeModel node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return _resolveNodeDefinition?.Invoke(node.Type);
    }
}

public sealed class GraphPropertiesView : GraphDesignerPanel
{
    public static readonly StyledProperty<GraphPropertyEditorContext?> EditorContextProperty =
        AvaloniaProperty.Register<GraphPropertiesView, GraphPropertyEditorContext?>(
            nameof(EditorContext));

    public static readonly StyledProperty<ICommand?> EditCommittedCommandProperty =
        AvaloniaProperty.Register<GraphPropertiesView, ICommand?>(nameof(EditCommittedCommand));

    public static readonly StyledProperty<bool> ShowNodeBackgroundStretchEditorProperty =
        AvaloniaProperty.Register<GraphPropertiesView, bool>(
            nameof(ShowNodeBackgroundStretchEditor),
            defaultValue: true);

    public static readonly StyledProperty<bool> ShowEdgeMarkerEditorsProperty =
        AvaloniaProperty.Register<GraphPropertiesView, bool>(
            nameof(ShowEdgeMarkerEditors),
            defaultValue: true);

    private string _contextTitle = GraphLocalization.Get(PropertiesLanguage.Properties);
    private readonly Border _root;
    private readonly Kudo.Controls.PropertyGrid.PropertyGrid _propertyGrid = new();
    private IDisposable? _customContentLifetime;
    private GraphPropertyPanelItems? _panelItems;

    public GraphPropertiesView()
    {
        PropertyEditorProviders.Changed += (_, _) => RefreshCore(EditorContext);
        _propertyGrid.ValuePreviewing += HandlePropertyPreviewing;
        _propertyGrid.ValueCommitted += HandlePropertyCommitted;
        _propertyGrid.EditorFactory = GetCustomPropertyEditor;
        _propertyGrid.BackgroundBrush = PanelBrush;
        _root = new Border
        {
            Background = PanelBrush,
            Child = new ScrollViewer
            {
                Content = _propertyGrid
            }
        };
        _root.Classes.Add("graph-properties-root");
        Content = _root;
        AttachedToVisualTree += HandleAttachedToVisualTree;
        DetachedFromVisualTree += HandleDetachedFromVisualTree;
        RefreshCore(null);
    }

    public event Action<string>? EditCommitted;

    public event Action<GraphNodeBackgroundImageRequest>? BackgroundImageRequested;

    public ICommand? EditCommittedCommand
    {
        get => GetValue(EditCommittedCommandProperty);
        set => SetValue(EditCommittedCommandProperty, value);
    }

    public bool ShowNodeBackgroundStretchEditor
    {
        get => GetValue(ShowNodeBackgroundStretchEditorProperty);
        set => SetValue(ShowNodeBackgroundStretchEditorProperty, value);
    }

    public bool ShowEdgeMarkerEditors
    {
        get => GetValue(ShowEdgeMarkerEditorsProperty);
        set => SetValue(ShowEdgeMarkerEditorsProperty, value);
    }

    public string ContextTitle => _contextTitle;

    public GraphPropertyContext Context { get; private set; } = GraphPropertyContext.Empty;

    public GraphPropertyEditorProviderRegistry PropertyEditorProviders { get; } = new();

    public GraphPropertyEditorContext? EditorContext
    {
        get => GetValue(EditorContextProperty);
        set => SetValue(EditorContextProperty, value);
    }

    public void Refresh(GraphPropertyEditorContext? editorContext)
    {
        if (!ReferenceEquals(EditorContext, editorContext))
        {
            SetCurrentValue(EditorContextProperty, editorContext);
            return;
        }

        RefreshCore(editorContext);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == EditorContextProperty)
        {
            RefreshCore(change.GetNewValue<GraphPropertyEditorContext?>());
        }
        else if (change.Property == ShowNodeBackgroundStretchEditorProperty ||
                 change.Property == ShowEdgeMarkerEditorsProperty)
        {
            RefreshCore(EditorContext);
        }
    }

    /// <summary>
    /// Resolves the target context and maps the fields into PropertyGrid property items
    /// (commit semantics carried by GraphPropertyPanelItems, identical to the legacy built-in editors).
    /// </summary>
    private void RefreshCore(GraphPropertyEditorContext? editorContext)
    {
        ClearContent();
        if (editorContext is null)
        {
            Context = GraphPropertyContext.Empty;
            SetContext(L(PropertiesLanguage.Properties));
            _panelItems = CreatePanelItems(editorContext);
            _propertyGrid.Items = _panelItems.BuildForEmpty().ToArray();
            return;
        }

        Context = editorContext.PropertyContext;
        _panelItems = CreatePanelItems(editorContext);
        var items = new List<Kudo.Controls.PropertyGrid.PropertyItem>();

        if (Context.Kind == GraphPropertyTargetKind.GroupSelection)
        {
            SetContext(L(PropertiesLanguage.MultiGroupProperties));
            items.AddRange(_panelItems.BuildForGroupSelection(Context.Groups));
        }
        else if (Context.Group is { } group)
        {
            SetContext(L(PropertiesLanguage.GroupProperties));
            items.AddRange(_panelItems.BuildForGroup(group));
        }
        else if (Context.Kind == GraphPropertyTargetKind.NodeSelection)
        {
            SetContext(L(PropertiesLanguage.MultiSelectionProperties));
            items.AddRange(_panelItems.BuildForNodeSelection(Context.Nodes));
        }
        else if (Context.Node is { } node)
        {
            var hasCustomContent = PropertyEditorProviders.TryCreate(
                editorContext,
                Context,
                out var customContent);
            SetContext(hasCustomContent
                ? customContent!.Title
                : L(PropertiesLanguage.ComponentProperties));
            items.AddRange(_panelItems.BuildForNode(
                node,
                editorContext.ResolveNodeDefinition(node),
                hasCustomContent ? customContent!.Title : null,
                hasCustomContent ? customContent!.Controls : null));
            if (hasCustomContent)
            {
                _customContentLifetime = customContent!.Lifetime;
            }
        }
        else if (PropertyEditorProviders.TryCreate(editorContext, Context, out var replacementContent))
        {
            _customContentLifetime = replacementContent!.Lifetime;
            SetContext(replacementContent.Title);
            items.AddRange(_panelItems.BuildForProviderOnly(
                replacementContent.Title, replacementContent.Controls));
        }
        else if (Context.Edge is { } edge)
        {
            SetContext(L(PropertiesLanguage.EdgeProperties));
            items.AddRange(_panelItems.BuildForEdge(edge, FormatEdgeState(edge.State)));
        }
        else if (Context.Annotation is { } annotation)
        {
            SetContext(L(PropertiesLanguage.AnnotationProperties));
            items.AddRange(_panelItems.BuildForAnnotation(annotation));
        }
        else
        {
            SetContext(L(PropertiesLanguage.TaskProperties));
            items.AddRange(_panelItems.BuildForDocument());
        }

        _propertyGrid.Items = items;
    }

    private GraphPropertyPanelItems CreatePanelItems(GraphPropertyEditorContext? editorContext)
    {
        return new GraphPropertyPanelItems(
            editorContext!,
            Context,
            ShowNodeBackgroundStretchEditor,
            ShowEdgeMarkerEditors,
            NotifyEditCommitted,
            NotifyBackgroundImageRequested,
            () => RefreshCore(EditorContext));
    }

    private Control? GetCustomPropertyEditor(Kudo.Controls.PropertyGrid.PropertyItem item) =>
        _panelItems?.GetCustomEditor(item);

    private void HandlePropertyPreviewing(object? sender, Kudo.Controls.PropertyGrid.PropertyValuePreviewEventArgs e)
    {
        if (_panelItems is not null && _panelItems.HandlePreview(e.Item, e.NewValue))
        {
            e.Cancel = true;
        }
    }

    private void HandlePropertyCommitted(object? sender, Kudo.Controls.PropertyGrid.PropertyValueChangedEventArgs e)
    {
        _panelItems?.HandleCommit(e.Item, e.OldValue, e.NewValue);
    }

    private static string FormatEdgeState(EdgeState state) => state switch
    {
        EdgeState.Active => L(PropertiesLanguage.EdgeActive),
        EdgeState.Highlighted => L(PropertiesLanguage.EdgeHighlighted),
        EdgeState.Invalid => L(PropertiesLanguage.EdgeInvalid),
        EdgeState.Disabled => L(PropertiesLanguage.EdgeDisabled),
        _ => L(PropertiesLanguage.EdgeNormal)
    };

    protected override void OnPaletteChanged()
    {
        if (_root is not null)
        {
            _root.Background = PanelBrush;
        }

        _propertyGrid.BackgroundBrush = PanelBrush;
        RefreshCore(EditorContext);
    }

    private void NotifyEditCommitted(string message)
    {
        EditCommitted?.Invoke(message);
        if (EditCommittedCommand?.CanExecute(message) == true)
        {
            EditCommittedCommand.Execute(message);
        }
    }

    private void NotifyBackgroundImageRequested(GraphNodeBackgroundImageRequest request) =>
        BackgroundImageRequested?.Invoke(request);

    private void SetContext(string title) => _contextTitle = title;

    private void HandleCultureChanged(object? sender, EventArgs e) => RefreshCore(EditorContext);

    private void HandleAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        I18nManager.Instance.CultureChanged += HandleCultureChanged;
        RefreshCore(EditorContext);
    }

    private void HandleDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        I18nManager.Instance.CultureChanged -= HandleCultureChanged;
        ClearContent();
    }

    private void ClearContent()
    {
        _propertyGrid.Items = null;
        Interlocked.Exchange(ref _customContentLifetime, null)?.Dispose();
    }

    private static string L(string key) => GraphLocalization.Get(key);

    private static string F(string key, params object?[] arguments) =>
        GraphLocalization.Format(key, arguments);
}
