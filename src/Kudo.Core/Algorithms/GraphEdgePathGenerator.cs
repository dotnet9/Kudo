namespace Kudo.Core.Algorithms;

/// <summary>
/// Optionally transforms a routed edge into the final drawable path.
/// </summary>
public interface IGraphEdgePathGenerator
{
    /// <summary>
    /// The routing style this generator handles.
    /// </summary>
    EdgeRoutingStyle RoutingStyle { get; }

    /// <summary>
    /// Generates the final path from the router result.
    /// </summary>
    GraphEdgePath Generate(GraphEdgeRoutingRequest request, GraphEdgePath routedPath);
}
