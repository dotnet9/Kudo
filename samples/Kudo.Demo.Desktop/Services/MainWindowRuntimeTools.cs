using Avalonia.Threading;
using Kudo;
using Kudo.Avalonia.Designer;
using Kudo.Core.Runtime;

namespace Kudo.Demo.Desktop.Services;

internal sealed class MainWindowRuntimeTools : IDisposable
{
    private readonly GraphVariablesView _variables;
    private readonly GraphBreakpointsView _breakpoints;
    private readonly GraphCallStackView _callStack;
    private readonly GraphRuntimeTraceView _trace;
    private readonly Func<GraphDocumentView?> _activeViewProvider;
    private IGraphRuntimeSession? _session;
    private bool _isDisposed;

    public MainWindowRuntimeTools(
        GraphVariablesView variables,
        GraphBreakpointsView breakpoints,
        GraphCallStackView callStack,
        GraphRuntimeTraceView trace,
        Func<GraphDocumentView?> activeViewProvider)
    {
        _variables = variables ?? throw new ArgumentNullException(nameof(variables));
        _breakpoints = breakpoints ?? throw new ArgumentNullException(nameof(breakpoints));
        _callStack = callStack ?? throw new ArgumentNullException(nameof(callStack));
        _trace = trace ?? throw new ArgumentNullException(nameof(trace));
        _activeViewProvider = activeViewProvider ??
            throw new ArgumentNullException(nameof(activeViewProvider));
        _variables.VariableRequested += LocateVariable;
        _callStack.FrameRequested += LocateFrame;
        _trace.DiagnosticRequested += LocateDiagnostic;
        _trace.VisibleDiagnosticsRequested += LocateDiagnostics;
    }

    public void Refresh()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        var view = _activeViewProvider();
        var session = view?.RuntimeSession;
        if (!ReferenceEquals(_session, session))
        {
            DetachSession();
            _session = session;
            AttachSession();
        }

        _variables.Refresh(session);
        _breakpoints.Refresh(CreateBreakpointContext(view, session));
        _callStack.Refresh(session);
        _trace.Refresh(session);
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        DetachSession();
        _variables.VariableRequested -= LocateVariable;
        _callStack.FrameRequested -= LocateFrame;
        _trace.DiagnosticRequested -= LocateDiagnostic;
        _trace.VisibleDiagnosticsRequested -= LocateDiagnostics;
        _variables.Refresh(null);
        _breakpoints.Refresh(null);
        _callStack.Refresh(null);
        _trace.Refresh(null);
    }

    private GraphBreakpointEditorContext? CreateBreakpointContext(
        GraphDocumentView? view,
        IGraphRuntimeSession? session) =>
        view is null || session is null
            ? null
            : new GraphBreakpointEditorContext(
                session,
                LocateNode,
                view.SetMessage,
                LocateNodes);

    private void AttachSession()
    {
        if (_session is null)
        {
            return;
        }

        _session.SnapshotChanged += HandleSnapshotChanged;
        _session.BreakpointsChanged += HandleRuntimeChanged;
        _session.WatchesChanged += HandleRuntimeChanged;
        _session.DiagnosticsChanged += HandleRuntimeChanged;
    }

    private void DetachSession()
    {
        if (_session is null)
        {
            return;
        }

        _session.SnapshotChanged -= HandleSnapshotChanged;
        _session.BreakpointsChanged -= HandleRuntimeChanged;
        _session.WatchesChanged -= HandleRuntimeChanged;
        _session.DiagnosticsChanged -= HandleRuntimeChanged;
        _session = null;
    }

    private void HandleRuntimeChanged(object? sender, EventArgs e)
        => ScheduleRefresh();

    private void HandleSnapshotChanged(object? sender, GraphRuntimeSnapshot snapshot)
        => ScheduleRefresh();

    private void ScheduleRefresh()
    {
        if (_isDisposed)
        {
            return;
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            Refresh();
        }
        else
        {
            Dispatcher.UIThread.Post(Refresh);
        }
    }

    private void LocateVariable(GraphRuntimeVariable variable)
    {
        if (!string.IsNullOrWhiteSpace(variable.NodeId))
        {
            LocateNode(variable.NodeId);
        }
    }

    private void LocateFrame(GraphRuntimeStackFrame frame) => LocateNode(frame.NodeId);

    private void LocateDiagnostic(GraphRuntimeDiagnostic diagnostic)
    {
        if (!string.IsNullOrWhiteSpace(diagnostic.NodeId))
        {
            LocateNode(diagnostic.NodeId);
        }
    }

    private void LocateDiagnostics(IReadOnlyList<GraphRuntimeDiagnostic> diagnostics) =>
        LocateNodes(diagnostics
            .Select(diagnostic => diagnostic.NodeId)
            .Where(nodeId => !string.IsNullOrWhiteSpace(nodeId))
            .Select(nodeId => nodeId!)
            .Distinct(StringComparer.Ordinal)
            .ToArray());

    private void LocateNode(string nodeId)
    {
        var view = _activeViewProvider();
        var node = view?.Session.Document.FindNode(nodeId);
        if (view is null || node is null)
        {
            return;
        }

        view.EditorContext.Selection.SetNodes([nodeId], nodeId);
        view.EditorContext.Viewport.Center(GraphNodeGeometry.GetCenter(node));
    }

    private void LocateNodes(IReadOnlyList<string> nodeIds)
    {
        var view = _activeViewProvider();
        if (view is null)
        {
            return;
        }

        var existingIds = nodeIds
            .Where(nodeId => view.Session.Document.FindNode(nodeId) is not null)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (existingIds.Length == 0)
        {
            return;
        }

        view.EditorContext.Selection.SetNodes(existingIds, existingIds[0]);
        view.EditorContext.Viewport.FitSelection();
    }
}
