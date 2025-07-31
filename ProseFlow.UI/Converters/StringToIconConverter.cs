using Avalonia.Data.Converters;
using System;
using System.Globalization;
using Avalonia.Svg;

namespace ProseFlow.UI.Converters;

public class StringToIconConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string path && !string.IsNullOrWhiteSpace(path))
        {
            try
            {
                return new SvgImage { Source = SvgSource.Load(path, null) };
            }
            catch
            {
                // Fallback to a default icon if path is invalid
            }
        }
        return new SvgImage { Source = SvgSource.Load("avares://ProseFlow.UI/Assets/Icons/default.svg", null) };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}