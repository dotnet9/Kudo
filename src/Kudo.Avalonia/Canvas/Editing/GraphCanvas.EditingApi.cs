using System.Windows.Input;

namespace Kudo.Avalonia;

public partial class GraphCanvas
{
    public ICommand? GraphEditCommittedCommand
    {
        get => GetValue(GraphEditCommittedCommandProperty);
        set => SetValue(GraphEditCommittedCommandProperty, value);
    }

    public ICommand? GraphEditRejectedCommand
    {
        get => GetValue(GraphEditRejectedCommandProperty);
        set => SetValue(GraphEditRejectedCommandProperty, value);
    }

    public IGraphEditCoordinator EditCoordinator
    {
        get => GetValue(EditCoordinatorProperty);
        set => SetValue(EditCoordinatorProperty, value);
    }

    public IGraphEditPolicy EditPolicy
    {
        get => _editPolicy;
        set
        {
            var policy = value ?? throw new ArgumentNullException(nameof(value));
            if (ReferenceEquals(_editPolicy, policy))
            {
                return;
            }

            _editPolicy = policy;
            EditPolicyChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public string? LastEditRejectionReason { get; private set; }

    public bool CanAuthorizeEdit(GraphEditRequest request)
    {
        ValidateEditRequest(request);
        return EvaluateEdit(request).IsAllowed;
    }

    public bool TryAuthorizeEdit(GraphEditRequest request)
    {
        ValidateEditRequest(request);
        return CanPerformEdit(request);
    }
}
