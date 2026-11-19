namespace Kudo.Avalonia;

public partial class GraphCanvas
{
    public bool CanGroupSelectedNodes() =>
        _groupController.CanGroupSelectedNodes();

    public bool CanUngroupSelectedNodes() =>
        _groupController.CanUngroupSelectedNodes();

    public bool GroupSelectedNodes(string? title = null) =>
        _groupController.GroupSelectedNodes(title);

    public bool UngroupSelectedNodes() =>
        _groupController.UngroupSelectedNodes();

    public bool CanToggleSelectedGroupCollapsed() =>
        _groupController.CanToggleSelectedGroupCollapsed();

    public bool ToggleSelectedGroupCollapsed() =>
        _groupController.ToggleSelectedGroupCollapsed();

    public bool SetGroupCollapsed(string groupId, bool isCollapsed) =>
        _groupController.SetGroupCollapsed(groupId, isCollapsed);
}
