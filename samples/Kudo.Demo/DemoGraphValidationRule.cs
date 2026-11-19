namespace Kudo.Demo;

public sealed class DemoGraphValidationRule : IGraphValidationRule
{
    private readonly IDemoGraphTextProvider? _textProvider;

    public DemoGraphValidationRule(IDemoGraphTextProvider? textProvider = null) =>
        _textProvider = textProvider;

    public IEnumerable<GraphValidationIssue> Validate(GraphDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        foreach (var node in document.Nodes)
        {
            foreach (var port in node.Ports)
            {
                var reference = new PortReference(node.Id, port.Id);
                var isConnected = port.Kind switch
                {
                    PortKind.Input => document.Edges.Any(edge => edge.Target == reference),
                    PortKind.Output => document.Edges.Any(edge => edge.Source == reference),
                    _ => document.Edges.Any(edge =>
                        edge.Source == reference || edge.Target == reference)
                };
                if (isConnected || port.State == PortState.Disabled || IsConnectionDisabled(port))
                {
                    continue;
                }

                var nodeTitle = string.IsNullOrWhiteSpace(node.Name) ? node.Id : node.Name;
                yield return new GraphValidationIssue(
                    ValidationSeverity.Warning,
                    DemoGraphText.Format(
                        _textProvider,
                        DemoGraphTextKey.DisconnectedPort,
                        nodeTitle,
                        port.Name),
                    node.Id,
                    "DEMO1001");
            }
        }
    }

    private static bool IsConnectionDisabled(PortModel port) => port.Kind switch
    {
        PortKind.Input => port.Capacity.MaxIncoming == 0,
        PortKind.Output => port.Capacity.MaxOutgoing == 0,
        PortKind.InputOutput =>
            port.Capacity.MaxTotal == 0 ||
            port.Capacity.MaxIncoming == 0 && port.Capacity.MaxOutgoing == 0,
        _ => port.Capacity.MaxTotal == 0
    };
}
