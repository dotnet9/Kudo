using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Kudo.Avalonia;

namespace Kudo.Benchmarks;

public enum GraphRenderBenchmarkScene
{
    Immediate,
    EmbeddedControls
}

public sealed record GraphRenderBenchmarkCase(
    string Name,
    GraphRenderBenchmarkScene Scene,
    int FrameCount,
    Action Setup,
    Func<long> RenderFrame)
{
    public bool IsContinuous => FrameCount > 1;
}

public sealed class GraphRenderBenchmarkSuite : IDisposable
{
    public const int DefaultContinuousFrameCount = 60;

    private const string EmbeddedNodeType = "input";
    private readonly GraphBenchmarkDocumentFactory _factory;
    private readonly GraphBenchmarkScenario _scenario;
    private readonly int _continuousFrameCount;
    private RenderState? _state;
    private GraphRenderOperation _operation;
    private int _frameIndex;
    private bool _disposed;

    public GraphRenderBenchmarkSuite(
        GraphBenchmarkDocumentFactory factory,
        GraphBenchmarkScenario scenario,
        int continuousFrameCount = DefaultContinuousFrameCount)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _scenario = scenario ?? throw new ArgumentNullException(nameof(scenario));
        if (continuousFrameCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(continuousFrameCount),
                "连续 Render 基准的帧数必须大于 0。");
        }

        _continuousFrameCount = continuousFrameCount;
        GraphBenchmarkAvaloniaHost.EnsureInitialized();
        Cases = CreateCases();
    }

    public IReadOnlyList<GraphRenderBenchmarkCase> Cases { get; }

    public int ContinuousFrameCount => _continuousFrameCount;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        DisposeState();
    }

    private IReadOnlyList<GraphRenderBenchmarkCase> CreateCases()
    {
        var cases = new List<GraphRenderBenchmarkCase>(6);
        foreach (var scene in Enum.GetValues<GraphRenderBenchmarkScene>())
        {
            var sceneName = scene == GraphRenderBenchmarkScene.Immediate
                ? "纯即时绘制"
                : "1/3 input 节点嵌入 Control";
            cases.Add(new GraphRenderBenchmarkCase(
                $"首次 Render（{sceneName}）",
                scene,
                1,
                () => Prepare(scene, GraphRenderOperation.FirstRender),
                RenderFirstFrame));
            cases.Add(new GraphRenderBenchmarkCase(
                $"连续平移 {_continuousFrameCount} 帧（{sceneName}）",
                scene,
                _continuousFrameCount,
                () => Prepare(scene, GraphRenderOperation.Pan),
                RenderPanFrame));
            cases.Add(new GraphRenderBenchmarkCase(
                $"连续 Zoom {_continuousFrameCount} 帧（{sceneName}）",
                scene,
                _continuousFrameCount,
                () => Prepare(scene, GraphRenderOperation.Zoom),
                RenderZoomFrame));
        }

        return cases.AsReadOnly();
    }

    private void Prepare(GraphRenderBenchmarkScene scene, GraphRenderOperation operation)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        DisposeState();

        var document = _factory.Create(_scenario);
        var canvas = new BenchmarkGraphCanvas();
        var embeddedControlRenderCounter = new RenderCounter();
        var embeddedControlCount = 0;
        if (scene == GraphRenderBenchmarkScene.EmbeddedControls)
        {
            embeddedControlCount = document.Nodes.Count(node =>
                string.Equals(node.Type, EmbeddedNodeType, StringComparison.OrdinalIgnoreCase));
            canvas.NodeControls.Register(
                EmbeddedNodeType,
                context => CreateEmbeddedNodeControl(context, embeddedControlRenderCounter));
        }

        canvas.Document = document;
        var window = GraphBenchmarkAvaloniaHost.CreateWindow(canvas);
        _state = new RenderState(
            window,
            canvas,
            document,
            scene,
            embeddedControlCount,
            embeddedControlRenderCounter);
        _operation = operation;
        _frameIndex = 0;

        if (operation != GraphRenderOperation.FirstRender)
        {
            window.Show();
            RenderAndVerify(_state);
        }
    }

    private long RenderFirstFrame()
    {
        var state = GetState(GraphRenderOperation.FirstRender);
        if (state.Window.IsVisible)
        {
            throw new InvalidOperationException("首次 Render 场景在 Setup 后已意外显示窗口。");
        }

        state.Window.Show();
        RenderAndVerify(state);
        return CalculateChecksum(state);
    }

    private long RenderPanFrame()
    {
        var state = GetState(GraphRenderOperation.Pan);
        var angle = NextSequenceAngle();
        state.Canvas.Viewport = state.Canvas.Viewport with
        {
            Offset = new GraphPoint(
                Math.Sin(angle) * 320,
                Math.Sin(angle * 0.5) * 180)
        };
        RenderAndVerify(state);
        return CalculateChecksum(state);
    }

    private long RenderZoomFrame()
    {
        var state = GetState(GraphRenderOperation.Zoom);
        var angle = NextSequenceAngle();
        state.Canvas.Viewport = state.Canvas.Viewport with
        {
            Zoom = 1 + Math.Sin(angle) * 0.35
        };
        RenderAndVerify(state);
        return CalculateChecksum(state);
    }

    private double NextSequenceAngle()
    {
        var frame = _frameIndex++;
        return (frame + 1) * Math.Tau / _continuousFrameCount;
    }

    private RenderState GetState(GraphRenderOperation expectedOperation)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_state is null || _operation != expectedOperation)
        {
            throw new InvalidOperationException("请先调用当前 Render Case 的 Setup。");
        }

        return _state;
    }

    private static Control CreateEmbeddedNodeControl(
        GraphNodeControlContext context,
        RenderCounter renderCounter) =>
        new BenchmarkEmbeddedNodeControl(renderCounter)
        {
            Child = new Border
            {
                Background = Brushes.WhiteSmoke,
                BorderBrush = Brushes.SlateGray,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8),
                Child = new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(context.Node.Name)
                        ? string.IsNullOrWhiteSpace(context.Node.Type) ? context.Node.Id : context.Node.Type
                        : context.Node.Name,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Center
                }
            }
        };

    private static void RenderAndVerify(RenderState state)
    {
        var renderCount = state.Canvas.RenderCount;
        GraphBenchmarkAvaloniaHost.RenderFrame();
        if (state.Canvas.RenderCount <= renderCount)
        {
            throw new InvalidOperationException("Headless 帧未触发 GraphCanvas.Render。");
        }

        if (state.Scene == GraphRenderBenchmarkScene.EmbeddedControls &&
            state.EmbeddedControlRenderCounter.Count == 0)
        {
            throw new InvalidOperationException("Headless 帧未触发节点内嵌 Control.Render。");
        }

        if (state.Canvas.Bounds.Width != GraphBenchmarkAvaloniaHost.ViewportWidth ||
            state.Canvas.Bounds.Height != GraphBenchmarkAvaloniaHost.ViewportHeight)
        {
            throw new InvalidOperationException(
                $"Render 视口尺寸异常：{state.Canvas.Bounds.Width}x{state.Canvas.Bounds.Height}。");
        }
    }

    private static long CalculateChecksum(RenderState state)
    {
        var viewport = state.Canvas.Viewport;
        var offset = viewport.Offset;
        return state.Canvas.RenderCount * 1_000_003L +
               state.Document.Nodes.Count * 10_007L +
               state.Document.Edges.Count * 101L +
               state.EmbeddedControlCount * 17L +
               state.EmbeddedControlRenderCounter.Count * 13L +
               (long)Math.Round(offset.X * 10) +
               (long)Math.Round(offset.Y * 10) +
               (long)Math.Round(viewport.Zoom * 1_000);
    }

    private void DisposeState()
    {
        if (_state is null)
        {
            return;
        }

        if (_state.Window.IsVisible)
        {
            _state.Window.Close();
        }
        else
        {
            _state.Window.Content = null;
        }

        _state = null;
        GraphBenchmarkAvaloniaHost.DrainJobs();
    }

    private enum GraphRenderOperation
    {
        FirstRender,
        Pan,
        Zoom
    }

    private sealed record RenderState(
        Window Window,
        BenchmarkGraphCanvas Canvas,
        GraphDocument Document,
        GraphRenderBenchmarkScene Scene,
        int EmbeddedControlCount,
        RenderCounter EmbeddedControlRenderCounter);

    private sealed class RenderCounter
    {
        public int Count { get; private set; }

        public void Increment() => Count++;
    }

    private sealed class BenchmarkEmbeddedNodeControl(RenderCounter renderCounter) : Decorator
    {
        public override void Render(DrawingContext context)
        {
            base.Render(context);
            renderCounter.Increment();
        }
    }

    private sealed class BenchmarkGraphCanvas : GraphCanvas
    {
        public int RenderCount { get; private set; }

        public override void Render(DrawingContext context)
        {
            base.Render(context);
            RenderCount++;
        }
    }
}
