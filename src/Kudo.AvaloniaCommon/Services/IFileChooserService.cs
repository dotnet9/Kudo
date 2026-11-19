namespace Kudo.AvaloniaCommon.Services;

public interface IFileChooserService
{
    void SetHostWindow(TopLevel window);

    Task<List<string>?> OpenFileAsync(string title, IReadOnlyList<FilePickerFileType>? fileTypeFilter,
        bool allowMultiple, string? suggestedFileName = default, string? initialDirectory = default);

    Task<string?> SaveFileAsync(
        string title,
        IReadOnlyList<FilePickerFileType>? fileTypeChoices,
        string? suggestedFileName = default);

    Task<List<string>?> OpenFolderAsync(string title, bool allowMultiple = false, string? initialDirectory = default);
}
