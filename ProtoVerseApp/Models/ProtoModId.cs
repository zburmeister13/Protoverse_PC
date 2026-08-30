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
    /// </summary>
    public enum ProtoModId : byte
    {
        None = 0x00,
        BlinkyLed = 0x01,
        AccelTemp = 0x02,
        ElectronicLoad = 0x03,

        // Reserved for future ProtoMods:
        // SimpleLed        = 0x04,
        // LogicShiftReg    = 0x05,
        // DdsGenerator     = 0x06,

        /// <summary>Addresses ProtoCore itself (not a ProtoMod) - used for slot
        /// identification requests/reports.</summary>
        Core = 0xF0,

        /// <summary>Reserved, not currently used for anything.</summary>
        Broadcast = 0xFF
    }
}
