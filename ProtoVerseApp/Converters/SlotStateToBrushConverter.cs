using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using ProtoVerseApp.ViewModels;

namespace ProtoVerseApp.Converters
{
    /// <summary>Slot status dot: Empty -> transparent (hollow ring), Occupied ->
    /// brand accent green, Unsupported -> brand accent orange (a module is there,
    /// but this app build has no panel for it). Colors match the ProtoVerse brand
    /// palette defined in App.xaml (AccentGreenColor / AccentOrangeColor) - kept as
    /// literal values here since a converter can't cleanly bind to a
    /// StaticResource.</summary>
    public class SlotStateToBrushConverter : IValueConverter
    {
        private static readonly Brush OccupiedBrush = new SolidColorBrush(Color.FromRgb(0x6F, 0xCF, 0x61));
        private static readonly Brush UnsupportedBrush = new SolidColorBrush(Color.FromRgb(0xF2, 0x99, 0x4A));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value switch
            {
                SlotState.Occupied => OccupiedBrush,
                SlotState.Unsupported => UnsupportedBrush,
                _ => Brushes.Transparent
            };

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
