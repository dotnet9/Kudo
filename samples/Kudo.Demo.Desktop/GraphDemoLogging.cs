using Avalonia.Media;
using CodeWF.Avalonia.Log;
using CodeWF.Toolkit.Logging;
using Kudo.Avalonia;
using Kudo.Core.Documents;
using Kudo.Core.Runtime;
using Microsoft.Extensions.Logging;
using MainWindowLanguage = global::Localization.Kudo_Demo_Desktop.Views.MainWindow;

namespace Kudo.Demo.Desktop;

internal static class GraphDemoLogging
{
    private static readonly object Gate = new();
    private static bool _isInitialized;

    public static LogView CreateView()
    {
        EnsureInitialized();
        return new LogView
        {
            Source = Logger.Events,
            MinimumLevel = LogLevel.Trace,
            MaximumLevel = LogLevel.Critical,
            MaxDisplayCount = 1_000,
            RefreshInterval = TimeSpan.FromMilliseconds(80),
            TimestampFormat = "HH:mm:ss.fff",
            Background = Brushes.Transparent,
            Margin = new global::Avalonia.Thickness(10, 5)
        };
    }

    public static void Information(string message)
    {
        EnsureInitialized();
        Logger.Info(message);
    }

    public static void Warning(string message, Exception? exception = null)
    {
        EnsureInitialized();
        Logger.Warn(message, exception);
    }

    public static void Runtime(GraphDocumentSession session, GraphRuntimeDiagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(diagnostic);
        EnsureInitialized();

        var title = string.IsNullOrWhiteSpace(session.Document.Title)
            ? L(MainWindowLanguage.UntitledCanvas)
            : session.Document.Title;
        var node = diagnostic.NodeId is null
            ? string.Empty
            : F(MainWindowLanguage.RuntimeLogNode, diagnostic.NodeId);
        var message = $"[{title}] Tick {diagnostic.TickCount}{node} · {FormatMessage(diagnostic)}";
        switch (diagnostic.Severity)
        {
            case GraphRuntimeDiagnosticSeverity.Trace:
                Logger.Trace(message);
                break;
            case GraphRuntimeDiagnosticSeverity.Information:
                Logger.Info(message);
                break;
            case GraphRuntimeDiagnosticSeverity.Warning:
                Logger.Warn(message, diagnostic.Exception);
                break;
            case GraphRuntimeDiagnosticSeverity.Error:
                Logger.Error(message, diagnostic.Exception);
                break;
            default:
                Logger.Info(message);
                break;
        }
    }

    public static void EnsureInitialized()
    {
        lock (Gate)
        {
            if (_isInitialized)
            {
                return;
            }

            _ = Logger.TryInitialize(new LoggerOptions
            {
                MinimumLevel = LogLevel.Trace,
                EnableConsole = false,
                EnableEventFeed = true,
                RecentEventCapacity = 2_000,
                LocalizedLevelFormatter = FormatLevel,
                LineTemplate = "{Timestamp:HH:mm:ss.fff} [{Level:localized}] {Message}{NewLine}"
            });
            _isInitialized = true;
        }
    }

    private static string FormatMessage(GraphRuntimeDiagnostic diagnostic) =>
        diagnostic.Code switch
        {
            GraphRuntimeDiagnosticCodes.Paused => L(MainWindowLanguage.RuntimeLogPaused),
            GraphRuntimeDiagnosticCodes.Stepped => L(MainWindowLanguage.RuntimeLogStepped),
            GraphRuntimeDiagnosticCodes.Stopped => L(MainWindowLanguage.RuntimeLogStopped),
            GraphRuntimeDiagnosticCodes.Reset => L(MainWindowLanguage.RuntimeLogReset),
            GraphRuntimeDiagnosticCodes.BreakpointHit => L(MainWindowLanguage.RuntimeLogBreakpointHit),
            GraphRuntimeDiagnosticCodes.SnapshotRefreshed => L(MainWindowLanguage.RuntimeLogSnapshotRefreshed),
            GraphRuntimeDiagnosticCodes.Faulted =>
                F(MainWindowLanguage.RuntimeLogFaulted, diagnostic.Message),
            _ => diagnostic.Message
        };

    private static string FormatLevel(LogLevel level) => level switch
    {
        LogLevel.Trace => L(MainWindowLanguage.LogLevelTrace),
        LogLevel.Debug => L(MainWindowLanguage.LogLevelDebug),
        LogLevel.Information => L(MainWindowLanguage.LogLevelInformation),
        LogLevel.Warning => L(MainWindowLanguage.LogLevelWarning),
        LogLevel.Error => L(MainWindowLanguage.LogLevelError),
        LogLevel.Critical => L(MainWindowLanguage.LogLevelCritical),
        LogLevel.None => L(MainWindowLanguage.LogLevelNone),
        _ => level.ToString()
    };

    private static string L(string key) => GraphLocalization.Get(key);

    private static string F(string key, params object?[] arguments) =>
        GraphLocalization.Format(key, arguments);
}
