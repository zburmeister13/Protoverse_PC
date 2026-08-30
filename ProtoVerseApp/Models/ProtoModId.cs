namespace ProtoVerseApp.Models
{
    /// <summary>
    /// Fixed vocabulary of ProtoMod type IDs. This value must match the constant
    /// baked into the ProtoCore firmware for each module type - it is the shared
    /// "language" both sides speak, the same way the EEPROM on each ProtoMod
    /// reports its type to ProtoCore.
    ///
    /// Add a new entry here every time a new ProtoMod type is introduced, and add
    /// the matching entry on the firmware side at the same time.
    ///
    /// Widened from a single byte to a 2-byte (little-endian on the wire) value on
    /// 2026-08-30, agreed cross-session with the firmware Claude session, to support
    /// a product catalog expected to eventually exceed 1,000 ProtoMod types (a byte
    /// only offered ~253 usable values after reserved IDs). Reserved IDs
    /// (None/Core/Broadcast) were moved to the top of the range at the same time so
    /// they read as obviously-not-a-catalog-entry rather than looking like they
    /// could be real ProtoMod types.
    /// </summary>
    public enum ProtoModId : ushort
    {
        None = 0x0000,
        BlinkyLed = 0x0001,
        AccelTemp = 0x0002,
        ElectronicLoad = 0x0003,

        // 0x0004..0xFFEF reserved for the ProtoMod catalog.

        /// <summary>Addresses ProtoCore itself (not a ProtoMod) - used for slot
        /// identification requests/reports.</summary>
        Core = 0xFFF0,

        /// <summary>Reserved, not currently used for anything.</summary>
        Broadcast = 0xFFFF
    }
}
