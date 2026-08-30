using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ProtoVerseApp.Converters
{
    /// <summary>true -> brand accent green (on), false -> muted lavender-gray (off).
    /// Used for the Blinky LED on/off indicator dot. Colors match the ProtoVerse
    /// brand palette defined in App.xaml (AccentGreenColor / TextSecondaryColor) -
    /// kept as literal values here since a converter can't cleanly bind to a
    /// StaticResource.</summary>
    public class BoolToBrushConverter : IValueConverter
    {
        private static readonly Brush OnBrush = new SolidColorBrush(Color.FromRgb(0x6F, 0xCF, 0x61));
        private static readonly Brush OffBrush = new SolidColorBrush(Color.FromRgb(0xA7, 0x9F, 0xD1));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is bool b && b ? OnBrush : OffBrush;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
