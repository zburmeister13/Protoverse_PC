using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;

namespace ProtoVerseApp.Charts
{
    /// <summary>
    /// Builds OxyPlot PlotModels themed to match the ProtoVerse brand palette defined
    /// in App.xaml. OxyPlot's PlotModel is a plain C# object, not a WPF
    /// DependencyObject, so it can't bind to a StaticResource brush the way the rest
    /// of the app's controls do - these hex values are the same kind of literal
    /// duplication already accepted for BoolToBrushConverter/SlotStateToBrushConverter
    /// (see CLAUDE.md's "Branded dark theme" note) and must be kept in sync by hand if
    /// the palette in App.xaml ever changes.
    /// </summary>
    public static class ChartTheme
    {
        public const string LeftAxisKey = "Left";
        public const string RightAxisKey = "Right";

        public static readonly OxyColor Background = OxyColor.FromRgb(0x24, 0x1D, 0x57); // SurfaceColor
        public static readonly OxyColor GridLines = OxyColor.FromRgb(0x3D, 0x34, 0x75);   // BorderColor
        public static readonly OxyColor Text = OxyColor.FromRgb(0xF5, 0xF0, 0xE6);        // TextPrimaryColor
        public static readonly OxyColor TextMuted = OxyColor.FromRgb(0xA7, 0x9F, 0xD1);   // TextSecondaryColor

        public static readonly OxyColor AccentTeal = OxyColor.FromRgb(0x3F, 0xD6, 0xC4);
        public static readonly OxyColor AccentGreen = OxyColor.FromRgb(0x6F, 0xCF, 0x61);
        public static readonly OxyColor AccentBlue = OxyColor.FromRgb(0x4F, 0xA9, 0xE0);
        public static readonly OxyColor AccentOrange = OxyColor.FromRgb(0xF2, 0x99, 0x4A);

        /// <summary>Themed, empty PlotModel with a single left Y axis. Caller adds
        /// its own series via AddLineSeries.</summary>
        public static PlotModel CreatePlotModel(string yAxisTitle)
        {
            var model = CreateBaseModel();
            model.Axes.Add(CreateValueAxis(yAxisTitle, AxisPosition.Left, null));
            return model;
        }

        /// <summary>Themed, empty PlotModel with independent left/right Y axes, for
        /// two series with very different scales (e.g. volts vs. milliamps) sharing
        /// one chart. Series added to this model must set YAxisKey to LeftAxisKey or
        /// RightAxisKey.</summary>
        public static PlotModel CreateDualAxisPlotModel(string leftTitle, string rightTitle)
        {
            var model = CreateBaseModel();
            model.Axes.Add(CreateValueAxis(leftTitle, AxisPosition.Left, LeftAxisKey));
            model.Axes.Add(CreateValueAxis(rightTitle, AxisPosition.Right, RightAxisKey));
            return model;
        }

        private static PlotModel CreateBaseModel()
        {
            var model = new PlotModel
            {
                Background = Background,
                PlotAreaBorderColor = GridLines,
                TextColor = Text,
                TitleColor = Text,
            };
            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Elapsed (s)",
                TextColor = TextMuted,
                TitleColor = TextMuted,
                TicklineColor = GridLines,
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = GridLines,
                MinorGridlineColor = GridLines,
                AxislineColor = GridLines,
            });
            return model;
        }

        private static LinearAxis CreateValueAxis(string title, AxisPosition position, string? key) => new()
        {
            Key = key,
            Position = position,
            Title = title,
            TextColor = TextMuted,
            TitleColor = TextMuted,
            TicklineColor = GridLines,
            MajorGridlineStyle = position == AxisPosition.Left ? LineStyle.Solid : LineStyle.None,
            MajorGridlineColor = GridLines,
            MinorGridlineColor = GridLines,
            AxislineColor = GridLines,
        };

        /// <summary>Turns on a themed legend for a multi-series chart. Only call this
        /// where a legend actually disambiguates something (e.g. X/Y/Z, or two series
        /// sharing a chart) - a single-series chart's axis title already says what it
        /// is, so skip this there rather than showing a legend with one blank-looking
        /// entry.</summary>
        public static void EnableLegend(PlotModel model) => model.Legends.Add(new Legend
        {
            LegendPosition = LegendPosition.TopRight,
            LegendPlacement = LegendPlacement.Inside,
            LegendOrientation = LegendOrientation.Horizontal,
            LegendTextColor = Text,
            LegendBackground = OxyColors.Transparent,
        });

        /// <summary>Adds and returns a themed line series.</summary>
        public static LineSeries AddLineSeries(PlotModel model, OxyColor color, string? title = null)
        {
            var series = new LineSeries
            {
                Color = color,
                StrokeThickness = 2,
                Title = title,
            };
            model.Series.Add(series);
            return series;
        }

        /// <summary>Appends a point, trimming the oldest once over maxPoints so the
        /// chart shows a rolling window instead of growing unbounded over a long
        /// session.</summary>
        public static void AppendPoint(LineSeries series, double x, double y, int maxPoints)
        {
            series.Points.Add(new DataPoint(x, y));
            while (series.Points.Count > maxPoints)
                series.Points.RemoveAt(0);
        }

        /// <summary>Themed PlotModel for a 2D "bubble level"-style XY marker: both
        /// axes are fixed to [-range, range] (not auto-ranging) so the origin always
        /// sits at the exact visual center regardless of the current reading, with a
        /// distinct crosshair gridline through zero on each axis.</summary>
        public static PlotModel CreateXyPlotModel(double range)
        {
            var model = new PlotModel
            {
                Background = Background,
                PlotAreaBorderColor = GridLines,
                TextColor = Text,
                TitleColor = Text,
            };
            model.Axes.Add(CreateCrosshairAxis("X (g)", AxisPosition.Bottom, range));
            model.Axes.Add(CreateCrosshairAxis("Y (g)", AxisPosition.Left, range));
            return model;
        }

        private static LinearAxis CreateCrosshairAxis(string title, AxisPosition position, double range) => new()
        {
            Position = position,
            Title = title,
            Minimum = -range,
            Maximum = range,
            TextColor = TextMuted,
            TitleColor = TextMuted,
            TicklineColor = GridLines,
            MajorGridlineStyle = LineStyle.Dot,
            MajorGridlineColor = GridLines,
            AxislineColor = GridLines,
            ExtraGridlines = new[] { 0.0 },
            ExtraGridlineColor = TextMuted,
            ExtraGridlineThickness = 1.5,
        };

        /// <summary>Adds the single marker used by the XY plot. Call SetXyPoint to
        /// move it - a bubble-level indicator only ever has one current position, not
        /// a trail of history.</summary>
        public static ScatterSeries AddXyMarker(PlotModel model, OxyColor color)
        {
            var series = new ScatterSeries
            {
                MarkerType = MarkerType.Circle,
                MarkerFill = color,
                MarkerStroke = Text,
                MarkerStrokeThickness = 1.5,
                MarkerSize = 8,
            };
            model.Series.Add(series);
            return series;
        }

        public static void SetXyPoint(ScatterSeries series, double x, double y)
        {
            series.Points.Clear();
            series.Points.Add(new ScatterPoint(x, y));
        }

        /// <summary>Themed PlotModel for a single vertical "fill gauge" bar: a
        /// value-only Y axis fixed to [minY, maxY], and an invisible X axis wide
        /// enough for exactly one bar (there's nothing to categorize - it's a single
        /// continuously-updated value, not a series of named bars).</summary>
        public static PlotModel CreateVerticalGaugeModel(string yAxisTitle, double minY, double maxY)
        {
            var model = new PlotModel
            {
                Background = Background,
                PlotAreaBorderColor = GridLines,
                TextColor = Text,
                TitleColor = Text,
            };
            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                IsAxisVisible = false,
                Minimum = -1,
                Maximum = 1,
            });
            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = yAxisTitle,
                Minimum = minY,
                Maximum = maxY,
                TextColor = TextMuted,
                TitleColor = TextMuted,
                TicklineColor = GridLines,
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = GridLines,
                MinorGridlineColor = GridLines,
                AxislineColor = GridLines,
            });
            return model;
        }

        /// <summary>Adds the single bar used by a vertical gauge. baseValue is where
        /// the bar's unfilled edge sits - the bar fills from there towards whatever
        /// value SetGaugeValue is given, growing upward above baseValue and downward
        /// below it, exactly like a thermometer with its zero line moved.</summary>
        public static LinearBarSeries AddVerticalGaugeBar(PlotModel model, OxyColor color, double baseValue)
        {
            var series = new LinearBarSeries
            {
                FillColor = color,
                StrokeColor = color,
                StrokeThickness = 1,
                BarWidth = 1.6,
                BaseValue = baseValue,
            };
            model.Series.Add(series);
            return series;
        }

        public static void SetGaugeValue(LinearBarSeries series, double value)
        {
            series.Points.Clear();
            series.Points.Add(new DataPoint(0, value));
        }
    }
}
