using System.Text.Json;
using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace Kudo.Avalonia.Tests.Localization;

/// <summary>
///     T4 同步回归：Language.cs（Language.tt 生成产物）与 JSON 语言资源的键必须一一对应。
///     手工改了 JSON 却忘记重跑 T4（或反之）时，此测试立即失败。
/// </summary>
public sealed class LanguageSyncTests
{
    private static string I18nRoot =>
        Path.Combine(RepoRoot(), "samples", "Kudo.Demo.Desktop", "I18n");

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Kudo.slnx")))
        {
            directory = directory.Parent;
        }

        directory.ShouldNotBeNull();
        return directory.FullName;
    }

    private static SortedSet<string> JsonKeyPaths(string fileName)
    {
        var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(I18nRoot, fileName)));
        var paths = new SortedSet<string>(StringComparer.Ordinal);
        Collect(document.RootElement.GetProperty("Localization"), "Localization", paths);
        return paths;
    }

    private static void Collect(JsonElement node, string prefix, SortedSet<string> paths)
    {
        foreach (var property in node.EnumerateObject())
        {
            var name = $"{prefix}.{property.Name}";
            if (property.Value.ValueKind == JsonValueKind.Object)
            {
                Collect(property.Value, name, paths);
            }
            else
            {
                paths.Add(name);
            }
        }
    }

    private static SortedSet<string> GeneratedKeys()
    {
        var source = File.ReadAllText(Path.Combine(I18nRoot, "Language.cs"));
        return new SortedSet<string>(
            Regex.Matches(source, "\"(Localization\\.[A-Za-z0-9_.]+)\"")
                .Select(match => match.Groups[1].Value)
                .Where(key => !key.EndsWith("Culture", StringComparison.Ordinal)),
            StringComparer.Ordinal);
    }

    [Fact]
    public void Generated_keys_cover_every_json_path()
    {
        var generated = GeneratedKeys();
        var jsonPaths = JsonKeyPaths("Kudo.Demo.zh-CN.json");

        jsonPaths.Where(path => !generated.Contains(path))
            .ShouldBeEmpty("JSON 中存在 Language.cs 未生成的键（请重跑 Language.tt）：");
    }

    [Fact]
    public void Generated_keys_never_reference_missing_json_paths()
    {
        var generated = GeneratedKeys();
        var zhPaths = JsonKeyPaths("Kudo.Demo.zh-CN.json");
        var enPaths = JsonKeyPaths("Kudo.Demo.en-US.json");

        generated.Where(key => !zhPaths.Contains(key) && !enPaths.Contains(key))
            .ShouldBeEmpty("Language.cs 引用了 JSON 中不存在的键：");
    }

    [Fact]
    public void Localization_values_are_never_empty()
    {
        foreach (var fileName in new[] { "Kudo.Demo.zh-CN.json", "Kudo.Demo.en-US.json" })
        {
            var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(I18nRoot, fileName)));
            var empty = document.RootElement.EnumerateObject()
                .SelectMany(property => EnumerateLeaves(property))
                .Where(leaf => string.IsNullOrWhiteSpace(leaf.Value))
                .Select(leaf => leaf.Path)
                .ToList();

            empty.ShouldBeEmpty($"{fileName} 存在空翻译：");
        }
    }

    private static IEnumerable<(string Path, string? Value)> EnumerateLeaves(JsonProperty property)
    {
        var name = property.Name;
        if (property.Value.ValueKind == JsonValueKind.Object)
        {
            foreach (var child in property.Value.EnumerateObject())
            {
                foreach (var leaf in EnumerateLeaves(child))
                {
                    yield return ($"{name}.{leaf.Path}", leaf.Value);
                }
            }
        }
        else
        {
            yield return (name, property.Value.ValueKind == JsonValueKind.String
                ? property.Value.GetString()
                : null);
        }
    }
}
