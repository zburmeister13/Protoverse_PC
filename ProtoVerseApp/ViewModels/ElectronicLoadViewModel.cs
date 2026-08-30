using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProtoVerseApp.Models;
using ProtoVerseApp.Services;

namespace ProtoVerseApp.ViewModels
{
    /// <summary>
    /// Panel for the Electronic Load ProtoMod.
    ///
    /// TODO: SetCurrentLimit's payload and the telemetry layout in OnFrameReceived
    /// are placeholders - replace once this module's real command set is defined.
    /// </summary>
    public partial class ElectronicLoadViewModel : ModulePanelViewModelBase
    {
        private const byte CmdSetCurrentLimitMa = 0x01;

        public override ProtoModId ModuleId => ProtoModId.ElectronicLoad;
        public override string DisplayName => "Electronic Load";

        [ObservableProperty]
        private double _measuredVoltage;

        [ObservableProperty]
        private double _measuredCurrentMa;

        [ObservableProperty]
        private int _currentLimitMa = 100;

        public ElectronicLoadViewModel(FrameDispatcher dispatcher) : base(dispatcher)
        {
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
        }

        private static ushort ReadUInt16LE(byte[] data, int offset) =>
            (ushort)(data[offset] | (data[offset + 1] << 8));
    }
}
