using System;
using CommunityToolkit.Mvvm.ComponentModel;
using OxyPlot;
using OxyPlot.Series;
using ProtoVerseApp.Charts;
using ProtoVerseApp.Models;
using ProtoVerseApp.Services;

namespace ProtoVerseApp.ViewModels
{
    /// <summary>
    /// Panel for the Accelerometer + Temperature ProtoMod.
    ///
    /// TODO: the payload layout below is a placeholder (temp as one signed byte in
    /// degrees C, accel X/Y/Z as three signed 16-bit values in milli-g) - replace once
    /// the actual sensor's data format and the firmware's read command are defined.
    ///
    /// The charts below are deliberately decoupled from that parsing: OnFrameReceived
    /// turns raw bytes into TemperatureC/AccelXg/AccelYg/AccelZg, and AppendToCharts
    /// just plots whatever those properties currently hold - a temperature trend line,
    /// an X/Y "bubble level" tilt indicator, and a Z fill gauge centered on -1g. When
    /// the real payload format lands, only the parsing in OnFrameReceived needs to
    /// change - the charts, their theming, and this decoupling don't, and Simulator
    /// mode's fake telemetry already exercises this whole path today.
    /// </summary>
    public partial class AccelTempViewModel : ModulePanelViewModelBase
    {
        /// <summary>How much history the temperature chart keeps on screen. Telemetry
        /// streams roughly once a second, so this is ~2 minutes - long enough to see a
        /// trend, short enough that the chart (and memory) doesn't grow unbounded over
        /// a long session.</summary>
        private const int MaxHistoryPoints = 120;

        /// <summary>Symmetric axis range (g) for the XY tilt plot - fixed rather than
        /// auto-ranging so the origin always sits at the exact visual center. 1.5g
        /// comfortably frames the simulator's ±1g sine/cosine excursions with margin.</summary>
        private const double XyRange = 1.5;

        /// <summary>The Z gauge's baseline/center value (g) - per spec, -1g (a flat,
        /// resting board under normal gravity) sits at the middle of the bar; the bar
        /// fills upward for readings above -1g and downward for readings below it.</summary>
        private const double ZGaugeCenter = -1.0;

        /// <summary>How far above/below ZGaugeCenter the gauge's axis extends.</summary>
        private const double ZGaugeHalfRange = 1.0;

        public override ProtoModId ModuleId => ProtoModId.AccelTemp;
        public override string DisplayName => "Accelerometer + Temperature";

        [ObservableProperty]
        private double _temperatureC;

        [ObservableProperty]
        private double _accelXg;

        [ObservableProperty]
        private double _accelYg;

        [ObservableProperty]
        private double _accelZg;

        public PlotModel TempPlotModel { get; } = ChartTheme.CreatePlotModel("Temperature (°C)");

        /// <summary>Bubble-level-style X/Y tilt indicator - a single dot sitting at
        /// the origin when the board is flat (X≈0, Y≈0), moving off-center as it
        /// tilts.</summary>
        public PlotModel XyPlotModel { get; } = ChartTheme.CreateXyPlotModel(XyRange);

        /// <summary>Vertical fill gauge for Z - see ZGaugeCenter.</summary>
        public PlotModel ZGaugeModel { get; } =
            ChartTheme.CreateVerticalGaugeModel("Z (g)", ZGaugeCenter - ZGaugeHalfRange, ZGaugeCenter + ZGaugeHalfRange);

        private readonly LineSeries _tempSeries;
        private readonly ScatterSeries _xyMarker;
        private readonly LinearBarSeries _zGaugeSeries;
        private readonly DateTime _startTime = DateTime.UtcNow;

        public AccelTempViewModel(FrameDispatcher dispatcher) : base(dispatcher)
        {
            _tempSeries = ChartTheme.AddLineSeries(TempPlotModel, ChartTheme.AccentOrange);
            _xyMarker = ChartTheme.AddXyMarker(XyPlotModel, ChartTheme.AccentTeal);
            _zGaugeSeries = ChartTheme.AddVerticalGaugeBar(ZGaugeModel, ChartTheme.AccentGreen, ZGaugeCenter);
        }

        protected override void OnFrameReceived(ProtocolFrame frame)
        {
            if (frame.Type != MsgType.Response && frame.Type != MsgType.StreamData)
                return;

            // Placeholder layout: [tempC (sbyte)] [accelX_mg (int16 LE)] [accelY_mg] [accelZ_mg]
            if (frame.Payload.Length < 7)
                return;

            TemperatureC = unchecked((sbyte)frame.Payload[0]);
            AccelXg = ReadInt16LE(frame.Payload, 1) / 1000.0;
            AccelYg = ReadInt16LE(frame.Payload, 3) / 1000.0;
            AccelZg = ReadInt16LE(frame.Payload, 5) / 1000.0;

            AppendToCharts();
        }

        private void AppendToCharts()
        {
            double elapsed = (DateTime.UtcNow - _startTime).TotalSeconds;
            ChartTheme.AppendPoint(_tempSeries, elapsed, TemperatureC, MaxHistoryPoints);
            TempPlotModel.InvalidatePlot(true);

            ChartTheme.SetXyPoint(_xyMarker, AccelXg, AccelYg);
            XyPlotModel.InvalidatePlot(true);

            ChartTheme.SetGaugeValue(_zGaugeSeries, AccelZg);
            ZGaugeModel.InvalidatePlot(true);
        }

        private static short ReadInt16LE(byte[] data, int offset) =>
            (short)(data[offset] | (data[offset + 1] << 8));
    }
}
