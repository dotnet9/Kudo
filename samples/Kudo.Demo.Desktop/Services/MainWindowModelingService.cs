using Avalonia;
using Kudo.Avalonia;
using Kudo.Avalonia.Designer;
using Kudo.Core.Documents;
using MainWindowLanguage = global::Localization.Kudo_Demo_Desktop.Views.MainWindow;

namespace Kudo.Demo.Desktop.Services;

internal sealed class MainWindowModelingService
{
    private readonly GraphDocumentManager _documents;
    private readonly Func<IGraphNodeCatalog> _nodeCatalogProvider;
    private readonly Func<GraphDocumentView?> _activeViewProvider;
    private readonly Func<GraphDocumentView?, string, bool> _ensureEditable;
    private readonly Action<string, string?> _appendLog;

    public MainWindowModelingService(
        GraphDocumentManager documents,
        Func<IGraphNodeCatalog> nodeCatalogProvider,
        Func<GraphDocumentView?> activeViewProvider,
        Func<GraphDocumentView?, string, bool> ensureEditable,
        Action<string, string?> appendLog)
    {
        _documents = documents ?? throw new ArgumentNullException(nameof(documents));
        _nodeCatalogProvider = nodeCatalogProvider ??
            throw new ArgumentNullException(nameof(nodeCatalogProvider));
        _activeViewProvider = activeViewProvider ??
            throw new ArgumentNullException(nameof(activeViewProvider));
        _ensureEditable = ensureEditable ??
            throw new ArgumentNullException(nameof(ensureEditable));
        _appendLog = appendLog ??
            throw new ArgumentNullException(nameof(appendLog));
    }

    public void Execute(MainWindowModelingAction action)
    {
        switch (action)
        {
            case MainWindowModelingAction.AddValveNode:
                AddNode(DemoModelNodeTypes.Valve);
                break;
            case MainWindowModelingAction.AddPumpNode:
                AddNode(DemoModelNodeTypes.Pump);
                break;
            case MainWindowModelingAction.AddInstrumentNode:
                AddNode(DemoModelNodeTypes.Instrument);
                break;
            case MainWindowModelingAction.AddAnnotation:
                AddAnnotation();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(action), action, null);
        }
    }

    public void AddNode(GraphNodeDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var view = _activeViewProvider();
        if (view is null)
        {
            _appendLog(
                L(MainWindowLanguage.CreateOrOpenCanvas),
                L(MainWindowLanguage.NoActiveCanvas));
            return;
        }

        if (!_ensureEditable(view, L(MainWindowLanguage.AddNodeAction)))
        {
            return;
        }

        AddNode(view, definition, null);
    }

    public void AddNode(GraphDocumentView view, GraphNodeDropRequest request)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(request);
        if (!_ensureEditable(view, L(MainWindowLanguage.DropNodeAction)))
        {
            return;
        }

        var definition = _nodeCatalogProvider().FindByNodeType(request.NodeType);
        if (definition is null)
        {
            _appendLog(
                F(MainWindowLanguage.CatalogNodeTypeMissing, request.NodeType),
                L(MainWindowLanguage.ModelDropFailed));
            return;
        }

        AddNode(view, definition, request.Position);
    }

    private void AddNode(string nodeType)
    {
        var definition = _nodeCatalogProvider().FindByNodeType(nodeType) ??
            throw new ArgumentOutOfRangeException(
                nameof(nodeType),
                nodeType,
                L(MainWindowLanguage.UnknownNodeType));
        AddNode(definition);
    }

    private void AddNode(
        GraphDocumentView view,
        GraphNodeDefinition definition,
        GraphPoint? requestedPosition)
    {
        if (!_documents.Sessions.Contains(view.Session))
        {
            _appendLog(
                L(MainWindowLanguage.TargetCanvasClosed),
                L(MainWindowLanguage.ModelDropFailed));
            return;
        }

        var document = view.Session.Document;
        var sequence = document.Nodes.Count + 1;
        while (document.FindNode($"{definition.NodeType}-{sequence}") is not null)
        {
            sequence++;
        }

        var position = requestedPosition ??
            new GraphPoint(100 + sequence * 26, 80 + sequence * 20);
        if (!view.TryCreateNodeFromDefinition(
                definition,
                position,
                operationContext: requestedPosition is not null
                    ? GraphOperationContext.User(GraphOperationInteractions.DragDrop)
                    : GraphOperationContext.User()))
        {
            return;
        }

        var nodeId = view.Canvas.SelectedNodeId ?? $"{definition.NodeType}-{sequence}";
        var node = document.FindNode(nodeId);
        view.SetMessage(F(MainWindowLanguage.NodeAddedStatus, node?.Name ?? definition.Name));
    }

    private void AddAnnotation()
    {
        var view = _activeViewProvider();
        if (view is null ||
            !_ensureEditable(view, L(MainWindowLanguage.AddAnnotationAction)))
        {
            return;
        }

        var document = view.Session.Document;
        var sequence = document.Annotations.Count + 1;
        while (document.FindAnnotation($"annotation-{sequence}") is not null)
        {
            sequence++;
        }

        var center = view.ScreenToGraph(new Point(
            view.Canvas.Bounds.Width / 2,
            view.Canvas.Bounds.Height / 2));
        var annotation = new AnnotationModel
        {
            Id = $"annotation-{sequence}",
            Text = L(MainWindowLanguage.AnnotationDefaultText),
            Position = new GraphPoint(center.X - 110, center.Y - 60)
        };
        if (!view.TryExecuteCommand(new AddAnnotationCommand(
                annotation,
                L(MainWindowLanguage.AddAnnotationAction))))
        {
            return;
        }

        view.Canvas.SetSelectedAnnotation(annotation.Id);
        view.Canvas.BeginAnnotationTextEdit(annotation.Id);
        view.SetMessage(L(MainWindowLanguage.AnnotationAdded));
    }

    private static string L(string key) => GraphLocalization.Get(key);

    private static string F(string key, params object?[] arguments) =>
        GraphLocalization.Format(key, arguments);
}
