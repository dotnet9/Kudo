using Avalonia;
using Xunit;

namespace Kudo.Controls.Tests;

/// <summary>Headless 测试应用宿主。</summary>
public sealed class TestApp : Application
{
    public override void Initialize()
    {
    }
}

/// <summary>单集合串行执行（Headless 控件操作有 UI 线程亲和，禁止并行）。</summary>
[CollectionDefinition("KudoControls-headless", DisableParallelization = true)]
public sealed class HeadlessSessionCollection;
