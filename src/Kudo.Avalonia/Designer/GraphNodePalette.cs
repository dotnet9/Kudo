using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Kudo.Avalonia;
using Kudo.Avalonia.Controls;
using CodeWF.Toolkit.Lang;
using Kudo.Avalonia.Designer;
using System.Windows.Input;
using PaletteLanguage = global::Localization.Kudo_Avalonia.Designer.GraphNodePalette;

namespace Kudo.Avalonia.Designer;

public sealed class GraphNodePalette : GraphDesignerPanel
{
    private const double DefaultGroupPaneHeight = 198;
    private const double MinimumGroupPaneHeight = 36;
    private const double ModelItemWidth = 56;
    private const double ModelItemHeight = 80;

    public static readonly StyledProperty<IGraphNodeCatalog?> CatalogProperty =
        AvaloniaProperty.Register<GraphNodePalette, IGraphNodeCatalog?>(nameof(Catalog));

    public static readonly StyledProperty<string?> ModelDirectoryProperty =
        AvaloniaProperty.Register<GraphNodePalette, string?>(
            nameof(ModelDirectory),
            Path.Combine(AppContext.BaseDirectory, "Models"));

    public static readonly StyledProperty<ICommand?> NodeRequestedCommandProperty =
        AvaloniaProperty.Register<GraphNodePalette, ICommand?>(nameof(NodeRequestedCommand));

    public static readonly StyledProperty<bool> AutoLoadModelDirectoryCatalogProperty =
        AvaloniaProperty.Register<GraphNodePalette, bool>(
            nameof(AutoLoadModelDirectoryCatalog),
            true);

    public static readonly DirectProperty<GraphNodePalette, bool> IsModelDirectoryLoadingProperty =
        AvaloniaProperty.RegisterDirect<GraphNodePalette, bool>(
            nameof(IsModelDirectoryLoading),
            palette => palette.IsModelDirectoryLoading);

    public static readonly StyledProperty<bool> IsClickToAddEnabledProperty =
        AvaloniaProperty.Register<GraphNodePalette, bool>(
            nameof(IsClickToAddEnabled),
            true);

    public static readonly StyledProperty<bool> IsLoadingProperty =
        AvaloniaProperty.Register<GraphNodePalette, bool>(nameof(IsLoading));

    public static readonly StyledProperty<string?> LoadingTitleProperty =
        AvaloniaProperty.Register<GraphNodePalette, string?>(nameof(LoadingTitle));

    public static readonly StyledProperty<string?> LoadingMessageProperty =
        AvaloniaProperty.Register<GraphNodePalette, string?>(nameof(LoadingMessage));

    public static readonly StyledProperty<string?> LoadErrorMessageProperty =
        AvaloniaProperty.Register<GraphNodePalette, string?>(nameof(LoadErrorMessage));

    public static readonly StyledProperty<string?> ReloadTextProperty =
        AvaloniaProperty.Register<GraphNodePalette, string?>(nameof(ReloadText));

    public static readonly StyledProperty<ICommand?> ReloadCommandProperty =
        AvaloniaProperty.Register<GraphNodePalette, ICommand?>(nameof(ReloadCommand));

    private readonly StackPanel _groups = new() { Spacing = 2 };
    private readonly StackPanel _models = new() { Spacing = 6 };
    private readonly TextBlock _catalogTitle = new()
    {
        Text = GraphLocalization.Get(PaletteLanguage.CatalogTitle)
    };
    private readonly TextBox _search = new()
    {
        Name = "GraphNodePaletteSearchBox",
        PlaceholderText = GraphLocalization.Get(PaletteLanguage.SearchPlaceholder)
    };
    private readonly RowDefinition _groupRow;
    private readonly RowDefinition _splitterRow;
    private readonly ScrollViewer _groupScrollViewer;
    private readonly GridSplitter _splitter;
    private readonly GraphLoadingHost _loadingHost;
    private readonly Dictionary<string, IImage> _icons = new(StringComparer.OrdinalIgnoreCase);
    private long _iconLoadVersion;
    private Border? _root;
    private GraphNodeCatalogGroup? _selectedGroup;
    private CancellationTokenSource? _modelDirectoryLoadCancellation;
    private long _modelDirectoryLoadVersion;
    private bool _isDragging;
    private bool _isSearchActive;
    private bool _isAttachedToVisualTree;
    private bool _isApplyingModelDirectoryCatalog;
    private bool _isModelDirectoryCatalogActive;
    private bool _isModelDirectoryLoading;
    private double _groupPaneHeight = DefaultGroupPaneHeight;
    private int _visibleModelCount;
    private string _emptyText = GraphLocalization.Get(PaletteLanguage.EmptyText);
    private bool _isSearchPlaceholderCustomized;
    private bool _isEmptyTextCustomized;

    public GraphNodePalette()
    {
        _search.TextChanged += (_, _) => RefreshModels();
        _search.Classes.Add("graph-node-palette-search");
        _catalogTitle.Classes.Add("graph-node-palette-title");
        var searchIcon = new PathIcon
        {
            Width = 14,
            Height = 14,
            Margin = new Thickness(0, 0, 8, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false
        };
        searchIcon[!PathIcon.DataProperty] = new DynamicResourceExtension("SemiIconSearchStroked");
        searchIcon.Classes.Add("graph-node-palette-search-icon");
        var searchHost = new Grid { Children = { _search, searchIcon } };
        var rows = new RowDefinitions($"Auto,Auto,{DefaultGroupPaneHeight},6,*");
        _groupRow = rows[2];
        _groupRow.MinHeight = MinimumGroupPaneHeight;
        _splitterRow = rows[3];
        rows[4].MinHeight = 80;
        _groupScrollViewer = new ScrollViewer
        {
            Content = _groups,
            VerticalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
        };
        _splitter = new GridSplitter
        {
            Name = "GraphNodePaletteSplitter",
            Height = 6,
            MinHeight = 6,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ResizeDirection = GridResizeDirection.Rows,
            ResizeBehavior = GridResizeBehavior.PreviousAndNext,
            Cursor = new Cursor(StandardCursorType.SizeNorthSouth),
            Background = DividerBrush
        };
        _splitter.Classes.Add("graph-node-palette-splitter");
        var layout = new Grid
        {
            Margin = new Thickness(6),
            RowDefinitions = rows,
            Children =
            {
                _catalogTitle,
                searchHost,
                _groupScrollViewer,
                _splitter,
                new ScrollViewer
                {
                    Content = _models,
                    HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                    VerticalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
                }
            }
        };
        _search.Margin = new Thickness(0, 0, 0, 6);
        for (var index = 0; index < layout.Children.Count; index++)
        {
            Grid.SetRow(layout.Children[index], index);
        }

        _root = new Border
        {
            Background = PanelBrush,
            Child = layout
        };
        _root.Classes.Add("graph-node-palette-root");
        _loadingHost = new GraphLoadingHost
        {
            LoadedContent = _root,
            LoadingTitle = LoadingTitle,
            LoadingMessage = LoadingMessage,
            ErrorMessage = LoadErrorMessage,
            RetryText = ReloadText,
            RetryCommand = ReloadCommand
        };
        Content = _loadingHost;
        RefreshCatalog();
        RefreshLoadingState();
    }

    public GraphNodePalette(IGraphNodeCatalog catalog) : this()
    {
        Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    public IGraphNodeCatalog? Catalog
    {
        get => GetValue(CatalogProperty);
        set => SetValue(CatalogProperty, value);
    }

    public string? ModelDirectory
    {
        get => GetValue(ModelDirectoryProperty);
        set => SetValue(ModelDirectoryProperty, value);
    }

    public ICommand? NodeRequestedCommand
    {
        get => GetValue(NodeRequestedCommandProperty);
        set => SetValue(NodeRequestedCommandProperty, value);
    }

    public bool AutoLoadModelDirectoryCatalog
    {
        get => GetValue(AutoLoadModelDirectoryCatalogProperty);
        set => SetValue(AutoLoadModelDirectoryCatalogProperty, value);
    }

    public bool IsModelDirectoryLoading
    {
        get => _isModelDirectoryLoading;
        private set => SetAndRaise(
            IsModelDirectoryLoadingProperty,
            ref _isModelDirectoryLoading,
            value);
    }

    public bool IsClickToAddEnabled
    {
        get => GetValue(IsClickToAddEnabledProperty);
        set => SetValue(IsClickToAddEnabledProperty, value);
    }

    public bool IsLoading
    {
        get => GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    public string? LoadingTitle
    {
        get => GetValue(LoadingTitleProperty);
        set => SetValue(LoadingTitleProperty, value);
    }

    public string? LoadingMessage
    {
        get => GetValue(LoadingMessageProperty);
        set => SetValue(LoadingMessageProperty, value);
    }

    public string? LoadErrorMessage
    {
        get => GetValue(LoadErrorMessageProperty);
        set => SetValue(LoadErrorMessageProperty, value);
    }

    public string? ReloadText
    {
        get => GetValue(ReloadTextProperty);
        set => SetValue(ReloadTextProperty, value);
    }

    public ICommand? ReloadCommand
    {
        get => GetValue(ReloadCommandProperty);
        set => SetValue(ReloadCommandProperty, value);
    }

    public int GroupCount => Catalog?.Groups.Count ?? 0;

    public int VisibleModelCount => _visibleModelCount;

    public string SelectedGroupId => _selectedGroup?.Id ?? string.Empty;

    public string SelectedGroupName => _selectedGroup?.Name ?? string.Empty;

    public string? SearchText
    {
        get => _search.Text;
        set
        {
            _search.Text = value;
            RefreshModels();
        }
    }

    public string SearchPlaceholder
    {
        get => _search.PlaceholderText ?? string.Empty;
        set
        {
            _isSearchPlaceholderCustomized = true;
            _search.PlaceholderText = value;
        }
    }

    public string EmptyText
    {
        get => _emptyText;
        set
        {
            _isEmptyTextCustomized = true;
            _emptyText = value ?? string.Empty;
            RefreshModels();
        }
    }

    public event Action<GraphNodeDefinition>? NodeRequested;

    public bool SelectGroup(string groupId)
    {
        var group = Catalog?.Groups.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, groupId, StringComparison.Ordinal));
        if (group is null)
        {
            return false;
        }

        _selectedGroup = group;
        RefreshGroups();
        RefreshModels();
        return true;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CatalogProperty)
        {
            if (!_isApplyingModelDirectoryCatalog)
            {
                _isModelDirectoryCatalogActive = false;
                CancelModelDirectoryLoad();
            }

            DisposeModelIcons();
            RefreshCatalog();
        }
        else if (change.Property == ModelDirectoryProperty)
        {
            if (AutoLoadModelDirectoryCatalog && _isAttachedToVisualTree)
            {
                StartLoadingModelDirectoryCatalog();
            }
        }
        else if (change.Property == AutoLoadModelDirectoryCatalogProperty)
        {
            if (!AutoLoadModelDirectoryCatalog)
            {
                CancelModelDirectoryLoad();
            }
            else if (_isAttachedToVisualTree && Catalog is null)
            {
                StartLoadingModelDirectoryCatalog();
            }
        }
        else if (change.Property == IsLoadingProperty ||
                 change.Property == LoadingTitleProperty ||
                 change.Property == LoadingMessageProperty ||
                 change.Property == LoadErrorMessageProperty ||
                 change.Property == ReloadTextProperty ||
                 change.Property == ReloadCommandProperty)
        {
            RefreshLoadingState();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _isAttachedToVisualTree = true;
        I18nManager.Instance.CultureChanged += HandleCultureChanged;
        RefreshCatalog();
        if (AutoLoadModelDirectoryCatalog && Catalog is null)
        {
            StartLoadingModelDirectoryCatalog();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _isAttachedToVisualTree = false;
        CancelModelDirectoryLoad();
        I18nManager.Instance.CultureChanged -= HandleCultureChanged;
        // Dock 浮动和拖拽会临时迁移可视树；位图必须保留到控件重新挂载。
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPaletteChanged()
    {
        if (_root is not null)
        {
            _root.Background = PanelBrush;
        }

        _splitter.Background = DividerBrush;
        _loadingHost.PanelBrush = PanelBrush;
        _loadingHost.ElevatedBrush = ElevatedBrush;
        _loadingHost.DividerBrush = DividerBrush;
        _loadingHost.PrimaryTextBrush = PrimaryTextBrush;
        _loadingHost.SecondaryTextBrush = SecondaryTextBrush;
        _loadingHost.ErrorBrush = ErrorBrush;

        RefreshGroups();
        RefreshModels();
    }

    private void RefreshLoadingState()
    {
        _loadingHost.IsLoading = IsLoading;
        _loadingHost.LoadingTitle = LoadingTitle;
        _loadingHost.LoadingMessage = LoadingMessage;
        _loadingHost.ErrorMessage = LoadErrorMessage;
        _loadingHost.RetryText = ReloadText;
        _loadingHost.RetryCommand = ReloadCommand;
    }

    private void RefreshCatalog()
    {
        var selectedGroupId = _selectedGroup?.Id;
        _selectedGroup = Catalog?.Groups.FirstOrDefault(group =>
            string.Equals(group.Id, selectedGroupId, StringComparison.Ordinal))
            ?? Catalog?.Groups.FirstOrDefault();
        RefreshGroups();
        RefreshModels();
    }

    private void HandleCultureChanged(object? sender, EventArgs e)
    {
        if (!_isSearchPlaceholderCustomized)
        {
            _search.PlaceholderText = GraphLocalization.Get(PaletteLanguage.SearchPlaceholder);
        }

        _catalogTitle.Text = GraphLocalization.Get(PaletteLanguage.CatalogTitle);

        if (!_isEmptyTextCustomized)
        {
            _emptyText = GraphLocalization.Get(PaletteLanguage.EmptyText);
        }

        if (_isModelDirectoryCatalogActive)
        {
            StartLoadingModelDirectoryCatalog();
        }

        RefreshModels();
    }

    private void StartLoadingModelDirectoryCatalog()
    {
        CancelModelDirectoryLoad();
        if (!AutoLoadModelDirectoryCatalog || !_isAttachedToVisualTree)
        {
            return;
        }

        string? path;
        try
        {
            path = ResolveModelLibraryPath(ModelDirectory);
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            NotSupportedException or
            PathTooLongException)
        {
            _isModelDirectoryCatalogActive = false;
            return;
        }

        if (path is null)
        {
            _isModelDirectoryCatalogActive = false;
            return;
        }

        var cancellation = new CancellationTokenSource();
        _modelDirectoryLoadCancellation = cancellation;
        var version = ++_modelDirectoryLoadVersion;
        IsModelDirectoryLoading = true;
        IsLoading = true;
        LoadErrorMessage = null;
        _ = LoadModelDirectoryCatalogAsync(path, version, cancellation);
    }

    private async Task LoadModelDirectoryCatalogAsync(
        string path,
        long version,
        CancellationTokenSource cancellation)
    {
        try
        {
            var catalog = await GraphNodeCatalogXmlLoader
                .LoadAsync(path, cancellation.Token)
                .ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (version != _modelDirectoryLoadVersion ||
                    cancellation.IsCancellationRequested ||
                    !_isAttachedToVisualTree)
                {
                    return;
                }

                _isApplyingModelDirectoryCatalog = true;
                try
                {
                    Catalog = catalog;
                    _isModelDirectoryCatalogActive = true;
                }
                finally
                {
                    _isApplyingModelDirectoryCatalog = false;
                }
            });
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            InvalidDataException or
            System.Xml.XmlException)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (version == _modelDirectoryLoadVersion)
                {
                    _isModelDirectoryCatalogActive = false;
                    LoadErrorMessage = exception.Message;
                }
            });
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (version == _modelDirectoryLoadVersion)
                {
                    _modelDirectoryLoadCancellation = null;
                    IsModelDirectoryLoading = false;
                    IsLoading = false;
                }
            });
            cancellation.Dispose();
        }
    }

    private void CancelModelDirectoryLoad()
    {
        _modelDirectoryLoadVersion++;
        _modelDirectoryLoadCancellation?.Cancel();
        _modelDirectoryLoadCancellation = null;
        IsModelDirectoryLoading = false;
        IsLoading = false;
    }

    private static string? ResolveModelLibraryPath(string? modelDirectory)
    {
        var path = string.IsNullOrWhiteSpace(modelDirectory)
            ? Path.Combine(AppContext.BaseDirectory, "Models")
            : modelDirectory;
        var fullPath = Path.GetFullPath(path);
        if (File.Exists(fullPath))
        {
            return fullPath;
        }

        var modelPath = Path.Combine(fullPath, "Model.xml");
        return File.Exists(modelPath) ? modelPath : null;
    }

    private void RefreshGroups()
    {
        _groups.Children.Clear();
        foreach (var group in Catalog?.Groups ?? [])
        {
            var currentGroup = group;
            var isSelected = string.Equals(
                group.Id,
                _selectedGroup?.Id,
                StringComparison.Ordinal);
            var icon = CreateGroupIcon(group);
            icon.Classes.Add("graph-node-palette-group-icon");
            var title = new TextBlock
            {
                Text = group.Name,
                VerticalAlignment = VerticalAlignment.Center
            };
            var content = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("18,*"),
                ColumnSpacing = 6,
                Children = { icon, title }
            };
            Grid.SetColumn(title, 1);
            var button = new Button
            {
                Content = content,
                Tag = group,
                MinHeight = 28,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left
            };
            button.Classes.Add("PropertyGroupHeader");
            button.Classes.Add("graph-node-palette-group");
            button.Classes.Set("selected", isSelected);
            button.Click += (_, _) =>
            {
                _selectedGroup = currentGroup;
                RefreshGroups();
                RefreshModels();
            };
            _groups.Children.Add(button);
        }
    }

    private void RefreshModels()
    {
        _models.Children.Clear();
        var catalog = Catalog;
        var searchText = _search.Text?.Trim();
        var isSearchActive = !string.IsNullOrWhiteSpace(searchText);
        SetSearchActive(isSearchActive);
        var definitions = catalog is null
            ? []
            : !isSearchActive
                ? _selectedGroup?.Definitions ?? []
                : catalog.Search(searchText);
        _visibleModelCount = definitions.Count;
        if (isSearchActive)
        {
            foreach (var group in catalog?.Groups ?? [])
            {
                var groupDefinitions = definitions
                    .Where(definition => string.Equals(
                        definition.GroupId,
                        group.Id,
                        StringComparison.Ordinal))
                    .ToArray();
                if (groupDefinitions.Length == 0)
                {
                    continue;
                }

                _models.Children.Add(new TextBlock
                {
                    Text = group.Name,
                    Margin = new Thickness(4, 4, 4, 2),
                    FontWeight = FontWeight.SemiBold,
                    Foreground = PrimaryTextBrush
                });
                _models.Children.Add(CreateModelPanel(groupDefinitions));
            }
        }
        else if (_visibleModelCount > 0)
        {
            _models.Children.Add(CreateModelPanel(definitions));
        }

        if (_visibleModelCount == 0)
        {
            _models.Children.Add(new TextBlock
            {
                Text = EmptyText,
                Margin = new Thickness(4),
                Foreground = SecondaryTextBrush
            });
        }
    }

    private void SetSearchActive(bool value)
    {
        if (_isSearchActive == value)
        {
            return;
        }

        _isSearchActive = value;
        if (value)
        {
            if (_groupRow.ActualHeight > 0)
            {
                _groupPaneHeight = _groupRow.ActualHeight;
            }

            _groupScrollViewer.IsVisible = false;
            _splitter.IsVisible = false;

            _groupRow.MinHeight = 0;
            _groupRow.Height = new GridLength(0);
            _splitterRow.Height = new GridLength(0);
            return;
        }

        _groupRow.MinHeight = MinimumGroupPaneHeight;
        _groupRow.Height = new GridLength(Math.Max(MinimumGroupPaneHeight, _groupPaneHeight));
        _splitterRow.Height = new GridLength(6);
        _groupScrollViewer.IsVisible = true;
        _splitter.IsVisible = true;
    }

    private WrapPanel CreateModelPanel(IEnumerable<GraphNodeDefinition> definitions)
    {
        var panel = new WrapPanel
        {
            ItemWidth = ModelItemWidth,
            ItemHeight = ModelItemHeight,
            ItemSpacing = 6,
            LineSpacing = 6,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        foreach (var definition in definitions)
        {
            panel.Children.Add(CreateModelButton(definition));
        }

        return panel;
    }

    private Button CreateModelButton(GraphNodeDefinition definition)
    {
        Point? dragStart = null;
        PointerPressedEventArgs? dragStartEvent = null;
        var dragPointerId = 0;
        var didDrag = false;
        var button = new Button
        {
            Tag = definition,
            Width = ModelItemWidth,
            Height = ModelItemHeight,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Content = new StackPanel
            {
                Spacing = 4,
                HorizontalAlignment = HorizontalAlignment.Center,
                Children =
                {
                    new Border
                    {
                        Width = 44,
                        Height = 32,
                        Child = CreateModelIcon(definition)
                    },
                    new TextBlock
                    {
                        Text = definition.Name,
                        Width = 54,
                        Height = 28,
                        LineHeight = 14,
                        TextAlignment = TextAlignment.Center,
                        TextTrimming = TextTrimming.None,
                        TextWrapping = TextWrapping.Wrap,
                        MaxLines = 2
                    }
                }
            }
        };
        button.Classes.Add("graph-node-palette-model");
        ((StackPanel)button.Content).Children[0].Classes.Add("graph-node-palette-icon");
        ((StackPanel)button.Content).Children[1].Classes.Add("graph-node-palette-model-title");
        GraphToolTipHelper.SetText(button, definition.Description);
        button.Click += (_, _) =>
        {
            if (didDrag)
            {
                didDrag = false;
                return;
            }

            if (!IsClickToAddEnabled)
            {
                return;
            }

            NodeRequested?.Invoke(definition);
            if (NodeRequestedCommand?.CanExecute(definition) == true)
            {
                NodeRequestedCommand.Execute(definition);
            }
        };
        button.AddHandler(InputElement.PointerPressedEvent, (_, e) =>
        {
            if (_isDragging || !e.GetCurrentPoint(button).Properties.IsLeftButtonPressed)
            {
                return;
            }

            didDrag = false;
            dragStart = e.GetPosition(button);
            dragPointerId = e.Pointer.Id;
            dragStartEvent = e;
        }, RoutingStrategies.Tunnel);
        button.AddHandler(InputElement.PointerMovedEvent, (_, e) =>
        {
            if (_isDragging || dragStart is null || dragStartEvent is null || e.Pointer.Id != dragPointerId)
            {
                return;
            }

            var delta = e.GetPosition(button) - dragStart.Value;
            if (Math.Abs(delta.X) < 4 && Math.Abs(delta.Y) < 4)
            {
                return;
            }

            didDrag = true;
            var triggerEvent = dragStartEvent;
            dragStart = null;
            dragStartEvent = null;
            BeginModelDrag(triggerEvent, definition);
        }, RoutingStrategies.Tunnel);
        button.AddHandler(InputElement.PointerReleasedEvent, (_, _) =>
        {
            dragStart = null;
            dragStartEvent = null;
        }, RoutingStrategies.Tunnel);
        return button;
    }

    private Control CreateModelIcon(GraphNodeDefinition definition)
    {
        var path = definition.ResolveResourcePath(definition.Icon);
        if (!string.IsNullOrWhiteSpace(path))
        {
            if (_icons.TryGetValue(path, out var icon))
            {
                return CreateImage(icon);
            }

            var host = CreateDeferredImageIcon(path, CreateTextIcon(definition.IconText, 9));
            QueueIconLoad(path, host.Image, host.Fallback, host.Version);
            return host.Container;
        }

        return CreateTextIcon(definition.IconText, 9);
    }

    private Control CreateGroupIcon(GraphNodeCatalogGroup group)
    {
        if (!string.IsNullOrWhiteSpace(group.Icon))
        {
            if (_icons.TryGetValue(group.Icon, out var icon))
            {
                var image = CreateImage(icon);
                image.Width = 18;
                image.Height = 18;
                return image;
            }

            var host = CreateDeferredImageIcon(
                group.Icon,
                CreateTextIcon(string.IsNullOrWhiteSpace(group.IconText) ? "G" : group.IconText, 14));
            host.Image.Width = 18;
            host.Image.Height = 18;
            QueueIconLoad(group.Icon, host.Image, host.Fallback, host.Version);
            return host.Container;
        }

        return CreateTextIcon(string.IsNullOrWhiteSpace(group.IconText) ? "G" : group.IconText, 14);
    }

    private (Grid Container, Image Image, Control Fallback, long Version) CreateDeferredImageIcon(
        string path,
        Control fallback)
    {
        var image = new Image
        {
            Stretch = global::Avalonia.Media.Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            IsVisible = false
        };
        return (
            new Grid { Children = { fallback, image } },
            image,
            fallback,
            Interlocked.Read(ref _iconLoadVersion));
    }

    private void QueueIconLoad(
        string path,
        Image image,
        Control fallback,
        long version)
    {
        _ = LoadIconAsync(path, image, fallback, version);
    }

    private async Task LoadIconAsync(
        string path,
        Image image,
        Control fallback,
        long version)
    {
        try
        {
            var icon = await Task
                .Run(() => GraphFileImageResolver.TryLoadThumbnail(path, 64, out var loadedIcon)
                    ? loadedIcon
                    : null)
                .ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (version != Interlocked.Read(ref _iconLoadVersion) ||
                    !_isAttachedToVisualTree ||
                    icon is null)
                {
                    DisposeIfUnused(icon);
                    return;
                }

                if (!_icons.TryAdd(path, icon) &&
                    _icons.TryGetValue(path, out var cachedIcon))
                {
                    DisposeIfUnused(icon);
                    icon = cachedIcon;
                }

                image.Source = icon;
                image.IsVisible = true;
                fallback.IsVisible = false;
            });
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
        }
    }

    private static Image CreateImage(IImage icon) =>
        new()
        {
            Source = icon,
            Stretch = global::Avalonia.Media.Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

    private static void DisposeIfUnused(IImage? icon)
    {
        if (icon is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private static TextBlock CreateTextIcon(
        string text,
        double fontSize) =>
        new()
        {
            Text = text,
            Width = 18,
            FontSize = fontSize,
            FontWeight = FontWeight.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center
        };

    private void DisposeModelIcons()
    {
        Interlocked.Increment(ref _iconLoadVersion);
        foreach (var icon in _icons.Values)
        {
            if (icon is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        _icons.Clear();
    }

    private async void BeginModelDrag(
        PointerPressedEventArgs triggerEvent,
        GraphNodeDefinition definition)
    {
        _isDragging = true;
        try
        {
            await DragDrop.DoDragDropAsync(
                triggerEvent,
                GraphNodeDragDrop.CreateData(definition),
                DragDropEffects.Copy);
        }
        finally
        {
            _isDragging = false;
        }
    }
}
