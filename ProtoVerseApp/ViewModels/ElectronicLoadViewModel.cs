using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProtoVerseApp.Models;
using ProtoVerseApp.Services;

namespace ProtoVerseApp.ViewModels
{
    /// <summary>
    /// Panel for the Electronic Load ProtoMod.
    ///
    /// This board is genuinely open-loop on the current hardware revision - a
    /// bit-banged PWM low-passed into an op-amp that forces a set current through
    /// a 10-ohm sense resistor, with no ADC feedback path to firmware at all.
    /// There is no measured voltage/current to show, ever, on this revision, so
    /// (per the firmware session and a user decision made when this was flagged)
    /// this panel deliberately has no live chart and no "measured" readouts -
    /// showing fabricated telemetry would be actively misleading for a teaching
    /// tool. It only shows what the device actually reports: the commanded
    /// current echoed back, and the PWM duty cycle firmware is driving.
    /// </summary>
    public partial class ElectronicLoadViewModel : ModulePanelViewModelBase
    {
        private const byte CmdSetCurrentLimitMa = 0x01;

        public override ProtoModId ModuleId => ProtoModId.ElectronicLoad;
        public override string DisplayName => "Electronic Load";

        [ObservableProperty]
        private int _commandedCurrentMa;

        [ObservableProperty]
        private int _dutyPercent;

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
            if (frame.Type != MsgType.Response)
                return;

            // [current_ma_lo, current_ma_hi, duty_percent] - current_ma is an echo
            // of what was just commanded, not a measurement (see class doc comment).
            if (frame.Payload.Length < 3)
                return;

            CommandedCurrentMa = ReadUInt16LE(frame.Payload, 0);
            DutyPercent = frame.Payload[2];
        }

        private static ushort ReadUInt16LE(byte[] data, int offset) =>
            (ushort)(data[offset] | (data[offset + 1] << 8));
    }
}
