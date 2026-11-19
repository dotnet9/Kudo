// 监视表达式求值器：隔离 Provider 调用、异常结果转换和取消处理。
namespace Kudo.Core.Runtime;

internal static class GraphRuntimeWatchEvaluator
{
    public static async ValueTask<IReadOnlyList<GraphRuntimeWatchResult>> EvaluateAsync(
        GraphDocument document,
        IGraphRuntimeStateProvider stateProvider,
        GraphRuntimeSnapshot snapshot,
        IReadOnlyList<string> watchExpressions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(stateProvider);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(watchExpressions);
        if (watchExpressions.Count == 0)
        {
            return [];
        }

        var results = new List<GraphRuntimeWatchResult>(watchExpressions.Count);
        foreach (var expression in watchExpressions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (stateProvider is IGraphRuntimeWatchEvaluator evaluator)
            {
                try
                {
                    var result = await evaluator
                        .EvaluateWatchAsync(document, snapshot, expression, cancellationToken)
                        .ConfigureAwait(false);
                    results.Add(result with { Expression = expression });
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    results.Add(new GraphRuntimeWatchResult(
                        expression,
                        null,
                        Error: exception.Message));
                }

                continue;
            }

            var variable = snapshot.Variables.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, expression, StringComparison.OrdinalIgnoreCase));
            results.Add(variable is null
                ? new GraphRuntimeWatchResult(
                    expression,
                    null,
                    Error: "The variable was not found in the current snapshot.")
                {
                    ErrorCode = GraphRuntimeWatchErrorCodes.VariableNotFound
                }
                : new GraphRuntimeWatchResult(
                    expression,
                    variable.Value,
                    variable.TypeName ?? variable.Value?.GetType().Name));
        }

        return results;
    }
}
