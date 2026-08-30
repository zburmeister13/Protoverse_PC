using CommunityToolkit.Mvvm.ComponentModel;
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
    /// </summary>
    public partial class AccelTempViewModel : ModulePanelViewModelBase
    {
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

        public AccelTempViewModel(FrameDispatcher dispatcher) : base(dispatcher)
        {
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
        }

        private static short ReadInt16LE(byte[] data, int offset) =>
            (short)(data[offset] | (data[offset + 1] << 8));
    }
}
