namespace Kudo.AvaloniaCommon.ViewModels;

public class NoRegisterDialogViewModel : ViewModelBase
{
    public static string? CustomTitle { get; set; }
    public static string? CustomDescription { get; set; }
    public static string? CustomErrorCode { get; set; }

    public string? Title => CustomTitle;
    public string? Message => CustomDescription;
    public string? ErrorCode => CustomErrorCode;

    public Task RaiseOkHandlerAsync(object? parameter)
    {
        if (parameter is Window window) window.Close();
        return Task.CompletedTask;
    }
}
