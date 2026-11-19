namespace Kudo.AvaloniaCommon.Converters;

public class DateTimeFormatConverter : IValueConverter
{
    public const string Format = "yyyy-MM-dd HH:mm:ss";
    public const string Empty = "-";

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DateTime dt)
        {
            if (dt == default) return Empty;
            return dt.ToString(Format);
        }

        return Empty;
    }

    public object? ConvertBack(object? value, Type targetTypes, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}