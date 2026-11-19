namespace Kudo.AvaloniaCommon.Converters;

public class DateTimeStrFormatConverter : IValueConverter
{
    public const string Format = "yyyy-MM-dd HH:mm:ss";
    public const string Empty = "-";

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string dtStr && DateTime.TryParse(dtStr, out var dt) && dt != default) return dt.ToString(Format);
        return "-";
    }

    public object? ConvertBack(object? value, Type targetTypes, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}