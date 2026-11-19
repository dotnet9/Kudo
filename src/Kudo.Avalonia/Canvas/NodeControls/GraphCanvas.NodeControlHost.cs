using Avalonia.Controls;

namespace Kudo.Avalonia;

internal sealed class GraphNodeControlHost : Decorator
{
    public GraphNodeControlHost(
        string registeredNodeType,
        GraphNodeControlContext context,
        Control child)
    {
        RegisteredNodeType = registeredNodeType;
        Context = context;
        Child = child;
    }

    public string RegisteredNodeType { get; }

    public GraphNodeControlContext Context { get; }
}
