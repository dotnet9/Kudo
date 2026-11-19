using Kudo.Avalonia;
using Kudo.Core.Documents;
using System.Diagnostics;
using MainWindowLanguage = global::Localization.Kudo_Demo_Desktop.Views.MainWindow;

namespace Kudo.Demo.Desktop.Services;

internal sealed class MainWindowSampleService(
    GraphDocumentManager documents,
    IDemoGraphTextProvider graphTextProvider,
    Action<GraphDocumentSession> activateDocument,
    Action<string, string?> appendLog)
{
    private int _documentSequence = documents.Sessions.Count;
    private bool _isCreatingLargeGraph;

    public void CreateGraph()
    {
        _documentSequence = Math.Max(_documentSequence, documents.Sessions.Count);
        _documentSequence++;
        var session = documents.Create(F(MainWindowLanguage.UntitledCanvasSequence, _documentSequence));
        activateDocument(session);
        appendLog(
            F(MainWindowLanguage.CanvasCreated, session.Document.Title),
            L(MainWindowLanguage.CanvasCreatedStatus));
    }

    public async Task CreateLargeGraphAsync(int nodeCount)
    {
        if (_isCreatingLargeGraph)
        {
            return;
        }

        _isCreatingLargeGraph = true;
        try
        {
            appendLog(
                F(MainWindowLanguage.LargeGraphGenerating, nodeCount),
                F(MainWindowLanguage.LargeGraphGeneratingStatus, nodeCount));
            var generationTimer = Stopwatch.StartNew();
            var document = await Task.Run(() =>
                SampleGraphs.CreateLargeWorkflowDocument(nodeCount, graphTextProvider));
            generationTimer.Stop();

            var loadingTimer = Stopwatch.StartNew();
            var session = documents.Add(document);
            activateDocument(session);
            loadingTimer.Stop();

            appendLog(
                F(
                    MainWindowLanguage.LargeGraphReady,
                    document.Nodes.Count,
                    document.Edges.Count,
                    generationTimer.ElapsedMilliseconds,
                    loadingTimer.ElapsedMilliseconds),
                F(MainWindowLanguage.LargeGraphReadyStatus, nodeCount));
        }
        catch (Exception exception)
        {
            appendLog(
                F(MainWindowLanguage.LargeGraphFailed, nodeCount, exception.Message),
                L(MainWindowLanguage.LargeGraphFailedStatus));
        }
        finally
        {
            _isCreatingLargeGraph = false;
        }
    }

    private static string L(string key) => GraphLocalization.Get(key);

    private static string F(string key, params object?[] arguments) =>
        GraphLocalization.Format(key, arguments);
}
