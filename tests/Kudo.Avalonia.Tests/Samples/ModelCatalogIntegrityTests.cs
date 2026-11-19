using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Shouldly;
using Xunit;

namespace Kudo.Avalonia.Tests.Samples;

/// <summary>
///     桌面 Demo 模型库完整性回归：模型目录声明的符号/图片/语言键必须真实存在。
///     新增或移除符号、示例时，此测试可捕获悬空引用。
/// </summary>
public sealed class ModelCatalogIntegrityTests
{
    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Kudo.slnx")))
        {
            directory = directory.Parent;
        }

        directory.ShouldNotBeNull("无法从测试输出目录定位仓库根（Kudo.slnx）");
        return directory.FullName;
    }

    private static string ModelsRoot => Path.Combine(RepoRoot(), "samples", "Kudo.Demo.Desktop", "Models");

    private static string SamplesRoot => Path.Combine(RepoRoot(), "samples", "Kudo.Demo.Desktop", "Samples");

    private static string LanguageSource =>
        File.ReadAllText(Path.Combine(RepoRoot(), "samples", "Kudo.Demo.Desktop", "I18n", "Language.cs"));

    [Fact]
    public void Model_catalog_declared_paths_exist_on_disk()
    {
        var catalog = XDocument.Load(Path.Combine(ModelsRoot, "Model.xml"));
        var missing = new List<string>();

        foreach (var group in catalog.Descendants("Group"))
        {
            var icon = (string?)group.Attribute("Icon");
            if (!string.IsNullOrEmpty(icon) && !File.Exists(Path.Combine(ModelsRoot, icon)))
            {
                missing.Add(icon);
            }

            foreach (var symbol in group.Descendants("Symbol"))
            {
                var path = (string?)symbol.Attribute("Path");
                if (!string.IsNullOrEmpty(path) && !File.Exists(Path.Combine(ModelsRoot, path)))
                {
                    missing.Add(path);
                }
            }
        }

        missing.ShouldBeEmpty($"模型目录声明了不存在的资源：{string.Join(", ", missing)}");
    }

    [Fact]
    public void Model_catalog_keys_exist_in_language_source()
    {
        var catalog = XDocument.Load(Path.Combine(ModelsRoot, "Model.xml"));
        var language = LanguageSource;
        var orphan = catalog.Descendants()
            .Attributes()
            .Where(a => a.Name.LocalName.EndsWith("Key", StringComparison.Ordinal) &&
                        (a.Value.StartsWith("Localization.", StringComparison.Ordinal)))
            .Select(a => a.Value)
            .Distinct()
            .Where(key => !language.Contains(key, StringComparison.Ordinal))
            .ToList();

        orphan.ShouldBeEmpty($"模型目录引用了语言资源中不存在的 Key：{string.Join(", ", orphan)}");
    }

    [Fact]
    public void Every_sample_package_has_manifest_and_task()
    {
        var orphan = Directory.GetDirectories(SamplesRoot)
            .Where(directory => !File.Exists(Path.Combine(directory, "Manifest.xml")) ||
                                !File.Exists(Path.Combine(directory, "Task.xml")))
            .Select(Path.GetFileName)
            .ToList();

        orphan.ShouldBeEmpty($"示例包缺少 Manifest.xml / Task.xml：{string.Join(", ", orphan)}");
    }

    [Fact]
    public void Localization_language_files_stay_in_sync()
    {
        var root = Path.Combine(RepoRoot(), "samples", "Kudo.Demo.Desktop", "I18n");
        var zh = KeyPaths(JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "Kudo.Demo.zh-CN.json"))).RootElement);
        var en = KeyPaths(JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "Kudo.Demo.en-US.json"))).RootElement);

        zh.ShouldBe(en);
    }

    private static List<string> KeyPaths(JsonElement node, string prefix = "")
    {
        var paths = new List<string>();
        foreach (var property in node.EnumerateObject())
        {
            var name = string.IsNullOrEmpty(prefix) ? property.Name : $"{prefix}.{property.Name}";
            if (property.Value.ValueKind == JsonValueKind.Object)
            {
                paths.AddRange(KeyPaths(property.Value, name));
            }
            else
            {
                paths.Add(name);
            }
        }

        return paths;
    }

    [Fact]
    public void Symbol_files_resolve_icon_paths_and_localization_keys()
    {
        var catalog = XDocument.Load(Path.Combine(ModelsRoot, "Model.xml"));
        var language = LanguageSource;
        var missing = new List<string>();

        foreach (var group in catalog.Descendants("Group"))
        {
            foreach (var symbolRef in group.Descendants("Symbol"))
            {
                var path = (string?)symbolRef.Attribute("Path");
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                var fullPath = Path.Combine(ModelsRoot, path);
                if (!File.Exists(fullPath))
                {
                    continue; // 路径存在性由上一测试覆盖
                }

                var symbol = XDocument.Load(fullPath);
                var symbolRoot = symbol.Root.ShouldNotBeNull();

                foreach (var attribute in symbolRoot.Attributes())
                {
                    if (attribute.Name.LocalName is "Icon" or "Resource" &&
                        !File.Exists(Path.Combine(ModelsRoot, attribute.Value)))
                    {
                        missing.Add($"{path} :: {attribute.Name}={attribute.Value}");
                    }

                    if (attribute.Value.StartsWith("Localization.", StringComparison.Ordinal) &&
                        !language.Contains(attribute.Value, StringComparison.Ordinal))
                    {
                        missing.Add($"{path} :: {attribute.Name}={attribute.Value}");
                    }
                }

                foreach (var attribute in symbol.Descendants().Attributes()
                             .Where(a => a.Value.StartsWith("Localization.", StringComparison.Ordinal)))
                {
                    if (!language.Contains(attribute.Value, StringComparison.Ordinal))
                    {
                        missing.Add($"{path} :: {attribute.Name}={attribute.Value}");
                    }
                }
            }
        }

        missing.ShouldBeEmpty($"符号文件存在悬空资源/语言键：{string.Join("; ", missing.Take(8))}");
    }

    [Fact]
    public void Demo_model_node_types_match_symbol_files()
    {
        // DemoModelNodeTypes 常量 ↔ 符号文件 NodeType/TemplateKey 双向一致
        var constants = new List<(string Name, string NodeType)>();
        var constantsSource = File.ReadAllText(
            Path.Combine(RepoRoot(), "samples", "Kudo.Demo.Desktop", "Services", "DemoModelCatalog.cs"));
        foreach (Match match in Regex.Matches(constantsSource,
                     @"public const string (\w+) = ""([^""]+)"";"))
        {
            constants.Add((match.Groups[1].Value, match.Groups[2].Value));
        }

        var symbolFileMap = new Dictionary<string, string>();
        foreach (var file in Directory.GetFiles(Path.Combine(ModelsRoot, "Symbols"), "*.xml"))
        {
            var symbol = XDocument.Load(file);
            var nodeType = (string?)symbol.Root?.Attribute("NodeType");
            if (!string.IsNullOrEmpty(nodeType))
            {
                symbolFileMap[nodeType] = Path.GetFileName(file);
            }
        }

        foreach (var (name, nodeType) in constants)
        {
            if (name is "TextLabel" or "FlowArrow")
            {
                continue; // 仪表与标注符号由 Instrumentation 目录提供，允许独立演化
            }

            symbolFileMap.ShouldContainKey(nodeType, $"DemoModelNodeTypes.{name} 引用了缺失的符号 NodeType");
        }
    }

    [Fact]
    public void Sample_packages_declare_kudo_formats()
    {
        foreach (var directory in Directory.GetDirectories(SamplesRoot))
        {
            var manifest = XDocument.Load(Path.Combine(directory, "Manifest.xml"));
            ((string?)manifest.Root?.Attribute("Format")).ShouldBe("Kudo.Package",
                $"{Path.GetFileName(directory)} 的 Manifest.xml 格式不正确");

            var task = XDocument.Load(Path.Combine(directory, "Task.xml"));
            ((string?)task.Root?.Attribute("Format")).ShouldBe("Kudo.Task/1",
                $"{Path.GetFileName(directory)} 的 Task.xml 格式不正确");
        }
    }
}
