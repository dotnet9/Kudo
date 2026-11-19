// 属性编辑器扩展点：允许领域模块按当前上下文提供独立属性内容，并支持卸载恢复。
using Avalonia.Controls;

namespace Kudo.Avalonia.Designer;

public sealed record GraphPropertyEditorContent(
    string Title,
    IReadOnlyList<Control> Controls,
    IDisposable? Lifetime = null);

public interface IGraphPropertyEditorProvider
{
    bool TryCreate(
        GraphPropertyEditorContext editorContext,
        GraphPropertyContext propertyContext,
        out GraphPropertyEditorContent? content);
}

public sealed class GraphPropertyEditorProviderRegistry
{
    private readonly List<Registration> _registrations = [];

    public event EventHandler? Changed;

    public IReadOnlyList<IGraphPropertyEditorProvider> Providers => _registrations
        .Select(item => item.Provider)
        .ToArray();

    /// <summary>
    /// 按注册顺序加入 Provider；匹配时后注册项优先，释放句柄后恢复之前的结果。
    /// </summary>
    public IDisposable Register(IGraphPropertyEditorProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        var registration = new Registration(this, provider);
        _registrations.Add(registration);
        Changed?.Invoke(this, EventArgs.Empty);
        return registration;
    }

    public bool TryCreate(
        GraphPropertyEditorContext editorContext,
        GraphPropertyContext propertyContext,
        out GraphPropertyEditorContent? content)
    {
        for (var index = _registrations.Count - 1; index >= 0; index--)
        {
            if (_registrations[index].Provider.TryCreate(editorContext, propertyContext, out content))
            {
                if (content is null || string.IsNullOrWhiteSpace(content.Title) ||
                    content.Controls is null || content.Controls.Any(control => control is null))
                {
                    throw new InvalidOperationException(
                        "The property editor provider returned invalid content.");
                }

                return true;
            }
        }

        content = null;
        return false;
    }

    private void Remove(Registration registration)
    {
        if (!_registrations.Remove(registration))
        {
            return;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private sealed class Registration(
        GraphPropertyEditorProviderRegistry owner,
        IGraphPropertyEditorProvider provider) : IDisposable
    {
        private GraphPropertyEditorProviderRegistry? _owner = owner;

        public IGraphPropertyEditorProvider Provider { get; } = provider;

        public void Dispose() => Interlocked.Exchange(ref _owner, null)?.Remove(this);
    }
}
