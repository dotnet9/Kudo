using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Kudo.Controls.PropertyModel;
using TKCode.Arc;
using ArcPropertyGrid = TKCode.Arc.PropertyGrid;

namespace Kudo.Avalonia.Designer;

/// <summary>Arc PropertyGrid 的属性行宿主对象：行集合经 <see cref="PropertyItemGridBridge"/> 注册到本类型。</summary>
internal sealed class PropertyItemBag
{
    internal IReadOnlyList<PropertyItem> Items { get; }

    internal PropertyItemBag(IReadOnlyList<PropertyItem> items) => Items = items;
}

/// <summary>
///     把 GraphPropertyPanelItems 生成的 PropertyItem 条目列表映射到 Arc 的 PropertyGrid：
///     每个叶子条目转换为一个 PropertyGridProperty 委托描述（getter 读条目值，setter 走预览校验与提交回调），
///     分类条目转换为行的 Category 分组，条件可见转换为 PropertyFilter。
/// </summary>
internal static class PropertyItemGridBridge
{
    public static void Bind(ArcPropertyGrid grid, IReadOnlyList<PropertyItem> items, GraphPropertyPanelItems panelItems)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(panelItems);

        var properties = new List<PropertyGridProperty>();
        var visiblePredicates = new Dictionary<string, Func<bool>>(StringComparer.Ordinal);
        var currentCategory = string.Empty;

        foreach (var item in items)
        {
            if (item.Kind == PropertyItemKind.Category)
            {
                currentCategory = item.Name;
                continue;
            }

            if (item.VisibleWhen is not null)
            {
                visiblePredicates[item.Id] = item.VisibleWhen;
            }

            properties.Add(BuildProperty(item, currentCategory, panelItems));
        }

        PropertyGridTypes.Register(typeof(PropertyItemBag),
            () => new PropertyGridTypeInfo(typeof(PropertyItemBag), properties));

        grid.PropertyFilter = property =>
            !visiblePredicates.TryGetValue(property.Name, out var visibleWhen) || visibleWhen();
        grid.SelectedObject = new PropertyItemBag(items);
    }

    private static PropertyGridProperty BuildProperty(
        PropertyItem item,
        string category,
        GraphPropertyPanelItems panelItems)
    {
        var isReadOnly = item.IsReadOnly || item.Kind == PropertyItemKind.ReadOnly;
        var propertyType = PropertyTypeOf(item.Kind);
        var getter = new Func<object, object?>(_ => ReadValue(item));
        Func<object, object?, object>? setter = null;
        if (!isReadOnly)
        {
            setter = (_, newValue) =>
            {
                var oldValue = item.Value;
                if (panelItems.HandlePreview(item, newValue))
                {
                    throw new InvalidOperationException("预览校验未通过，值已回退。");
                }

                panelItems.HandleCommit(item, oldValue, newValue);
                item.Value = newValue;
                return oldValue;
            };
        }

        var choices = item.Kind is PropertyItemKind.Choice or PropertyItemKind.Radio
            ? item.Options?
                .Select(option => new PropertyGridChoice(option, option))
                .ToList()
            : null;
        var property = new PropertyGridProperty(item.Id, propertyType, getter, setter)
        {
            DisplayName = item.Name,
            Category = string.IsNullOrEmpty(category) ? null : category,
            Description = item.Description,
            Minimum = item.Minimum,
            Maximum = item.Maximum,
            Step = item.Increment,
            Choices = choices,
        };

        return property;
    }

    private static Type PropertyTypeOf(PropertyItemKind kind) => kind switch
    {
        PropertyItemKind.Number => typeof(double),
        PropertyItemKind.Percent => typeof(double),
        PropertyItemKind.Boolean => typeof(bool),
        PropertyItemKind.Color => typeof(Color),
        PropertyItemKind.DateTime => typeof(DateTime),
        _ => typeof(string)
    };

    private static object? ReadValue(PropertyItem item)
    {
        var text = item.Value as string;
        return item.Kind switch
        {
            PropertyItemKind.Number or PropertyItemKind.Percent
                => double.TryParse(text, out var number) ? number : 0d,
            PropertyItemKind.Boolean => bool.TryParse(text, out var flag) && flag,
            PropertyItemKind.Color => Color.Parse(string.IsNullOrWhiteSpace(text) ? "#00000000" : text),
            PropertyItemKind.DateTime
                => DateTime.TryParse(text, out var moment) ? moment : DateTime.Now,
            _ => text ?? string.Empty
        };
    }
}
