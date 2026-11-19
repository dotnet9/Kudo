using Kudo.AvaloniaCommon.ViewModels;
using Kudo.AvaloniaCommon.Views;

namespace Kudo.AvaloniaCommon.Helpers;

public static class DialogHelper
{
    /// <summary>
    ///     弹出退出提示
    /// </summary>
    /// <param name="message">退出提示内容</param>
    /// <param name="mode"></param>
    /// <param name="button"></param>
    /// <returns></returns>
    public static async Task ShowOptionDialogAsync(Window owner,
        bool hideTrayIconOnClose, bool needExitDialogOnClose, Action<ExitOptionViewModel> overAction)
    {
        var view = new ExitOptionView();
        var vm = view.DataContext as ExitOptionViewModel;
        if (vm == null)
        {
            vm = new ExitOptionViewModel();
            view.DataContext = vm;
        }

        vm.HideTrayIconOnClose = hideTrayIconOnClose;
        vm.NeedExitDialogOnClose = needExitDialogOnClose;
        vm.SuccessEvent += (s, e) => overAction.Invoke(e);
        await view.ShowDialog(owner);
    }

    public static void AdjustWindowSize(this Window window)
    {
        if (window.Screens.Primary is not { } screen) return;

        const double resolutionThreshold = 1920 + 50;
        var isSmaller = screen.WorkingArea.Width < resolutionThreshold;
        var targetWidth = isSmaller ? 1200 : 1920;
        var targetHeight = isSmaller ? 800 : 1080;
        window.Width = Math.Min(targetWidth, screen.WorkingArea.Width);
        window.Height = Math.Min(targetHeight, screen.WorkingArea.Height);
    }
}