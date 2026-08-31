using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ProtoVerseApp.Converters
{
    /// <summary>true -&gt; Visible, false -&gt; Collapsed. WPF ships its own
    /// BooleanToVisibilityConverter, but it lives in a framework namespace that the
    /// Library card templates would otherwise have to import separately - keeping both
    /// directions here means every visibility binding in this app reads the same way.
    /// Registered in App.xaml (Application.Resources), not a single view's resources:
    /// a window-scoped resource isn't visible from a separate UserControl file.</summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is true ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is Visibility.Visible;
    }

    /// <summary>false -&gt; Visible, true -&gt; Collapsed. Used for the Library's
    /// "…coming soon" placeholders, which appear exactly where the real content is
    /// absent.</summary>
    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is true ? Visibility.Collapsed : Visibility.Visible;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is not Visibility.Visible;
    }
}
