// 分组层渲染：在边和节点之前绘制分组背景与折叠代理端口。
using Kudo;

namespace Kudo.Avalonia;

internal sealed class GraphCanvasGroupRenderPass
{
    public void Render(
        GraphCanvasRenderRequest request,
        IEnumerable<GroupModel> groups)
    {
        foreach (var group in groups)
        {
            if (request.Callbacks.GetGroupScreenRect(group) is not { } rect)
            {
                continue;
            }

            var isSelected = request.Callbacks.IsGroupSelected(group.Id);
            request.GroupRenderer.Render(new GraphGroupRenderContext(
                request.Context,
                group,
                rect,
                request.Zoom,
                isSelected,
                group.IsCollapsed ? request.Callbacks.GetCollapsedGroupProxyPorts(group) : [],
                request.Palette.GroupFillBrush,
                request.Palette.GroupHeaderFillBrush,
                isSelected
                    ? request.Palette.SelectedGroupStrokePen
                    : request.Palette.GroupStrokePen,
                request.Palette.GroupTitleBrush,
                request.Palette.InputPortBrush,
                request.Palette.OutputPortBrush,
                request.Palette.PortStrokePen));
        }
    }
}
