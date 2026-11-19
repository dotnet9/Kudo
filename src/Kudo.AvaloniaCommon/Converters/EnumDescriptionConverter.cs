using CodeWF.Toolkit.Core.Extensions;

using Avalonia;

namespace Kudo.AvaloniaCommon.Converters;

public class EnumDescriptionConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Enum enumKind) return enumKind.GetDescription();

        return value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var enumType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (!enumType.IsEnum) return AvaloniaProperty.UnsetValue;

        if (value is null)
        {
            return Nullable.GetUnderlyingType(targetType) is not null
                ? null
                : AvaloniaProperty.UnsetValue;
        }

        if (value.GetType() == enumType) return value;
        if (value is not string text) return AvaloniaProperty.UnsetValue;

        foreach (var enumValue in Enum.GetValues(enumType).Cast<Enum>())
        {
            if (string.Equals(enumValue.GetDescription(), text, StringComparison.Ordinal)
                || string.Equals(enumValue.ToString(), text, StringComparison.Ordinal))
            {
                return enumValue;
            }
        }

        return AvaloniaProperty.UnsetValue;
    }
}
