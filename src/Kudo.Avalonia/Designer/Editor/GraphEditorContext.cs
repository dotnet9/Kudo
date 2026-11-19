using Kudo.Avalonia.Export;
using Kudo.Core.Runtime;
using Kudo;

namespace Kudo.Avalonia.Designer;

/// <summary>
/// 聚合一个文档视图的编辑能力。具体动作由分类对象提供，宿主无需直接编排 Canvas、Session 与服务。
/// </summary>
public sealed class GraphEditorContext
{
    private readonly GraphDocumentView _documentView;

    public GraphEditorContext(
        GraphDocumentView documentView,
        IGraphRuntimeSession? runtimeSession = null,
        GraphExporter? exporter = null,
        GraphPrintService? printService = null)
    {
        ArgumentNullException.ThrowIfNull(documentView);
        if (runtimeSession is not null &&
            !ReferenceEquals(runtimeSession, documentView.RuntimeSession))
        {
            throw new ArgumentException(
                "Runtime session must be the session owned by the document view.",
                nameof(runtimeSession));
        }

        runtimeSession = documentView.RuntimeSession;
        _documentView = documentView;
        if (runtimeSession is not null &&
            !ReferenceEquals(runtimeSession.Document, documentView.Session.Document))
        {
            throw new ArgumentException(
                "Runtime session must target the editor document.",
                nameof(runtimeSession));
        }

        exporter ??= new GraphExporter();
        Document = new GraphDocumentOperations(documentView);
        Edit = new GraphEditOperations(documentView);
        Selection = new GraphSelectionOperations(documentView);
        Arrangement = new GraphArrangementOperations(documentView);
        Viewport = new GraphViewportOperations(documentView);
        Connections = new GraphConnectionOperations(documentView);
        Runtime = new GraphRuntimeOperations(runtimeSession);
        Output = new GraphOutputOperations(
            documentView,
            exporter,
            printService ?? new GraphPrintService(exporter));
    }

    public GraphDocumentOperations Document { get; }

    public GraphEditOperations Edit { get; }

    public GraphSelectionOperations Selection { get; }

    public GraphArrangementOperations Arrangement { get; }

    public GraphViewportOperations Viewport { get; }

    public GraphConnectionOperations Connections { get; }

    public GraphRuntimeOperations Runtime { get; }

    public GraphOutputOperations Output { get; }

    /// <summary>
    /// Gets the last graph-space pointer position recorded by the document canvas.
    /// </summary>
    public GraphPoint? LastPointerGraphPoint => _documentView.Canvas.LastPointerGraphPoint;

    public long LastPointerGraphPointVersion => _documentView.Canvas.LastPointerGraphPointVersion;
}
