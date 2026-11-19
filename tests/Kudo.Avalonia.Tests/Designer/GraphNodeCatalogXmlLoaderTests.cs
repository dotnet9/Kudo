using System.Globalization;
using CodeWF.Toolkit.Lang;
using CodeWF.Toolkit.Lang.Json;
using Kudo.Avalonia.Designer;
using Shouldly;

namespace Kudo.Avalonia.Tests.Catalog;

public sealed class GraphNodeCatalogXmlLoaderTests
{
    [Fact]
    public void Load_ResolvesLocalizedModelMetadata()
    {
        var directory = Directory.CreateTempSubdirectory("kudo-catalog-");
        try
        {
            File.WriteAllText(
                Path.Combine(directory.FullName, "Kudo.Test.en-US.json"),
                """
                {
                  "language": "English",
                  "description": "Kudo loader test resources",
                  "cultureName": "en-US",
                  "Localization": {
                    "Kudo_Test": {
                      "Models": {
                        "Group": "Localized group",
                        "Name": "Localized symbol",
                        "Description": "Localized description",
                        "Port": "Localized port",
                        "Property": "Localized property",
                        "PropertyGroup": "Localized property group",
                        "Option": "Localized option"
                      }
                    }
                  }
                }
                """);
            File.WriteAllText(
                Path.Combine(directory.FullName, "Model.xml"),
                """
                <ModelLibrary Format="Kudo.ModelLibrary/1">
                  <Groups>
                    <Group Id="test" NameKey="Localization.Kudo_Test.Models.Group">
                      <Symbols>
                        <Symbol Path="Symbols/Test.xml" />
                      </Symbols>
                    </Group>
                  </Groups>
                </ModelLibrary>
                """);
            Directory.CreateDirectory(Path.Combine(directory.FullName, "Symbols"));
            File.WriteAllText(
                Path.Combine(directory.FullName, "Symbols", "Test.xml"),
                """
                <Symbol Format="Kudo.Symbol/1"
                        Id="test"
                         NameKey="Localization.Kudo_Test.Models.Name"
                        DescriptionKey="Localization.Kudo_Test.Models.Description"
                        NodeType="Test.Node"
                        TemplateKey="Test">
                  <Ports>
                    <Port Id="input"
                          NameKey="Localization.Kudo_Test.Models.Port"
                          Kind="Input" />
                  </Ports>
                  <PropertyDefinitions>
                    <Property Name="Value"
                              DisplayKey="Localization.Kudo_Test.Models.Property"
                              GroupKey="Localization.Kudo_Test.Models.PropertyGroup">
                      <Options>
                        <Option Value="one"
                                DisplayKey="Localization.Kudo_Test.Models.Option" />
                      </Options>
                    </Property>
                  </PropertyDefinitions>
                </Symbol>
                """);

            I18nManager.Instance.Register(new JsonLangPlugin
            {
                ResourceFolder = directory.FullName
            }, CultureInfo.GetCultureInfo(GraphLocalization.DefaultCultureName));
            GraphLocalization.Register(CultureInfo.GetCultureInfo(GraphLocalization.DefaultCultureName));

            var catalog = GraphNodeCatalogXmlLoader.Load(
                Path.Combine(directory.FullName, "Model.xml"));
            var definition = catalog.FindById("test");

            definition.ShouldNotBeNull();
            catalog.Groups.Single().Name.ShouldBe("Localized group");
            definition!.Name.ShouldBe("Localized symbol");
            definition.Description.ShouldBe("Localized description");
            definition.DefaultPorts.Single().Name.ShouldBe("Localized port");
            definition.PropertyDefinitions.Single().DisplayName.ShouldBe("Localized property");
            definition.PropertyDefinitions.Single().GroupName.ShouldBe("Localized property group");
            definition.PropertyDefinitions.Single().ChoiceOptions.Single().DisplayName
                .ShouldBe("Localized option");
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
