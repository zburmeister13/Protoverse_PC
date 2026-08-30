using System;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProtoVerseApp.Models;
using ProtoVerseApp.Services;

namespace ProtoVerseApp.ViewModels
{
    /// <summary>
    /// Panel for the Blinky LED ProtoMod. Every command (SetState, SetBlinkRate,
    /// SetPattern, SetDirection, SetManualLeds) gets back the same 7-byte full-state
    /// snapshot from firmware, so there's one parse path in OnFrameReceived instead of
    /// a per-command special case, and every property here reflects the device's last
    /// echoed state rather than a locally-optimistic guess.
    ///
    /// The protocol only reports a state *snapshot* per command - it doesn't stream a
    /// frame per animation step, the same way a real board wouldn't report over serial
    /// every time its own LED-stepper timer fires. So while Mode is Animated, this view
    /// model runs its own local timer to visually replicate what the board's 4 physical
    /// LEDs should be doing right now, using the last known Pattern/Reverse/BlinkRateMs -
    /// this is a local re-creation of expected board behavior, not real telemetry.
    /// </summary>
    public partial class BlinkyLedViewModel : ModulePanelViewModelBase
    {
        private const byte CmdSetState = 0x01;
        private const byte CmdSetBlinkRateMs = 0x02;
        private const byte CmdSetPattern = 0x03;
        private const byte CmdSetDirection = 0x04;
        private const byte CmdSetManualLeds = 0x05;

        public override ProtoModId ModuleId => ProtoModId.BlinkyLed;
        public override string DisplayName => "Blinky LED";

        public BlinkyLedPattern[] Patterns { get; } = (BlinkyLedPattern[])Enum.GetValues(typeof(BlinkyLedPattern));

        [ObservableProperty]
        private bool _isOn;

        [ObservableProperty]
        private int _blinkRateMs = 500;

        [ObservableProperty]
        private BlinkyLedMode _mode = BlinkyLedMode.Animated;

        [ObservableProperty]
        private BlinkyLedPattern _pattern = BlinkyLedPattern.Bounce;

        [ObservableProperty]
        private bool _reverse;

        [ObservableProperty]
        private bool _led0On;

        [ObservableProperty]
        private bool _led1On;

        [ObservableProperty]
        private bool _led2On;

        [ObservableProperty]
        private bool _led3On;

        /// <summary>True while a device-echoed state snapshot is being applied back
        /// onto these same properties. Without this guard, writing e.g. Pattern from
        /// OnFrameReceived would trigger OnPatternChanged and fire a SetPattern command
        /// right back at the device for state it just told us it's already in.</summary>
        private bool _applyingDeviceState;

        /// <summary>True while the local animation timer is driving Led0On..Led3On for
        /// display. Same purpose as _applyingDeviceState but for a different source -
        /// without it, every animation step would fire SendManualLeds and spam the
        /// device with commands for LED motion it's already producing on its own.</summary>
        private bool _animatingLocally;

        private readonly DispatcherTimer _animationTimer = new();
        private readonly Random _animationRng = new();
        private int _animationStep;

        public BlinkyLedViewModel(FrameDispatcher dispatcher) : base(dispatcher)
        {
            _animationTimer.Tick += OnAnimationTick;
        }

        [RelayCommand]
        private void Toggle() => SendCommand(new byte[] { CmdSetState, (byte)(IsOn ? 0 : 1) });

        [RelayCommand]
        private void ApplyBlinkRate()
        {
            ushort rate = (ushort)BlinkRateMs;
            SendCommand(new byte[] { CmdSetBlinkRateMs, (byte)(rate & 0xFF), (byte)(rate >> 8) });
        }

        partial void OnPatternChanged(BlinkyLedPattern value)
        {
            if (_applyingDeviceState) return;
            SendCommand(new byte[] { CmdSetPattern, (byte)value });
        }

        partial void OnReverseChanged(bool value)
        {
            if (_applyingDeviceState) return;
            SendCommand(new byte[] { CmdSetDirection, (byte)(value ? 1 : 0) });
        }

        partial void OnLed0OnChanged(bool value) => SendManualLeds();
        partial void OnLed1OnChanged(bool value) => SendManualLeds();
        partial void OnLed2OnChanged(bool value) => SendManualLeds();
        partial void OnLed3OnChanged(bool value) => SendManualLeds();

        /// <summary>Sends the current manual LED mask. Sending this at all is also how
        /// the device switches into Manual mode - there's no separate "enter manual
        /// mode" command, so these controls stay enabled regardless of Mode.</summary>
        private void SendManualLeds()
        {
            if (_applyingDeviceState || _animatingLocally) return;
            byte mask = (byte)((Led0On ? 0x01 : 0) | (Led1On ? 0x02 : 0) | (Led2On ? 0x04 : 0) | (Led3On ? 0x08 : 0));
            SendCommand(new byte[] { CmdSetManualLeds, mask });
        }

        protected override void OnFrameReceived(ProtocolFrame frame)
        {
            if (frame.Type != MsgType.Response || frame.Payload.Length < 7)
                return;

            _applyingDeviceState = true;
            try
            {
                IsOn = frame.Payload[0] != 0;
                Mode = (BlinkyLedMode)frame.Payload[1];
                Pattern = (BlinkyLedPattern)frame.Payload[2];
                Reverse = frame.Payload[3] != 0;
                BlinkRateMs = frame.Payload[4] | (frame.Payload[5] << 8);

                byte mask = frame.Payload[6];
                Led0On = (mask & 0x01) != 0;
                Led1On = (mask & 0x02) != 0;
                Led2On = (mask & 0x04) != 0;
                Led3On = (mask & 0x08) != 0;
            }
            finally
            {
                _applyingDeviceState = false;
            }

            UpdateAnimationState();
        }

        /// <summary>(Re)starts, reconfigures, or stops the local LED animation to match
        /// the last known device state. Called after every state snapshot, since any of
        /// enabled/mode/pattern/reverse/rate could have just changed.</summary>
        private void UpdateAnimationState()
        {
            _animationTimer.Stop();

            if (!IsOn)
            {
                // Module disabled: no LED should read as lit, matching what the
                // physical board would look like powered off.
                SetLocalLeds(0);
                return;
            }

            if (Mode == BlinkyLedMode.Manual)
                return; // held exactly at the device-echoed manual mask - nothing to animate

            _animationStep = 0;
            _animationTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(20, BlinkRateMs));
            _animationTimer.Start();
        }

        private void OnAnimationTick(object? sender, EventArgs e)
        {
            _animationStep++;
            SetLocalLeds(ComputeAnimatedMask());
        }

        /// <summary>Which of the 4 LEDs should be lit on this animation step, for the
        /// current Pattern/Reverse - mirrors the board-level behavior the firmware
        /// session described (Bounce/Chase/All/Random).</summary>
        private byte ComputeAnimatedMask() => Pattern switch
        {
            BlinkyLedPattern.All => (_animationStep % 2 == 0) ? (byte)0x0F : (byte)0x00,
            BlinkyLedPattern.Random => (byte)(1 << _animationRng.Next(4)),
            BlinkyLedPattern.Chase => (byte)(1 << ChaseIndex()),
            _ => (byte)(1 << BounceIndex()), // Bounce
        };

        private int ChaseIndex()
        {
            int i = _animationStep % 4;
            return Reverse ? 3 - i : i;
        }

        private int BounceIndex()
        {
            // Ping-pongs across the 4 LEDs on a period-6 cycle: 0,1,2,3,2,1,0,1,2,3...
            int i = _animationStep % 6;
            int position = i <= 3 ? i : 6 - i;
            return Reverse ? 3 - position : position;
        }

        private void SetLocalLeds(byte mask)
        {
            _animatingLocally = true;
            try
            {
                Led0On = (mask & 0x01) != 0;
                Led1On = (mask & 0x02) != 0;
                Led2On = (mask & 0x04) != 0;
                Led3On = (mask & 0x08) != 0;
            }
            finally
            {
                _animatingLocally = false;
            }
        }

        public override void Detach()
        {
            _animationTimer.Stop();
            base.Detach();
        }
    }
}
