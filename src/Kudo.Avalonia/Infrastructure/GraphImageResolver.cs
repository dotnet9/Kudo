using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;
using Svg.Skia;
using System.Diagnostics;
using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace Kudo.Avalonia;

public interface IGraphImageResolver
{
    IImage? Resolve(string source);
}

public interface IGraphAnimatedImageResolver : IGraphImageResolver
{
    bool IsAnimated(string source);
}

public sealed class GraphFileImageResolver : IGraphAnimatedImageResolver, IDisposable
{
    private const int MaximumThumbnailCacheEntries = 256;
    private static readonly object ThumbnailCacheGate = new();
    private static readonly Dictionary<string, ThumbnailCacheEntry> ThumbnailCache =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Func<string?> _rootDirectoryProvider;
    private readonly Dictionary<string, IImage> _images = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, bool> _animatedSources = new(StringComparer.OrdinalIgnoreCase);
    private bool _isDisposed;

    public GraphFileImageResolver(Func<string?> rootDirectoryProvider) =>
        _rootDirectoryProvider = rootDirectoryProvider ??
            throw new ArgumentNullException(nameof(rootDirectoryProvider));

    public IImage? Resolve(string source)
    {
        if (_isDisposed || string.IsNullOrWhiteSpace(source) || Path.IsPathRooted(source))
        {
            return null;
        }

        var rootValue = _rootDirectoryProvider();
        if (string.IsNullOrWhiteSpace(rootValue))
        {
            return null;
        }

        var path = ResolveSafePath(rootValue, source);
        if (path is null)
        {
            return null;
        }

        if (_images.TryGetValue(path, out var image))
        {
            return image;
        }

        if (TryLoadImage(path, out image, out var isAnimated))
        {
            _images.Add(path, image);
            _animatedSources[path] = isAnimated;

            return image;
        }

        return null;
    }

    public bool IsAnimated(string source)
    {
        if (_isDisposed || string.IsNullOrWhiteSpace(source) || Path.IsPathRooted(source))
        {
            return false;
        }

        var rootValue = _rootDirectoryProvider();
        if (string.IsNullOrWhiteSpace(rootValue))
        {
            return false;
        }

        var path = ResolveSafePath(rootValue, source);
        if (path is null)
        {
            return false;
        }

        if (!_animatedSources.TryGetValue(path, out var isAnimated))
        {
            isAnimated = IsAnimatedFile(path);
            _animatedSources[path] = isAnimated;
        }

        return isAnimated;
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        foreach (var image in _images.Values)
        {
            if (image is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        _images.Clear();
        _animatedSources.Clear();
        GC.SuppressFinalize(this);
    }

    internal static bool TryLoadThumbnail(string path, double maximumDimension, out IImage image)
    {
        image = null!;
        if (!double.IsFinite(maximumDimension) || maximumDimension <= 0)
        {
            return false;
        }

        try
        {
            var bytes = File.ReadAllBytes(path);
            if (!IsSvg(bytes, path))
            {
                image = CreateBitmap(bytes, allowSkiaFallback: true);
                return true;
            }

            var cacheKey = $"{Path.GetFullPath(path)}|{maximumDimension:R}";
            var lastWriteTimeUtc = File.GetLastWriteTimeUtc(path);
            byte[]? thumbnailBytes = null;
            lock (ThumbnailCacheGate)
            {
                if (ThumbnailCache.TryGetValue(cacheKey, out var cached) &&
                    cached.LastWriteTimeUtc == lastWriteTimeUtc &&
                    cached.SourceLength == bytes.LongLength)
                {
                    thumbnailBytes = cached.Bytes;
                }
            }

            if (thumbnailBytes is null)
            {
                thumbnailBytes = RasterizeSvg(bytes, maximumDimension);
                lock (ThumbnailCacheGate)
                {
                    if (ThumbnailCache.Count >= MaximumThumbnailCacheEntries &&
                        !ThumbnailCache.ContainsKey(cacheKey))
                    {
                        ThumbnailCache.Remove(ThumbnailCache.Keys.First());
                    }

                    ThumbnailCache[cacheKey] = new ThumbnailCacheEntry(
                        lastWriteTimeUtc,
                        bytes.LongLength,
                        thumbnailBytes);
                }
            }

            image = CreateBitmap(thumbnailBytes);
            return true;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidDataException)
        {
            return false;
        }
    }

    private static string? ResolveSafePath(string rootValue, string source)
    {
        var root = Path.GetFullPath(rootValue)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(
            root,
            source.Replace('/', Path.DirectorySeparatorChar)));
        return path.StartsWith(root, StringComparison.OrdinalIgnoreCase) && File.Exists(path)
            ? path
            : null;
    }

    private static bool TryLoadImage(
        string path,
        out IImage image,
        out bool isAnimated)
    {
        image = null!;
        isAnimated = false;
        try
        {
            var bytes = File.ReadAllBytes(path);
            var isSvg = IsSvg(bytes, path);
            if (isSvg)
            {
                isAnimated = IsAnimatedSvg(bytes);
                if (isAnimated)
                {
                    if (!TryCreateAnimatedSvg(bytes, out var animatedSvg))
                    {
                        return false;
                    }

                    image = animatedSvg;
                    return true;
                }

                if (!TryCreateSvg(bytes, out var svgImage))
                {
                    return false;
                }

                image = svgImage;
                return true;
            }

            if (TryCreateAnimatedBitmap(bytes, out var animatedImage))
            {
                image = animatedImage;
                isAnimated = true;
                return true;
            }

            image = CreateBitmap(bytes, allowSkiaFallback: true);
            return true;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidDataException)
        {
            return false;
        }
    }

    private static bool TryCreateSvg(
        byte[] bytes,
        out GraphSvgImage image)
    {
        image = null!;
        using var stream = new MemoryStream(bytes);
        var svg = new SKSvg();
        var picture = svg.Load(stream) ?? svg.Picture;
        if (picture is null)
        {
            svg.Dispose();
            return false;
        }

        var bounds = picture.CullRect;
        var viewport = ReadSvgViewportSize(bytes);
        var width = viewport?.Width ?? bounds.Width;
        var height = viewport?.Height ?? bounds.Height;
        if (width <= 0 || height <= 0)
        {
            svg.Dispose();
            return false;
        }

        image = new GraphSvgImage(svg, picture, new Size(width, height));
        return true;
    }

    private static bool TryCreateAnimatedSvg(
        byte[] bytes,
        out GraphAnimatedSvgImage image)
    {
        image = null!;
        try
        {
            using var stream = new MemoryStream(bytes);
            var svg = new SKSvg();
            var picture = svg.Load(stream) ?? svg.Picture;
            if (picture is null || !svg.HasAnimations)
            {
                svg.Dispose();
                return false;
            }

            var bounds = picture.CullRect;
            var viewport = ReadSvgViewportSize(bytes);
            var width = Math.Max(bounds.Width, viewport?.Width ?? 0);
            var height = Math.Max(bounds.Height, viewport?.Height ?? 0);
            if (width <= 0 || height <= 0)
            {
                svg.Dispose();
                return false;
            }

            image = new GraphAnimatedSvgImage(svg, new Size(width, height));
            return true;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidDataException)
        {
            return false;
        }
    }

    private static bool TryCreateAnimatedBitmap(
        byte[] bytes,
        out GraphAnimatedBitmapImage image)
    {
        image = null!;
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data);
        if (codec is null || codec.FrameCount <= 1)
        {
            return false;
        }

        var sourceInfo = codec.Info;
        if (sourceInfo.Width <= 0 || sourceInfo.Height <= 0)
        {
            return false;
        }

        var imageInfo = new SKImageInfo(
            sourceInfo.Width,
            sourceInfo.Height,
            SKColorType.Rgba8888,
            SKAlphaType.Premul);
        var frameInfo = codec.FrameInfo;
        var frames = new List<Bitmap>(codec.FrameCount);
        var skiaFrames = new List<SKBitmap>(codec.FrameCount);
        var durations = new List<TimeSpan>(codec.FrameCount);

        try
        {
            for (var i = 0; i < codec.FrameCount; i++)
            {
                var bitmap = new SKBitmap(imageInfo);
                var requiredFrame = i < frameInfo.Length ? frameInfo[i].RequiredFrame : -1;
                if (requiredFrame >= 0 && requiredFrame < skiaFrames.Count)
                {
                    using var canvas = new SKCanvas(bitmap);
                    canvas.DrawBitmap(
                        skiaFrames[requiredFrame],
                        0,
                        0,
                        new SKSamplingOptions(SKFilterMode.Linear));
                }

                var options = requiredFrame >= 0
                    ? new SKCodecOptions(i, requiredFrame)
                    : new SKCodecOptions(i);
                var result = codec.GetPixels(imageInfo, bitmap.GetPixels(), options);
                if (result is not SKCodecResult.Success and not SKCodecResult.IncompleteInput)
                {
                    bitmap.Dispose();
                    return false;
                }

                skiaFrames.Add(bitmap);
                frames.Add(CreateBitmap(EncodeBitmap(bitmap)));
                durations.Add(NormalizeFrameDuration(
                    i < frameInfo.Length ? frameInfo[i].Duration : 0));
            }

            if (frames.Count <= 1)
            {
                return false;
            }

            image = new GraphAnimatedBitmapImage(frames.ToArray(), durations.ToArray());
            frames.Clear();
            return true;
        }
        finally
        {
            foreach (var bitmap in skiaFrames)
            {
                bitmap.Dispose();
            }

            foreach (var frame in frames)
            {
                frame.Dispose();
            }
        }
    }

    private static TimeSpan NormalizeFrameDuration(int milliseconds) =>
        TimeSpan.FromMilliseconds(milliseconds <= 0 ? 100 : Math.Max(20, milliseconds));

    internal static byte[] RasterizeSvg(byte[] bytes, double? maximumDimension = null)
    {
        using var svg = new SKSvg();
        using var stream = new MemoryStream(bytes);
        var picture = svg.Load(stream) ?? svg.Picture;
        if (picture is null)
        {
            throw new InvalidDataException("SVG image could not be loaded.");
        }

        var bounds = picture.CullRect;
        var viewport = ReadSvgViewportSize(bytes);
        var sourceWidth = Math.Max(bounds.Width, viewport?.Width ?? 0);
        var sourceHeight = Math.Max(bounds.Height, viewport?.Height ?? 0);
        var scale = maximumDimension is { } limit
            ? Math.Min(1, limit / Math.Max(sourceWidth, sourceHeight))
            : 1;
        var width = Math.Max(1, (int)Math.Ceiling(sourceWidth * scale));
        var height = Math.Max(1, (int)Math.Ceiling(sourceHeight * scale));
        using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        if (surface is null)
        {
            throw new InvalidDataException("SVG rendering surface could not be created.");
        }

        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var pictureScaleX = bounds.Width > 0 ? width / bounds.Width : 1;
        var pictureScaleY = bounds.Height > 0 ? height / bounds.Height : 1;
        canvas.Scale(pictureScaleX, pictureScaleY);
        canvas.Translate(-bounds.Left, -bounds.Top);
        canvas.DrawPicture(picture);
        canvas.Flush();
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data?.ToArray() ?? throw new InvalidDataException("SVG image could not be encoded.");
    }

    private static (double Width, double Height)? ReadSvgViewportSize(byte[] bytes)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        };
        using var stream = new MemoryStream(bytes);
        using var reader = XmlReader.Create(stream, settings);
        var root = XDocument.Load(reader).Root;
        if (root is null || !string.Equals(root.Name.LocalName, "svg", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (root.Attribute("viewBox")?.Value is { } viewBox)
        {
            var values = viewBox.Replace(',', ' ')
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (values.Length == 4 &&
                TryParsePositive(values[2], out var width) &&
                TryParsePositive(values[3], out var height))
            {
                return (width, height);
            }
        }

        return TryParseSvgLength(root.Attribute("width")?.Value, out var fallbackWidth) &&
               TryParseSvgLength(root.Attribute("height")?.Value, out var fallbackHeight)
            ? (fallbackWidth, fallbackHeight)
            : null;
    }

    private static bool TryParseSvgLength(string? value, out double result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(value) || value.Contains('%'))
        {
            return false;
        }

        var text = value.Trim();
        var length = 0;
        while (length < text.Length &&
               (char.IsDigit(text[length]) || text[length] is '+' or '-' or '.' or 'e' or 'E'))
        {
            length++;
        }

        return length > 0 && TryParsePositive(text[..length], out result);
    }

    private static bool TryParsePositive(string value, out double result) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result) &&
        double.IsFinite(result) &&
        result > 0;

    private static Bitmap CreateBitmap(byte[] bytes, bool allowSkiaFallback = false)
    {
        try
        {
            using var stream = new MemoryStream(bytes);
            return new Bitmap(stream);
        }
        catch (Exception exception) when (
            allowSkiaFallback &&
            exception is ArgumentException or InvalidDataException or IOException)
        {
            return CreateBitmap(RasterizeBitmap(bytes));
        }
    }

    private static byte[] RasterizeBitmap(byte[] bytes)
    {
        using var bitmap = SKBitmap.Decode(bytes);
        if (bitmap is null)
        {
            throw new InvalidDataException("Image could not be decoded.");
        }

        return EncodeBitmap(bitmap);
    }

    private static byte[] EncodeBitmap(SKBitmap bitmap)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data?.ToArray() ?? throw new InvalidDataException("Image could not be encoded.");
    }

    private static bool IsSvg(byte[] bytes, string path) =>
        string.Equals(Path.GetExtension(path), ".svg", StringComparison.OrdinalIgnoreCase) ||
        StartsWithSvg(bytes);

    private static bool IsAnimatedPath(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".gif", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".webp", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAnimatedFile(string path)
    {
        if (IsAnimatedPath(path))
        {
            return IsAnimatedBitmapFile(path);
        }

        if (!string.Equals(Path.GetExtension(path), ".svg", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            return IsAnimatedSvg(File.ReadAllBytes(path));
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
    }

    private static bool IsAnimatedBitmapFile(string path)
    {
        try
        {
            using var codec = SKCodec.Create(path);
            return codec?.FrameCount > 1;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
    }

    private static bool IsAnimatedSvg(byte[] bytes)
    {
        if (!StartsWithSvg(bytes))
        {
            return false;
        }

        var text = ReadPrefix(bytes, 4096);
        return text.Contains("<animate", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("<set", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("<animateTransform", StringComparison.OrdinalIgnoreCase);
    }

    private static bool StartsWithSvg(byte[] bytes)
    {
        var prefix = ReadPrefix(bytes, 512).TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
        return prefix.StartsWith("<svg", StringComparison.OrdinalIgnoreCase) ||
               prefix.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase) &&
               prefix.Contains("<svg", StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadPrefix(byte[] bytes, int maximumBytes)
    {
        var length = Math.Min(bytes.Length, maximumBytes);
        return length == 0 ? string.Empty : System.Text.Encoding.UTF8.GetString(bytes, 0, length);
    }

    private sealed record ThumbnailCacheEntry(
        DateTime LastWriteTimeUtc,
        long SourceLength,
        byte[] Bytes);
}

internal sealed class GraphSvgImage : IImage, IDisposable
{
    private readonly SKSvg _svg;
    private readonly SKPicture _picture;
    private bool _isDisposed;

    public GraphSvgImage(SKSvg svg, SKPicture picture, Size size)
    {
        _svg = svg ?? throw new ArgumentNullException(nameof(svg));
        _picture = picture ?? throw new ArgumentNullException(nameof(picture));
        Size = size;
    }

    public Size Size { get; }

    public void Draw(DrawingContext context, Rect sourceRect, Rect destRect)
    {
        if (_isDisposed || sourceRect.Width <= 0 || sourceRect.Height <= 0 ||
            destRect.Width <= 0 || destRect.Height <= 0)
        {
            return;
        }

        context.Custom(new GraphSvgDrawOperation(_picture, sourceRect, destRect));
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _svg.Dispose();
    }

    private sealed class GraphSvgDrawOperation : ICustomDrawOperation
    {
        private readonly SKPicture _picture;
        private readonly Rect _sourceRect;
        private readonly Rect _destRect;

        public GraphSvgDrawOperation(
            SKPicture picture,
            Rect sourceRect,
            Rect destRect)
        {
            _picture = picture;
            _sourceRect = sourceRect;
            _destRect = destRect;
            Bounds = destRect;
        }

        public Rect Bounds { get; }

        public void Render(ImmediateDrawingContext context)
        {
            var feature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (feature is null)
            {
                return;
            }

            var scaleX = _destRect.Width / _sourceRect.Width;
            var scaleY = _destRect.Height / _sourceRect.Height;
            if (!double.IsFinite(scaleX) || !double.IsFinite(scaleY) ||
                scaleX <= 0 || scaleY <= 0)
            {
                return;
            }

            using var lease = feature.Lease();
            var canvas = lease.SkCanvas;
            canvas.Save();
            try
            {
                canvas.ClipRect(
                    new SKRect(
                        (float)_destRect.Left,
                        (float)_destRect.Top,
                        (float)_destRect.Right,
                        (float)_destRect.Bottom),
                    SKClipOperation.Intersect,
                    antialias: true);
                canvas.Translate(
                    (float)(_destRect.Left - _sourceRect.Left * scaleX),
                    (float)(_destRect.Top - _sourceRect.Top * scaleY));
                canvas.Scale((float)scaleX, (float)scaleY);
                canvas.DrawPicture(_picture);
            }
            finally
            {
                canvas.Restore();
            }
        }

        public bool HitTest(Point point) => Bounds.Contains(point);

        public bool Equals(ICustomDrawOperation? other) =>
            other is GraphSvgDrawOperation operation &&
            ReferenceEquals(_picture, operation._picture) &&
            _sourceRect == operation._sourceRect &&
            _destRect == operation._destRect;

        public void Dispose()
        {
        }
    }
}

internal sealed class GraphAnimatedBitmapImage : IImage, IDisposable
{
    private readonly Bitmap[] _frames;
    private readonly TimeSpan[] _durations;
    private readonly int _cycleMilliseconds;
    private readonly long _startedTimestamp;
    private bool _isDisposed;

    public GraphAnimatedBitmapImage(Bitmap[] frames, TimeSpan[] durations)
    {
        if (frames.Length == 0 || durations.Length != frames.Length)
        {
            throw new ArgumentException("Animated image frames and durations must have the same positive length.");
        }

        _frames = frames;
        _durations = durations;
        _cycleMilliseconds = Math.Max(1, durations.Sum(duration => Math.Max(1, (int)duration.TotalMilliseconds)));
        _startedTimestamp = Stopwatch.GetTimestamp();
    }

    public Size Size => _frames[0].Size;

    public void Draw(DrawingContext context, Rect sourceRect, Rect destRect) =>
        context.DrawImage(CurrentFrame, sourceRect, destRect);

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        foreach (var frame in _frames)
        {
            frame.Dispose();
        }
    }

    private Bitmap CurrentFrame
    {
        get
        {
            var elapsedMilliseconds = (int)(((Stopwatch.GetTimestamp() - _startedTimestamp) * 1000) /
                Stopwatch.Frequency);
            var position = Math.Abs(elapsedMilliseconds % _cycleMilliseconds);
            for (var i = 0; i < _frames.Length; i++)
            {
                position -= Math.Max(1, (int)_durations[i].TotalMilliseconds);
                if (position < 0)
                {
                    return _frames[i];
                }
            }

            return _frames[^1];
        }
    }
}

internal sealed class GraphAnimatedSvgImage : IImage, IDisposable
{
    private readonly SKSvg _svg;
    private readonly long _startedTimestamp;
    private bool _isDisposed;

    public GraphAnimatedSvgImage(SKSvg svg, Size size)
    {
        _svg = svg ?? throw new ArgumentNullException(nameof(svg));
        Size = size;
        _startedTimestamp = Stopwatch.GetTimestamp();
    }

    public Size Size { get; }

    public void Draw(DrawingContext context, Rect sourceRect, Rect destRect)
    {
        if (_isDisposed || sourceRect.Width <= 0 || sourceRect.Height <= 0 ||
            destRect.Width <= 0 || destRect.Height <= 0)
        {
            return;
        }

        context.Custom(new GraphAnimatedSvgDrawOperation(
            _svg,
            _startedTimestamp,
            sourceRect,
            destRect));
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _svg.Dispose();
    }

    private sealed class GraphAnimatedSvgDrawOperation : ICustomDrawOperation
    {
        private readonly SKSvg _svg;
        private readonly long _startedTimestamp;
        private readonly Rect _sourceRect;
        private readonly Rect _destRect;

        public GraphAnimatedSvgDrawOperation(
            SKSvg svg,
            long startedTimestamp,
            Rect sourceRect,
            Rect destRect)
        {
            _svg = svg;
            _startedTimestamp = startedTimestamp;
            _sourceRect = sourceRect;
            _destRect = destRect;
            Bounds = destRect;
        }

        public Rect Bounds { get; }

        public void Render(ImmediateDrawingContext context)
        {
            var feature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (feature is null)
            {
                return;
            }

            using var lease = feature.Lease();
            var canvas = lease.SkCanvas;
            var elapsed = TimeSpan.FromSeconds(
                Math.Max(0, Stopwatch.GetTimestamp() - _startedTimestamp) /
                (double)Stopwatch.Frequency);
            _svg.SetAnimationTime(elapsed);
            var picture = _svg.Picture;
            if (picture is null)
            {
                return;
            }

            var scaleX = _destRect.Width / _sourceRect.Width;
            var scaleY = _destRect.Height / _sourceRect.Height;
            if (!double.IsFinite(scaleX) || !double.IsFinite(scaleY) ||
                scaleX <= 0 || scaleY <= 0)
            {
                return;
            }

            canvas.Save();
            try
            {
                canvas.ClipRect(
                    new SKRect(
                        (float)_destRect.Left,
                        (float)_destRect.Top,
                        (float)_destRect.Right,
                        (float)_destRect.Bottom),
                    SKClipOperation.Intersect,
                    antialias: true);
                canvas.Translate(
                    (float)(_destRect.Left - _sourceRect.Left * scaleX),
                    (float)(_destRect.Top - _sourceRect.Top * scaleY));
                canvas.Scale((float)scaleX, (float)scaleY);
                canvas.DrawPicture(picture);
            }
            finally
            {
                canvas.Restore();
            }
        }

        public bool HitTest(Point p) => Bounds.Contains(p);

        public bool Equals(ICustomDrawOperation? other) => false;

        public void Dispose()
        {
        }
    }
}
