using Kudo.Core.Runtime;

namespace Kudo.Avalonia;

public partial class GraphCanvas
{
    private void UpdateRuntimeSnapshot(GraphRuntimeSnapshot? snapshot) =>
        _runtimeController.UpdateSnapshot(snapshot);

    private void UpdateRuntimeAnimationState() =>
        _runtimeController.UpdateAnimationState();
}
