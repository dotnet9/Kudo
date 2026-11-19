using Kudo.Demo.Desktop.Services;
using Shouldly;
using Xunit;

namespace Kudo.Avalonia.Tests.Services;

/// <summary>示例任务库发现契约：数量、目录与回收站内容（防业务示例回流）。</summary>
public sealed class DemoSampleTaskLibraryTests
{
    private static string SamplesRoot =>
        Path.Combine(RepoRoot(), "samples", "Kudo.Demo.Desktop", "Samples");

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

    [Fact]
    public void Discovers_seven_sample_packages_from_repo_samples()
    {
        var library = new DemoSampleTaskLibrary(SamplesRoot);

        library.Tasks.Count.ShouldBe(7);
        library.Tasks.Select(t => t.Id).ShouldBeUnique();
        library.Tasks.Select(t => t.Title).ShouldAllBe(title => !string.IsNullOrEmpty(title));
    }

    [Fact]
    public void Task_directories_resolve_and_carry_package_files()
    {
        var library = new DemoSampleTaskLibrary(SamplesRoot);

        foreach (var task in library.Tasks)
        {
            var directory = library.GetTaskDirectory(task.Id);
            directory.ShouldBe(task.TaskDirectory);
            File.Exists(Path.Combine(directory, "Manifest.xml")).ShouldBeTrue($"{task.Id} 缺少 Manifest.xml");
            File.Exists(Path.Combine(directory, "Task.xml")).ShouldBeTrue($"{task.Id} 缺少 Task.xml");
        }
    }

    [Fact]
    public void Order_risk_control_sample_remains_available()
    {
        var library = new DemoSampleTaskLibrary(SamplesRoot);

        library.Tasks.ShouldContain(t => t.Id == DemoSampleTaskLibrary.DefaultStartupTaskId);
    }

    [Fact]
    public void Thermal_hydraulic_business_sample_stays_removed()
    {
        var library = new DemoSampleTaskLibrary(SamplesRoot);

        library.Tasks.ShouldNotContain(t =>
            t.Id.Contains("Thermal", StringComparison.OrdinalIgnoreCase) ||
            t.Id.Contains("thermal", StringComparison.Ordinal) ||
            t.Title.Contains("热工"));
    }

    [Fact]
    public void Unknown_task_id_throws_argument_out_of_range()
    {
        var library = new DemoSampleTaskLibrary(SamplesRoot);

        Should.Throw<ArgumentOutOfRangeException>(() => library.GetTaskDirectory("no-such-sample"));
    }
}
