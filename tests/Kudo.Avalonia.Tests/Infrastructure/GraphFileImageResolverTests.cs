using Avalonia.Media.Imaging;
using Shouldly;

namespace Kudo.Avalonia.Tests.Infrastructure;

public sealed class GraphFileImageResolverTests
{
    [Fact]
    public void Resolve_StaticSvg_ReturnsVectorImageWithViewportSize()
    {
        var directory = Directory.CreateTempSubdirectory("kudo-svg-");
        try
        {
            var path = Path.Combine(directory.FullName, "symbol.svg");
            File.WriteAllText(
                path,
                """
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 40.8333">
                  <circle cx="16" cy="16" r="16" />
                </svg>
                """);
            using var resolver = new GraphFileImageResolver(() => directory.FullName);

            var image = resolver.Resolve("symbol.svg");

            image.ShouldBeOfType<GraphSvgImage>();
            image.ShouldNotBeOfType<Bitmap>();
            image!.Size.Width.ShouldBe(32, 0.0001);
            image.Size.Height.ShouldBe(40.8333, 0.0001);
            resolver.Resolve("symbol.svg").ShouldBeSameAs(image);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
