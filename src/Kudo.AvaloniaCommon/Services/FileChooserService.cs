namespace Kudo.AvaloniaCommon.Services;

public class FileChooserService : IFileChooserService
{
    private IStorageProvider? _storageProvider;

    public async Task<List<string>?> OpenFileAsync(string title, IReadOnlyList<FilePickerFileType>? fileTypeFilter,
        bool isAllowMultiple, string? suggestedFileName, string? initialDirectory)
    {
        if (_storageProvider is null) throw new ArgumentNullException(nameof(_storageProvider));
        var options = new FilePickerOpenOptions
        {
            Title = title,
            FileTypeFilter = fileTypeFilter,
            AllowMultiple = isAllowMultiple
        };
        if (!string.IsNullOrWhiteSpace(initialDirectory))
            try
            {
                var initialFolder = await _storageProvider.TryGetFolderFromPathAsync(initialDirectory);
                if (initialFolder != null) options.SuggestedStartLocation = initialFolder;
            }
            catch (Exception ex)
            {
                var friendlyMsg = "设置初始目录失败, 请联系管理员！";
                Logger.Error("设置初始目录失败", ex, friendlyMsg);
            }

        if (!string.IsNullOrWhiteSpace(suggestedFileName)) options.SuggestedFileName = suggestedFileName;
        var result = await _storageProvider.OpenFilePickerAsync(options);
        var paths = result
            .Select(file => file.TryGetLocalPath())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path!)
            .ToList();
        return paths.Count > 0 ? paths : default;
    }

    public async Task<string?> SaveFileAsync(
        string title,
        IReadOnlyList<FilePickerFileType>? fileTypeChoices,
        string? suggestedFileName)
    {
        if (_storageProvider is null) throw new ArgumentNullException(nameof(_storageProvider));
        var result = await _storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            FileTypeChoices = fileTypeChoices,
            SuggestedFileName = suggestedFileName
        });
        return result == null ? default : result.TryGetLocalPath();
    }

    public async Task<List<string>?> OpenFolderAsync(string title, bool allowMultiple, string? initialDirectory)
    {
        if (_storageProvider is null) throw new ArgumentNullException(nameof(_storageProvider));
        var options = new FolderPickerOpenOptions { Title = title, AllowMultiple = allowMultiple };
        if (!string.IsNullOrWhiteSpace(initialDirectory))
            try
            {
                var initialFolder = await _storageProvider.TryGetFolderFromPathAsync(initialDirectory);
                if (initialFolder != null) options.SuggestedStartLocation = initialFolder;
            }
            catch (Exception ex)
            {
                var friendlyMsg = "设置初始目录失败, 请联系管理员！";
                Logger.Error("设置初始目录失败", ex, friendlyMsg);
            }

        var result = await _storageProvider.OpenFolderPickerAsync(options);
        var paths = result
            .Select(file => file.TryGetLocalPath())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path!)
            .ToList();
        return paths.Count > 0 ? paths : default;
    }

    public void SetHostWindow(TopLevel topLevel)
    {
        _storageProvider = topLevel?.StorageProvider;
    }
}
