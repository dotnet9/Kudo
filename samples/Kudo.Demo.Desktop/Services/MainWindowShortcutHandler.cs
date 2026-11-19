using Avalonia.Input;
using Kudo.Demo.Desktop.ViewModels;
using Kudo.Core.Runtime;
using System.Windows.Input;

namespace Kudo.Demo.Desktop.Services;

internal sealed class MainWindowShortcutHandler(
    MainRibbonViewModel ribbonViewModel,
    Func<RuntimeStatus?> runtimeStatusProvider)
{
    public void Handle(KeyEventArgs e, bool isTextInputFocused)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (e.Handled || isTextInputFocused)
        {
            return;
        }

        if (e.Key == Key.Escape)
        {
            ribbonViewModel.ExecuteSelectionAction(MainWindowSelectionAction.Clear);
            e.Handled = true;
            return;
        }

        if (TryHandleRuntimeShortcut(e) || TryHandleControlShortcut(e))
        {
            e.Handled = true;
        }
    }

    private bool TryHandleRuntimeShortcut(KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.F5:
                if ((e.KeyModifiers & KeyModifiers.Shift) != 0)
                {
                    Execute(ribbonViewModel.StopRuntimeCommand);
                }
                else if (runtimeStatusProvider() == RuntimeStatus.Running)
                {
                    Execute(ribbonViewModel.PauseRuntimeCommand);
                }
                else
                {
                    Execute(ribbonViewModel.StartRuntimeCommand);
                }

                return true;
            case Key.F6:
                Execute(ribbonViewModel.StepRuntimeCommand);
                return true;
            case Key.F9:
                Execute(ribbonViewModel.ToggleBreakpointCommand);
                return true;
            default:
                return false;
        }
    }

    private bool TryHandleControlShortcut(KeyEventArgs e)
    {
        if (!HasCommandModifier(e.KeyModifiers))
        {
            return false;
        }

        switch (e.Key)
        {
            case Key.G:
                ribbonViewModel.ExecuteDocumentEditAction(
                    (e.KeyModifiers & KeyModifiers.Shift) != 0
                        ? MainWindowDocumentEditAction.UngroupSelection
                        : MainWindowDocumentEditAction.GroupSelection);
                return true;
            case Key.A:
                ribbonViewModel.ExecuteSelectionAction(MainWindowSelectionAction.SelectAll);
                return true;
            case Key.Z when (e.KeyModifiers & KeyModifiers.Shift) != 0:
            case Key.Y:
                ribbonViewModel.ExecuteDocumentEditAction(MainWindowDocumentEditAction.Redo);
                return true;
            case Key.Z:
                ribbonViewModel.ExecuteDocumentEditAction(MainWindowDocumentEditAction.Undo);
                return true;
            case Key.C:
                Execute(ribbonViewModel.ClipboardCommand, MainWindowClipboardAction.Copy);
                return true;
            case Key.X:
                Execute(ribbonViewModel.ClipboardCommand, MainWindowClipboardAction.Cut);
                return true;
            case Key.V:
                Execute(ribbonViewModel.ClipboardCommand, MainWindowClipboardAction.Paste);
                return true;
            case Key.D:
                ribbonViewModel.ExecuteDocumentEditAction(
                    MainWindowDocumentEditAction.DuplicateSelection);
                return true;
            default:
                return false;
        }
    }

    private static void Execute(ICommand command, object? parameter = null)
    {
        if (command.CanExecute(parameter))
        {
            command.Execute(parameter);
        }
    }

    private static bool HasCommandModifier(KeyModifiers modifiers) =>
        OperatingSystem.IsMacOS()
            ? (modifiers & KeyModifiers.Meta) != 0
            : (modifiers & KeyModifiers.Control) != 0;
}
