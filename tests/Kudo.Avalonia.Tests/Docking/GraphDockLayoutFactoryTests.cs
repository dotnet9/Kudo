using Dock.Model.Controls;
using Dock.Model.Core;
using Kudo.Avalonia.Docking;
using Kudo.Core.Documents;
using Shouldly;

namespace Kudo.Avalonia.Tests.Docking;

public sealed class GraphDockLayoutFactoryTests
{
    [Fact]
    public void CreateDockLayout_ShowsCloseButtonOnlyOnActiveDocument()
    {
        var documents = new GraphDocumentManager();
        using var factory = new GraphDockLayoutFactory();

        factory.CreateDockLayout(documents);

        factory.DocumentDock.ShouldNotBeNull();
        factory.DocumentDock!.CloseButtonShowMode.ShouldBe(DocumentCloseButtonShowMode.Active);
    }
}
