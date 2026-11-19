using Kudo.Avalonia;
using Kudo.Avalonia.Designer;

namespace Kudo.Demo.Desktop;

internal static class DemoGraphNodeCatalog
{
    public static IGraphNodeCatalog Create(GraphExtensionRegistry extensions)
    {
        ArgumentNullException.ThrowIfNull(extensions);
        var projectCatalog = GraphNodeCatalogXmlLoader
            .Load(Path.Combine(AppContext.BaseDirectory, "Models", "Model.xml"));
        return extensions.CreateCatalog(projectCatalog);
    }

    public static async Task<IGraphNodeCatalog> CreateAsync(
        GraphExtensionRegistry extensions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(extensions);
        var projectCatalog = await GraphNodeCatalogXmlLoader
            .LoadAsync(
                Path.Combine(AppContext.BaseDirectory, "Models", "Model.xml"),
                cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return extensions.CreateCatalog(projectCatalog);
    }
}
