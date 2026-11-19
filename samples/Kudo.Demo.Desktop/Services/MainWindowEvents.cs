using CodeWF.Toolkit.EventBus;
using Kudo.Demo.Desktop.ViewModels;
using Kudo.Avalonia.Designer;

namespace Kudo.Demo.Desktop.Services;

internal sealed class MainWindowWorkspaceChangedCommand(MainWindowWorkspaceState workspace) : Command
{
    public MainWindowWorkspaceState Workspace { get; } = workspace;
}

internal sealed class MainWindowWorkspaceAvailabilityChangedCommand(bool isAvailable) : Command
{
    public bool IsAvailable { get; } = isAvailable;
}

internal sealed class MainWindowPropertyContextChangedCommand(
    GraphPropertyEditorContext? context) : Command
{
    public GraphPropertyEditorContext? Context { get; } = context;
}
