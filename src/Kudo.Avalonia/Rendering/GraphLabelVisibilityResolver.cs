using Kudo;

namespace Kudo.Avalonia;

internal static class GraphLabelVisibilityResolver
{
    public static bool IsNodeNameVisible(
        NodeModel node,
        GraphLabelVisibility symbolVisibility,
        bool isSelected,
        bool isHovered)
    {
        ArgumentNullException.ThrowIfNull(node);
        var appearance = (node.Appearance ?? new GraphNodeAppearance()).Normalize();
        var defaultVisibility = appearance.VisualMode == GraphNodeVisualMode.Symbol
            ? symbolVisibility
            : GraphLabelVisibility.Always;
        return IsVisible(
            appearance.NameVisibility ?? defaultVisibility,
            isSelected,
            isHovered,
            appearance.VisualMode != GraphNodeVisualMode.Symbol || isSelected || isHovered);
    }

    public static bool IsNodeDescriptionVisible(
        NodeModel node,
        GraphLabelVisibility symbolVisibility,
        bool isSelected,
        bool isHovered)
    {
        ArgumentNullException.ThrowIfNull(node);
        var appearance = (node.Appearance ?? new GraphNodeAppearance()).Normalize();
        if (appearance.VisualMode != GraphNodeVisualMode.Symbol)
        {
            return false;
        }

        return IsVisible(
            appearance.DescriptionVisibility ?? symbolVisibility,
            isSelected,
            isHovered,
            isSelected || isHovered);
    }

    public static bool IsVisible(
        GraphLabelVisibility visibility,
        bool isSelected,
        bool isHovered,
        bool automaticVisibility) => visibility switch
        {
            GraphLabelVisibility.Always => true,
            GraphLabelVisibility.SelectedOrHovered => isSelected || isHovered,
            GraphLabelVisibility.Hovered => isHovered,
            GraphLabelVisibility.Never => false,
            _ => automaticVisibility
        };

    public static bool IsPortLabelVisible(
        NodeModel node,
        GraphLabelVisibility canvasVisibility,
        bool isSelected,
        bool isHovered)
    {
        ArgumentNullException.ThrowIfNull(node);
        var appearance = (node.Appearance ?? new GraphNodeAppearance()).Normalize();
        return IsVisible(
            appearance.PortLabelVisibility ?? canvasVisibility,
            isSelected,
            isHovered,
            isSelected || isHovered);
    }
}
