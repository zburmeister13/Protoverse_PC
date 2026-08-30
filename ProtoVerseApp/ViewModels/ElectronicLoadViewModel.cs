using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OxyPlot;
using OxyPlot.Series;
using ProtoVerseApp.Charts;
using ProtoVerseApp.Models;
using ProtoVerseApp.Services;

namespace ProtoVerseApp.ViewModels
{
    /// <summary>
    /// Panel for the Electronic Load ProtoMod.
    ///
    /// TODO: SetCurrentLimit's payload and the telemetry layout in OnFrameReceived
    /// are placeholders - replace once this module's real command set is defined.
    ///
    /// The trend-line chart is deliberately decoupled from that parsing, same as
    /// AccelTempViewModel: OnFrameReceived turns raw bytes into MeasuredVoltage/
    /// MeasuredCurrentMa, and AppendToChart (below) just plots whatever those
    /// properties currently hold. Only the parsing needs to change once the real
    /// payload format lands.
    /// </summary>
    public partial class ElectronicLoadViewModel : ModulePanelViewModelBase
    {
        private const byte CmdSetCurrentLimitMa = 0x01;

        /// <summary>~2 minutes of history at roughly 1 sample/sec - see
        /// AccelTempViewModel.MaxHistoryPoints for the same reasoning.</summary>
        private const int MaxHistoryPoints = 120;

        public override ProtoModId ModuleId => ProtoModId.ElectronicLoad;
        public override string DisplayName => "Electronic Load";

        [ObservableProperty]
        private double _measuredVoltage;

        [ObservableProperty]
        private double _measuredCurrentMa;

        [ObservableProperty]
        private int _currentLimitMa = 100;

        /// <summary>Voltage and current plotted on independent axes (left/right) since
        /// they're on very different scales (~0-5 V vs. hundreds of mA) - a shared
        /// axis would flatten one of them to near-invisible.</summary>
        public PlotModel PlotModel { get; } = ChartTheme.CreateDualAxisPlotModel("Voltage (V)", "Current (mA)");

        private readonly LineSeries _voltageSeries;
        private readonly LineSeries _currentSeries;
        private readonly DateTime _startTime = DateTime.UtcNow;

        public ElectronicLoadViewModel(FrameDispatcher dispatcher) : base(dispatcher)
        {
            _voltageSeries = ChartTheme.AddLineSeries(PlotModel, ChartTheme.AccentBlue, "Voltage");
            _voltageSeries.YAxisKey = ChartTheme.LeftAxisKey;

            _currentSeries = ChartTheme.AddLineSeries(PlotModel, ChartTheme.AccentOrange, "Current");
            _currentSeries.YAxisKey = ChartTheme.RightAxisKey;

            ChartTheme.EnableLegend(PlotModel);
        }

        [RelayCommand]
        private void ApplyCurrentLimit()
        {
            ushort limit = (ushort)CurrentLimitMa;
            SendCommand(new byte[] { CmdSetCurrentLimitMa, (byte)(limit & 0xFF), (byte)(limit >> 8) });
        }

        protected override void OnFrameReceived(ProtocolFrame frame)
        {
            if (frame.Type != MsgType.Response && frame.Type != MsgType.StreamData)
                return;

            // Placeholder layout: [voltage_mV (uint16 LE)] [current_mA (uint16 LE)]
            if (frame.Payload.Length < 4)
                return;

            MeasuredVoltage = ReadUInt16LE(frame.Payload, 0) / 1000.0;
            MeasuredCurrentMa = ReadUInt16LE(frame.Payload, 2);

            AppendToChart();
        }

        private void AppendToChart()
        {
            double elapsed = (DateTime.UtcNow - _startTime).TotalSeconds;

            ChartTheme.AppendPoint(_voltageSeries, elapsed, MeasuredVoltage, MaxHistoryPoints);
            ChartTheme.AppendPoint(_currentSeries, elapsed, MeasuredCurrentMa, MaxHistoryPoints);

            PlotModel.InvalidatePlot(true);
        }

        private static ushort ReadUInt16LE(byte[] data, int offset) =>
            (ushort)(data[offset] | (data[offset + 1] << 8));
    }
}
