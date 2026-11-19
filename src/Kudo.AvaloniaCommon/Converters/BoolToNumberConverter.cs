namespace Kudo.AvaloniaCommon.Converters;

public class BoolToNumberConverter : IValueConverter
{
    public const int True = 0;
    public const int False = 1;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool bValue) return bValue ? True : False;

        return False;
    }

    public object? ConvertBack(object? value, Type targetTypes, object? parameter, CultureInfo culture)
    {
        if (value is int intValue) return intValue == True;
        return false;
    }
}