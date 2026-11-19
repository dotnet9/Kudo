namespace Kudo.AvaloniaCommon.Converters;

public class AnyTrueConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        foreach (var item in values)
            if (item is bool value && value)
                return true;

        return false;
    }
}