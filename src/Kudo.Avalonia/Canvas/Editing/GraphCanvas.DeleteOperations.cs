using Kudo;

namespace Kudo.Avalonia;

public partial class GraphCanvas
{
    public bool CanDeleteSelection() =>
        _editController.CanDeleteSelection();

    public bool TryAuthorizeDeleteSelection() =>
        _editController.TryAuthorizeDeleteSelection();

    public bool DeleteSelection() =>
        _editController.DeleteSelection();
}
