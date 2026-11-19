namespace Kudo.AvaloniaCommon.ViewModels;

/// <summary>
///     退出提示对话框VM
/// </summary>
public class ExitOptionViewModel : ViewModelBase
{
    /// <summary>
    ///     确认事件
    /// </summary>
    public event EventHandler<ExitOptionViewModel>? SuccessEvent;

    /// <summary>
    ///     关闭窗体时，隐藏到托盘
    /// </summary>
    public bool HideTrayIconOnClose
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>
    ///     关闭窗体时，显示提示
    /// </summary>
    public bool NeedExitDialogOnClose
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public Task RaiseCancelHandlerAsync(object? parameter)
    {
        if (parameter is Window window) window.Close();
        return Task.CompletedTask;
    }

    public Task RaiseSaveHandlerAsync(object? parameter)
    {
        SuccessEvent?.Invoke(this, this);
        if (parameter is Window window) window.Close();
        return Task.CompletedTask;
    }
}
