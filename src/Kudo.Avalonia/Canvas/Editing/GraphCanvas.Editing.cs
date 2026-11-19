using Avalonia;
using Kudo;
using Kudo.Core.Algorithms;

namespace Kudo.Avalonia;

public partial class GraphCanvas
{
    internal void CompleteConnection(
        PortHitResult startPort,
        Point releasePoint,
        GraphEdgePath previewPath)
    {
        if (!TryCompleteConnection(startPort, releasePoint, manualWaypoints: null))
        {
            BeginManualConnectionDraft(startPort, releasePoint, previewPath);
        }
    }

    private bool TryCompleteConnection(
        PortHitResult startPort,
        Point releasePoint,
        IReadOnlyList<GraphPoint>? manualWaypoints)
    {
        if (Document is null)
        {
            return false;
        }

        var endPort = FindCompatibleConnectionCandidate(releasePoint, startPort) ?? HitTestPort(releasePoint);
        if (endPort is null || endPort.Reference == startPort.Reference)
        {
            return false;
        }

        var (source, target) = NormalizeConnection(startPort, endPort);
        var relationKind = ResolveRelationKind(startPort.Port);
        if (!CanPerformEdit(new GraphEditRequest(
                GraphEditOperation.ConnectPorts,
                Document,
                Source: source,
                Target: target)))
        {
            return false;
        }

        if (Document.TryConnect(
                source,
                target,
                out var edge,
                out var validation,
                relationKind,
                ConnectionValidationRule,
                ConnectionValidationTextProvider))
        {
            edge!.RoutingStyle = manualWaypoints is null
                ? NewEdgeRoutingStyle
                : EdgeRoutingStyle.Manual;
            if (manualWaypoints is not null)
            {
                edge.Waypoints = source == startPort.Reference
                    ? [.. manualWaypoints]
                    : [.. manualWaypoints.Reverse()];
            }

            if (!CommitGraphEdit(new AddEdgeCommand(
                edge,
                L(global::Localization.Kudo_Avalonia.Canvas.GraphCanvas.AddEdgeCommand))))
            {
                return false;
            }

            LastConnectionValidation = validation;
            ClearSelection();
            SelectedEdgeId = edge.Id;
            return true;
        }

        LastConnectionValidation = validation;
        return false;
    }

    internal void CompleteEdgeReconnection(
        EdgeModel edge,
        GraphEdgeEndpoint endpoint,
        Point releasePoint)
    {
        var endPort = FindCompatibleConnectionCandidate(releasePoint, edge, endpoint) ??
            HitTestPort(releasePoint);
        if (endPort is null)
        {
            return;
        }

        var source = endpoint == GraphEdgeEndpoint.Source ? endPort.Reference : edge.Source;
        var target = endpoint == GraphEdgeEndpoint.Target ? endPort.Reference : edge.Target;
        ReconnectEdge(edge.Id, source, target);
    }

    internal bool CanPerformEdit(GraphEditRequest request)
    {
        var decision = EvaluateEdit(request);
        if (decision.IsAllowed)
        {
            LastEditRejectionReason = null;
            return true;
        }

        LastEditRejectionReason = decision.Reason;
        RaiseGraphEditRejected(request, decision);
        return false;
    }

    private void ReportGraphEditRejection(GraphEditRequest request, string? reason)
    {
        LastEditRejectionReason = reason;
        RaiseGraphEditRejected(request, GraphEditDecision.Deny(reason));
    }

    private void RaiseGraphEditRejected(GraphEditRequest request, GraphEditDecision decision)
    {
        var args = new GraphEditRejectedEventArgs(request, decision);
        GraphEditRejected?.Invoke(this, args);
        ExecuteNotificationCommand(GraphEditRejectedCommand, args);
    }

    private GraphEditDecision EvaluateEdit(GraphEditRequest request) =>
        IsReadOnly && IsMutationOperation(request.Operation)
            ? GraphEditDecision.Deny(
                GraphLocalization.Get(global::Localization.Kudo_Avalonia.Canvas.GraphCanvas.ReadOnlyReason))
            : HasLockedNodeViolation(request)
                ? GraphEditDecision.Deny(
                    GraphLocalization.Get(global::Localization.Kudo_Avalonia.Canvas.GraphCanvas.LockedNodeReason))
                : EditPolicy.Evaluate(request);

    private static bool HasLockedNodeViolation(GraphEditRequest request)
    {
        if (request.Operation is GraphEditOperation.SelectNodes or GraphEditOperation.SetNodeLocked)
        {
            return false;
        }

        if (request.Operation == GraphEditOperation.EditNodeProperties &&
            request.Command is SetNodePropertiesCommand properties &&
            properties.OldPosition == properties.NewPosition &&
            Math.Abs(GraphNodeGeometry.NormalizeRotation(properties.OldRotation) -
                     GraphNodeGeometry.NormalizeRotation(properties.NewRotation)) < 0.0001)
        {
            return false;
        }

        if (request.TargetNodes.Count == 0 ||
            !request.TargetNodes.Any(node => node.IsLocked))
        {
            return false;
        }

        if (request.Operation == GraphEditOperation.EditNodeProperties &&
            request.Command is SetNodePropertiesCommand nodeProperties)
        {
            return nodeProperties.OldPosition != nodeProperties.NewPosition ||
                Math.Abs(GraphNodeGeometry.NormalizeRotation(nodeProperties.OldRotation) -
                         GraphNodeGeometry.NormalizeRotation(nodeProperties.NewRotation)) >= 0.0001;
        }

        return request.Operation is GraphEditOperation.MoveNodes or
            GraphEditOperation.ResizeNode or
            GraphEditOperation.DeleteNodes or
            GraphEditOperation.ReorderNodes;
    }

    private void ValidateEditRequest(GraphEditRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!ReferenceEquals(Document, request.Document))
        {
            throw new ArgumentException(
                "Edit request must target the current canvas document.",
                nameof(request));
        }
    }

    private static bool IsMutationOperation(GraphEditOperation operation) =>
        operation is not GraphEditOperation.SelectNodes and
            not GraphEditOperation.SelectEdges and
            not GraphEditOperation.SelectGroup and
            not GraphEditOperation.SelectAnnotation;

    private static string L(string key) => GraphLocalization.Get(key);

}
