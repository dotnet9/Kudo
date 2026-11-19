using Kudo.Core.Documents;

namespace Kudo.Avalonia.Designer;

/// <summary>
/// 提供当前编辑文档、会话和通用命令入口。
/// </summary>
public sealed class GraphDocumentOperations
{
    private readonly GraphDocumentView _view;

    internal GraphDocumentOperations(GraphDocumentView view) => _view = view;

    public GraphDocument Value => _view.Session.Document;

    public GraphDocumentSession Session => _view.Session;

    public bool IsDirty => Session.IsDirty;

    public bool IsReadOnly
    {
        get => _view.IsReadOnly;
        set => _view.IsReadOnly = value;
    }

    public bool CanExecute(IGraphCommand command) => _view.CanExecuteCommand(command);

    public bool TryAuthorize(IGraphCommand command) => _view.TryAuthorizeCommand(command);

    public bool Execute(IGraphCommand command) => _view.TryExecuteCommand(command);
}
