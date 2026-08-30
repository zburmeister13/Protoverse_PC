namespace ProtoVerseApp.Models
{
    /// <summary>
    /// What kind of frame this is. Every frame carries one of these regardless of
    /// which ProtoMod it's addressed to - the ProtoModId field says *who*, MsgType
    /// says *what kind of message*.
    /// </summary>
    public enum MsgType : byte
    {
        /// <summary>App -> ProtoCore: do something / set something.</summary>
        Command = 0x01,

        /// <summary>ProtoCore -> App: reply to a Command.</summary>
        Response = 0x02,

        /// <summary>App -> ProtoCore (addressed to Core): "what's plugged in?"</summary>
        PresenceRequest = 0x03,

        /// <summary>ProtoCore -> App (addressed to Core): which slots/ProtoMod IDs
        /// are currently present. Sent in reply to a PresenceRequest, and can also
        /// be sent unsolicited if ProtoCore detects a change.</summary>
        PresenceReport = 0x04,

        /// <summary>ProtoCore -> App: bulk/streamed data (e.g. a frequency sweep or
        /// waveform capture) that doesn't fit the simple command/response shape.</summary>
        StreamData = 0x05,

        /// <summary>Either direction: something went wrong: payload[0] is an
        /// application-defined error code.</summary>
        Error = 0xFF
    }
}
