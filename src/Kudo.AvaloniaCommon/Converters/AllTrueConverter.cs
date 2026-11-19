namespace Kudo.AvaloniaCommon.Converters;

public class AllTrueConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        foreach (var item in values)
            if (item is not bool value || !value)
                return false;

        return true;
    }
}