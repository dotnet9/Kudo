using System.Globalization;
using CodeWF.Toolkit.Lang;
using CodeWF.Toolkit.Lang.Json;
using Shouldly;
using PropertiesLanguage = global::Localization.Kudo_Avalonia.Designer.GraphPropertiesView;

namespace Kudo.Avalonia.Tests;

public sealed class GraphLocalizationTests
{
    [Fact]
    public void EmbeddedResources_FollowGlobalCultureWithoutCopiedLibraryJson()
    {
        I18nManager.Instance.Register(new JsonLangPlugin { ScanResourceFolder = false },
            CultureInfo.GetCultureInfo(GraphLocalization.DefaultCultureName));
        GraphLocalization.Get(PropertiesLanguage.Properties).ShouldBe("Properties");

        I18nManager.Instance.Culture = CultureInfo.GetCultureInfo(GraphLocalization.SimplifiedChineseCultureName);
        GraphLocalization.Get(PropertiesLanguage.Properties).ShouldBe("属性");

        GraphLocalization.Register(CultureInfo.GetCultureInfo("ja-JP"));
        I18nManager.Instance.Culture!.Name.ShouldBe("ja-JP");
        GraphLocalization.Get(PropertiesLanguage.Properties).ShouldBe("Properties");

        I18nManager.Instance.Culture = CultureInfo.GetCultureInfo(GraphLocalization.DefaultCultureName);
        GraphLocalization.Get(PropertiesLanguage.Properties).ShouldBe("Properties");
    }

    [Fact]
    public void Register_PreservesHostPluginAndHostOverridesAfterLanguageSwitches()
    {
        var directory = Directory.CreateTempSubdirectory("kudo-host-i18n-");
        try
        {
            File.WriteAllText(Path.Combine(directory.FullName, "host.en-US.json"), """
                {
                  "language": "English", "description": "Host English", "cultureName": "en-US",
                  "Localization": {
                    "Host": { "Title": "Host title" },
                    "Kudo_Avalonia": { "Designer": { "GraphPropertiesView": { "Properties": "Host properties" } } }
                  }
                }
                """);
            File.WriteAllText(Path.Combine(directory.FullName, "host.zh-CN.json"), """
                {
                  "language": "简体中文", "description": "宿主简体中文", "cultureName": "zh-CN",
                  "Localization": {
                    "Host": { "Title": "宿主标题" },
                    "Kudo_Avalonia": { "Designer": { "GraphPropertiesView": { "Properties": "宿主属性" } } }
                  }
                }
                """);
            var manager = I18nManager.Instance;
            manager.Register(new JsonLangPlugin { ResourceFolder = directory.FullName },
                CultureInfo.GetCultureInfo(GraphLocalization.DefaultCultureName));
            GraphLocalization.Register(CultureInfo.GetCultureInfo(GraphLocalization.DefaultCultureName));

            manager.GetResource("Localization.Host.Title").ShouldBe("Host title");
            GraphLocalization.Get(PropertiesLanguage.Properties).ShouldBe("Host properties");
            manager.GetResource(PropertiesLanguage.Properties).ShouldBe("Host properties");
            GraphLocalization.Get(PropertiesLanguage.NoActiveDocument).ShouldBe("No active canvas.");

            manager.Culture = CultureInfo.GetCultureInfo(GraphLocalization.SimplifiedChineseCultureName);
            manager.GetResource("Localization.Host.Title").ShouldBe("宿主标题");
            GraphLocalization.Get(PropertiesLanguage.Properties).ShouldBe("宿主属性");

            manager.Culture = CultureInfo.GetCultureInfo(GraphLocalization.DefaultCultureName);
            GraphLocalization.Get(PropertiesLanguage.Properties).ShouldBe("Host properties");
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
