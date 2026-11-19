using Avalonia;
using Avalonia.Input;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Threading;
using Silk.NET.OpenGL;
using System.Diagnostics;
using System.Numerics;

namespace Kudo.Avalonia.ThreeD;

public sealed class GraphThreeDOpenGlFailureEventArgs(Exception exception) : EventArgs
{
    public Exception Exception { get; } = exception;
}

public sealed class GraphThreeDOpenGlControl : OpenGlControlBase
{
    public static readonly StyledProperty<GraphThreeDScene> SceneProperty =
        AvaloniaProperty.Register<GraphThreeDOpenGlControl, GraphThreeDScene>(
            nameof(Scene),
            GraphThreeDScene.Default);

    public static readonly StyledProperty<bool> IsAnimationEnabledProperty =
        AvaloniaProperty.Register<GraphThreeDOpenGlControl, bool>(
            nameof(IsAnimationEnabled),
            true);

    private readonly DispatcherTimer _animationTimer;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private GraphThreeDOpenGlScene? _renderer;
    private GL? _gl;
    private bool _isAttachedToVisualTree;
    private bool _isCameraDragging;
    private Point _cameraDragStartPoint;
    private double _cameraStartPitch;
    private double _cameraStartYaw;
    private double _lastFrameTime;
    private double _rotation;

    public GraphThreeDOpenGlControl()
    {
        ClipToBounds = true;
        Focusable = true;
        _animationTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(1000.0 / 30)
        };
        _animationTimer.Tick += HandleAnimationTick;
    }

    public event EventHandler<GraphThreeDOpenGlFailureEventArgs>? InitializationFailed;

    public event EventHandler? OpenGlInitialized;

    public GraphThreeDScene Scene
    {
        get => GetValue(SceneProperty);
        set => SetValue(SceneProperty, value ?? GraphThreeDScene.Default);
    }

    public bool IsAnimationEnabled
    {
        get => GetValue(IsAnimationEnabledProperty);
        set => SetValue(IsAnimationEnabledProperty, value);
    }

    public Exception? InitializationException { get; private set; }

    public bool IsOpenGlInitialized => _renderer is not null;

    public bool IsAnimationActive => _animationTimer.IsEnabled;

    public void RotateCamera(double yawDelta, double pitchDelta)
    {
        var scene = (Scene ?? GraphThreeDScene.Default).Normalize();
        Scene = (scene with
        {
            Yaw = scene.Yaw + yawDelta,
            Pitch = scene.Pitch + pitchDelta,
            AutoRotate = false
        }).Normalize();
    }

    public void AdjustZoom(double factor)
    {
        if (!double.IsFinite(factor) || factor <= 0)
        {
            return;
        }

        var scene = (Scene ?? GraphThreeDScene.Default).Normalize();
        Scene = (scene with
        {
            Zoom = scene.Zoom * factor,
            AutoRotate = false
        }).Normalize();
    }

    public void ResetCamera()
    {
        var scene = (Scene ?? GraphThreeDScene.Default).Normalize();
        Scene = (scene with
        {
            Zoom = 1.0,
            Pitch = 24,
            Yaw = 34,
            AutoRotate = false
        }).Normalize();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _isAttachedToVisualTree = true;
        UpdateAnimationState();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _isAttachedToVisualTree = false;
        _animationTimer.Stop();
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SceneProperty)
        {
            UpdateAnimationState();
            RequestNextFrameRendering();
        }
        else if (change.Property == IsAnimationEnabledProperty)
        {
            UpdateAnimationState();
            if (IsAnimationEnabled)
            {
                RequestNextFrameRendering();
            }
        }
        else if (change.Property == BoundsProperty)
        {
            RequestNextFrameRendering();
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var scene = (Scene ?? GraphThreeDScene.Default).Normalize();
        Focus();
        _isCameraDragging = true;
        _cameraDragStartPoint = e.GetPosition(this);
        _cameraStartPitch = scene.Pitch;
        _cameraStartYaw = scene.Yaw;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_isCameraDragging)
        {
            return;
        }

        var delta = e.GetPosition(this) - _cameraDragStartPoint;
        var scene = (Scene ?? GraphThreeDScene.Default).Normalize();
        Scene = (scene with
        {
            Yaw = _cameraStartYaw + delta.X * 0.45,
            Pitch = _cameraStartPitch - delta.Y * 0.35,
            AutoRotate = false
        }).Normalize();
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_isCameraDragging)
        {
            return;
        }

        _isCameraDragging = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _isCameraDragging = false;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        AdjustZoom(e.Delta.Y > 0 ? 1.1 : 1.0 / 1.1);
        e.Handled = true;
    }

    protected override void OnOpenGlInit(GlInterface gl)
    {
        base.OnOpenGlInit(gl);
        try
        {
            InitializationException = null;
            _gl = GL.GetApi(gl.GetProcAddress);
            _renderer = new GraphThreeDOpenGlScene(_gl);
            _lastFrameTime = _stopwatch.Elapsed.TotalSeconds;
            UpdateAnimationState();
            Dispatcher.UIThread.Post(() => OpenGlInitialized?.Invoke(this, EventArgs.Empty));
        }
        catch (Exception exception)
        {
            _renderer?.Dispose();
            _renderer = null;
            _gl?.Dispose();
            _gl = null;
            InitializationException = exception;
            Dispatcher.UIThread.Post(() =>
                InitializationFailed?.Invoke(this, new GraphThreeDOpenGlFailureEventArgs(exception)));
        }
    }

    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        _animationTimer.Stop();
        _renderer?.Dispose();
        _renderer = null;
        _gl?.Dispose();
        _gl = null;
        base.OnOpenGlDeinit(gl);
    }

    protected override void OnOpenGlRender(GlInterface gl, int fb)
    {
        if (_renderer is null)
        {
            return;
        }

        var currentTime = _stopwatch.Elapsed.TotalSeconds;
        var deltaTime = Math.Min(0.1, Math.Max(0, currentTime - _lastFrameTime));
        _lastFrameTime = currentTime;
        var scene = (Scene ?? GraphThreeDScene.Default).Normalize();
        if (scene.AutoRotate)
        {
            _rotation = (_rotation + deltaTime * 28) % 360;
        }

        _renderer.Render(
            scene,
            Math.Max(1, (int)Bounds.Width),
            Math.Max(1, (int)Bounds.Height),
            _rotation);
    }

    private void HandleAnimationTick(object? sender, EventArgs e) => RequestNextFrameRendering();

    private void UpdateAnimationState()
    {
        if (_isAttachedToVisualTree &&
            IsAnimationEnabled &&
            (Scene ?? GraphThreeDScene.Default).Normalize().AutoRotate)
        {
            _animationTimer.Start();
        }
        else
        {
            _animationTimer.Stop();
        }
    }
}

internal sealed class GraphThreeDOpenGlScene : IDisposable
{
    private const string VertexShader = """
        #version 100
        precision mediump float;
        attribute vec3 aPosition;
        attribute vec3 aNormal;
        varying vec3 vFragPos;
        varying vec3 vNormal;
        uniform mat4 uModel;
        uniform mat4 uView;
        uniform mat4 uProjection;
        void main()
        {
            vec4 worldPos = uModel * vec4(aPosition, 1.0);
            vFragPos = worldPos.xyz;
            vNormal = normalize(mat3(uModel) * aNormal);
            gl_Position = uProjection * uView * worldPos;
        }
        """;

    private const string FragmentShader = """
        #version 100
        precision mediump float;
        varying vec3 vFragPos;
        varying vec3 vNormal;
        uniform vec4 uColor;
        uniform vec3 uLightPos;
        uniform vec3 uViewPos;
        void main()
        {
            vec3 normal = normalize(vNormal);
            vec3 lightDirection = normalize(uLightPos - vFragPos);
            float diffuse = max(dot(normal, lightDirection), 0.0);
            vec3 color = uColor.rgb * (0.36 + diffuse * 0.64);
            gl_FragColor = vec4(color, uColor.a);
        }
        """;

    private readonly GL _gl;
    private readonly GraphThreeDShaderProgram _shader;
    private readonly GraphThreeDMesh _cube;
    private readonly GraphThreeDMesh _pyramid;
    private readonly GraphThreeDLineMesh _line;
    private bool _disposed;

    public GraphThreeDOpenGlScene(GL gl)
    {
        _gl = gl;
        _shader = GraphThreeDShaderProgram.Create(gl, VertexShader, FragmentShader);
        _cube = GraphThreeDMesh.CreateCube(gl);
        _pyramid = GraphThreeDMesh.CreatePyramid(gl);
        _line = new GraphThreeDLineMesh(gl);
        gl.Enable(EnableCap.DepthTest);
        gl.DepthFunc(DepthFunction.Less);
        gl.Enable(EnableCap.Blend);
        gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        gl.Enable(EnableCap.CullFace);
        gl.CullFace(TriangleFace.Back);
    }

    public void Render(GraphThreeDScene scene, int width, int height, double rotation)
    {
        _gl.Viewport(0, 0, (uint)width, (uint)height);
        _gl.ClearColor(0.972f, 0.98f, 0.988f, 1f);
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        var yaw = scene.Yaw + (scene.AutoRotate ? rotation : 0);
        var cameraPosition = CreateCameraPosition(scene.Pitch, yaw, 8.5f / (float)scene.Zoom);
        var view = Matrix4x4.CreateLookAt(cameraPosition, Vector3.Zero, Vector3.UnitY);
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(
            MathF.PI / 4,
            width / (float)Math.Max(1, height),
            0.1f,
            100f);

        _shader.Use();
        _shader.SetMatrix("uView", view);
        _shader.SetMatrix("uProjection", projection);
        _shader.SetVector3("uViewPos", cameraPosition);
        _shader.SetVector3("uLightPos", new Vector3(4.5f, 7f, 5f));

        if (scene.Nodes.Count > 0)
        {
            RenderGraph(scene);
        }
        else
        {
            RenderFallbackShapes(scene);
        }
    }

    private void RenderGraph(GraphThreeDScene scene)
    {
        var nodesById = scene.Nodes
            .GroupBy(node => node.Id, StringComparer.Ordinal)
            .Select(group => group.Last())
            .ToDictionary(node => node.Id, StringComparer.Ordinal);

        _gl.Disable(EnableCap.CullFace);
        _gl.LineWidth(2.2f);
        foreach (var edge in scene.Edges)
        {
            if (!nodesById.TryGetValue(edge.SourceNodeId, out var source) ||
                !nodesById.TryGetValue(edge.TargetNodeId, out var target))
            {
                continue;
            }

            _shader.SetMatrix("uModel", Matrix4x4.Identity);
            _shader.SetVector4(
                "uColor",
                edge.IsSelected ? new Vector4(0.96f, 0.45f, 0.08f, 1) : new Vector4(0.36f, 0.44f, 0.55f, 0.9f));
            _line.Draw(ToVector(source.Position), ToVector(target.Position));
        }

        _gl.Enable(EnableCap.CullFace);
        foreach (var node in scene.Nodes)
        {
            var size = node.Size.Normalize();
            var scale = Matrix4x4.CreateScale(
                (float)size.Width,
                (float)Math.Max(0.18, size.Depth),
                (float)size.Height);
            var translation = Matrix4x4.CreateTranslation(ToVector(node.Position));
            var model = scale * translation;
            _shader.SetMatrix("uModel", model);
            _shader.SetVector4("uColor", ToVector(node.Color, node.IsSelected));
            var mesh = (node.ShapeKind ?? scene.ShapeKind) == GraphThreeDShapeKind.Pyramid
                ? _pyramid
                : _cube;
            mesh.Draw();
        }
    }

    private void RenderFallbackShapes(GraphThreeDScene scene)
    {
        var mesh = scene.ShapeKind == GraphThreeDShapeKind.Pyramid ? _pyramid : _cube;
        var count = Math.Clamp(scene.ShapeCount, 1, 9);
        for (var index = 0; index < count; index++)
        {
            var row = index / 3;
            var column = index % 3;
            var position = count == 1
                ? Vector3.Zero
                : new Vector3((column - 1) * 1.45f, 0, (row - 1) * 1.45f);
            var scale = count == 1 ? 1.4f : 0.75f;
            var model = Matrix4x4.CreateScale(scale) * Matrix4x4.CreateTranslation(position);
            _shader.SetMatrix("uModel", model);
            _shader.SetVector4("uColor", ToVector(scene.Color, false));
            mesh.Draw();
        }
    }

    private static Vector3 CreateCameraPosition(double pitch, double yaw, float distance)
    {
        var pitchRadians = MathF.PI * (float)pitch / 180f;
        var yawRadians = MathF.PI * (float)yaw / 180f;
        return new Vector3(
            MathF.Cos(pitchRadians) * MathF.Cos(yawRadians),
            MathF.Sin(pitchRadians),
            MathF.Cos(pitchRadians) * MathF.Sin(yawRadians)) * distance;
    }

    private static Vector3 ToVector(GraphThreeDPoint point) =>
        new((float)point.X, (float)point.Y, (float)point.Z);

    private static Vector4 ToVector(GraphThreeDColor color, bool selected)
    {
        var factor = selected ? 1.22f : 1f;
        return new Vector4(
            Math.Clamp(color.R / 255f * factor, 0, 1),
            Math.Clamp(color.G / 255f * factor, 0, 1),
            Math.Clamp(color.B / 255f * factor, 0, 1),
            color.A / 255f);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _line.Dispose();
        _pyramid.Dispose();
        _cube.Dispose();
        _shader.Dispose();
    }
}

internal sealed class GraphThreeDShaderProgram : IDisposable
{
    private readonly GL _gl;
    private readonly uint _handle;
    private readonly Dictionary<string, int> _uniforms = new(StringComparer.Ordinal);
    private bool _disposed;

    private GraphThreeDShaderProgram(GL gl, uint handle)
    {
        _gl = gl;
        _handle = handle;
    }

    public static GraphThreeDShaderProgram Create(GL gl, string vertexSource, string fragmentSource)
    {
        var vertex = Compile(gl, ShaderType.VertexShader, vertexSource);
        var fragment = Compile(gl, ShaderType.FragmentShader, fragmentSource);
        var program = gl.CreateProgram();
        gl.AttachShader(program, vertex);
        gl.AttachShader(program, fragment);
        gl.BindAttribLocation(program, 0, "aPosition");
        gl.BindAttribLocation(program, 1, "aNormal");
        gl.LinkProgram(program);
        gl.GetProgram(program, GLEnum.LinkStatus, out var status);
        if (status == 0)
        {
            var log = gl.GetProgramInfoLog(program);
            gl.DeleteProgram(program);
            gl.DeleteShader(vertex);
            gl.DeleteShader(fragment);
            throw new InvalidOperationException($"OpenGL shader link failed: {log}");
        }

        gl.DetachShader(program, vertex);
        gl.DetachShader(program, fragment);
        gl.DeleteShader(vertex);
        gl.DeleteShader(fragment);
        return new GraphThreeDShaderProgram(gl, program);
    }

    public void Use() => _gl.UseProgram(_handle);

    public unsafe void SetMatrix(string name, Matrix4x4 value)
    {
        var location = GetUniformLocation(name);
        if (location < 0)
        {
            return;
        }

        var matrix = value;
        _gl.UniformMatrix4(location, 1, false, (float*)&matrix);
    }

    public void SetVector3(string name, Vector3 value)
    {
        var location = GetUniformLocation(name);
        if (location >= 0)
        {
            _gl.Uniform3(location, value.X, value.Y, value.Z);
        }
    }

    public void SetVector4(string name, Vector4 value)
    {
        var location = GetUniformLocation(name);
        if (location >= 0)
        {
            _gl.Uniform4(location, value.X, value.Y, value.Z, value.W);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _gl.DeleteProgram(_handle);
    }

    private int GetUniformLocation(string name)
    {
        if (_uniforms.TryGetValue(name, out var location))
        {
            return location;
        }

        location = _gl.GetUniformLocation(_handle, name);
        _uniforms.Add(name, location);
        return location;
    }

    private static uint Compile(GL gl, ShaderType shaderType, string source)
    {
        var shader = gl.CreateShader(shaderType);
        gl.ShaderSource(shader, source);
        gl.CompileShader(shader);
        gl.GetShader(shader, GLEnum.CompileStatus, out var status);
        if (status != 0)
        {
            return shader;
        }

        var log = gl.GetShaderInfoLog(shader);
        gl.DeleteShader(shader);
        throw new InvalidOperationException($"OpenGL {shaderType} compilation failed: {log}");
    }
}

internal sealed class GraphThreeDMesh : IDisposable
{
    private readonly GL _gl;
    private readonly uint _vertexArray;
    private readonly uint _vertexBuffer;
    private readonly uint _indexBuffer;
    private readonly uint _indexCount;
    private bool _disposed;

    private unsafe GraphThreeDMesh(GL gl, ReadOnlySpan<float> vertices, ReadOnlySpan<uint> indices)
    {
        _gl = gl;
        _indexCount = (uint)indices.Length;
        _vertexArray = gl.GenVertexArray();
        _vertexBuffer = gl.GenBuffer();
        _indexBuffer = gl.GenBuffer();
        gl.BindVertexArray(_vertexArray);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vertexBuffer);
        fixed (float* vertexPointer = vertices)
        {
            gl.BufferData(
                BufferTargetARB.ArrayBuffer,
                (nuint)(vertices.Length * sizeof(float)),
                vertexPointer,
                BufferUsageARB.StaticDraw);
        }

        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _indexBuffer);
        fixed (uint* indexPointer = indices)
        {
            gl.BufferData(
                BufferTargetARB.ElementArrayBuffer,
                (nuint)(indices.Length * sizeof(uint)),
                indexPointer,
                BufferUsageARB.StaticDraw);
        }

        const uint stride = 8 * sizeof(float);
        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, null);
        gl.EnableVertexAttribArray(1);
        gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, (void*)(3 * sizeof(float)));
        gl.BindVertexArray(0);
    }

    public static GraphThreeDMesh CreateCube(GL gl) => new(gl, CubeVertices, CubeIndices);

    public static GraphThreeDMesh CreatePyramid(GL gl) => new(gl, PyramidVertices, PyramidIndices);

    public unsafe void Draw()
    {
        _gl.BindVertexArray(_vertexArray);
        _gl.DrawElements(PrimitiveType.Triangles, _indexCount, DrawElementsType.UnsignedInt, null);
        _gl.BindVertexArray(0);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _gl.DeleteBuffer(_indexBuffer);
        _gl.DeleteBuffer(_vertexBuffer);
        _gl.DeleteVertexArray(_vertexArray);
    }

    private static readonly float[] CubeVertices =
    [
        -0.5f, -0.5f,  0.5f,  0,  0,  1, 0, 0,
         0.5f, -0.5f,  0.5f,  0,  0,  1, 1, 0,
         0.5f,  0.5f,  0.5f,  0,  0,  1, 1, 1,
        -0.5f,  0.5f,  0.5f,  0,  0,  1, 0, 1,
         0.5f, -0.5f, -0.5f,  0,  0, -1, 0, 0,
        -0.5f, -0.5f, -0.5f,  0,  0, -1, 1, 0,
        -0.5f,  0.5f, -0.5f,  0,  0, -1, 1, 1,
         0.5f,  0.5f, -0.5f,  0,  0, -1, 0, 1,
        -0.5f,  0.5f,  0.5f,  0,  1,  0, 0, 0,
         0.5f,  0.5f,  0.5f,  0,  1,  0, 1, 0,
         0.5f,  0.5f, -0.5f,  0,  1,  0, 1, 1,
        -0.5f,  0.5f, -0.5f,  0,  1,  0, 0, 1,
        -0.5f, -0.5f, -0.5f,  0, -1,  0, 0, 0,
         0.5f, -0.5f, -0.5f,  0, -1,  0, 1, 0,
         0.5f, -0.5f,  0.5f,  0, -1,  0, 1, 1,
        -0.5f, -0.5f,  0.5f,  0, -1,  0, 0, 1,
         0.5f, -0.5f,  0.5f,  1,  0,  0, 0, 0,
         0.5f, -0.5f, -0.5f,  1,  0,  0, 1, 0,
         0.5f,  0.5f, -0.5f,  1,  0,  0, 1, 1,
         0.5f,  0.5f,  0.5f,  1,  0,  0, 0, 1,
        -0.5f, -0.5f, -0.5f, -1,  0,  0, 0, 0,
        -0.5f, -0.5f,  0.5f, -1,  0,  0, 1, 0,
        -0.5f,  0.5f,  0.5f, -1,  0,  0, 1, 1,
        -0.5f,  0.5f, -0.5f, -1,  0,  0, 0, 1
    ];

    private static readonly uint[] CubeIndices =
    [
        0, 1, 2, 2, 3, 0, 4, 5, 6, 6, 7, 4,
        8, 9, 10, 10, 11, 8, 12, 13, 14, 14, 15, 12,
        16, 17, 18, 18, 19, 16, 20, 21, 22, 22, 23, 20
    ];

    private static readonly float[] PyramidVertices =
    [
        -0.5f, -0.5f,  0.5f,  0, 0.45f,  0.9f, 0, 0,
         0.5f, -0.5f,  0.5f,  0, 0.45f,  0.9f, 1, 0,
         0,     0.5f,  0,     0, 0.45f,  0.9f, 0.5f, 1,
         0.5f, -0.5f,  0.5f,  0.9f, 0.45f, 0, 0, 0,
         0.5f, -0.5f, -0.5f,  0.9f, 0.45f, 0, 1, 0,
         0,     0.5f,  0,      0.9f, 0.45f, 0, 0.5f, 1,
         0.5f, -0.5f, -0.5f,  0, 0.45f, -0.9f, 0, 0,
        -0.5f, -0.5f, -0.5f,  0, 0.45f, -0.9f, 1, 0,
         0,     0.5f,  0,      0, 0.45f, -0.9f, 0.5f, 1,
        -0.5f, -0.5f, -0.5f, -0.9f, 0.45f, 0, 0, 0,
        -0.5f, -0.5f,  0.5f, -0.9f, 0.45f, 0, 1, 0,
         0,     0.5f,  0,     -0.9f, 0.45f, 0, 0.5f, 1,
        -0.5f, -0.5f, -0.5f,  0, -1, 0, 0, 0,
         0.5f, -0.5f, -0.5f,  0, -1, 0, 1, 0,
         0.5f, -0.5f,  0.5f,  0, -1, 0, 1, 1,
        -0.5f, -0.5f,  0.5f,  0, -1, 0, 0, 1
    ];

    private static readonly uint[] PyramidIndices =
    [
        0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 14, 15, 12
    ];
}

internal sealed class GraphThreeDLineMesh : IDisposable
{
    private readonly GL _gl;
    private readonly uint _vertexArray;
    private readonly uint _vertexBuffer;
    private bool _disposed;

    public unsafe GraphThreeDLineMesh(GL gl)
    {
        _gl = gl;
        _vertexArray = gl.GenVertexArray();
        _vertexBuffer = gl.GenBuffer();
        gl.BindVertexArray(_vertexArray);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vertexBuffer);
        gl.BufferData(BufferTargetARB.ArrayBuffer, 16 * sizeof(float), null, BufferUsageARB.DynamicDraw);
        const uint stride = 8 * sizeof(float);
        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, null);
        gl.EnableVertexAttribArray(1);
        gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, (void*)(3 * sizeof(float)));
        gl.BindVertexArray(0);
    }

    public unsafe void Draw(Vector3 start, Vector3 end)
    {
        Span<float> vertices =
        [
            start.X, start.Y, start.Z, 0, 1, 0, 0, 0,
            end.X, end.Y, end.Z, 0, 1, 0, 0, 0
        ];
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vertexBuffer);
        fixed (float* pointer = vertices)
        {
            _gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0, (nuint)(vertices.Length * sizeof(float)), pointer);
        }

        _gl.BindVertexArray(_vertexArray);
        _gl.DrawArrays(PrimitiveType.Lines, 0, 2);
        _gl.BindVertexArray(0);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _gl.DeleteBuffer(_vertexBuffer);
        _gl.DeleteVertexArray(_vertexArray);
    }
}
