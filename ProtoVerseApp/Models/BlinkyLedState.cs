namespace ProtoVerseApp.Models
{
    /// <summary>BlinkyLed operating mode - echoed back as payload[1] in every
    /// BlinkyLed Command's Response. Manual is entered by sending SetManualLeds;
    /// Animated is re-entered by sending SetPattern.</summary>
    public enum BlinkyLedMode : byte
    {
        Animated = 0,
        Manual = 1
    }

    /// <summary>BlinkyLed animation pattern - echoed back as payload[2] in every
    /// BlinkyLed Command's Response. Only meaningful while Mode is Animated.</summary>
    public enum BlinkyLedPattern : byte
    {
        Bounce = 0,
        Chase = 1,
        All = 2,
        Random = 3
    }
}
