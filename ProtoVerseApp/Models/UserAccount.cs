using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ProtoVerseApp.Models
{
    /// <summary>
    /// Whether the user has said a ProtoMod is theirs. Deliberately separate from
    /// whether the app has ever *seen* the board: seeing one proves it was plugged in,
    /// not that it belongs to anyone (a borrowed board is the obvious case). Only the
    /// person can answer that, so this only ever changes because they clicked
    /// something.
    /// </summary>
    public enum KitStatus
    {
        /// <summary>Seen, but the user hasn't said whether it's theirs. The Library
        /// asks.</summary>
        Unanswered,

        /// <summary>User confirmed the board is part of their kit.</summary>
        InKit,

        /// <summary>User said the board isn't theirs - borrowed, a classroom unit, a
        /// friend's. Keeps it out of their kit without pretending it was never seen,
        /// and is always changeable later.</summary>
        NotMine
    }

    /// <summary>One ProtoMod type's record within a single account: when this account
    /// first and last had it plugged in, and what the user has said about owning it.
    /// <paramref name="CircuitCode"/> is informational only - written so the on-disk
    /// file is readable by a person, never read back.</summary>
    public class ModuleRecord
    {
        [JsonPropertyName("moduleId")]
        public ushort ModuleId { get; set; }

        [JsonPropertyName("circuitCode")]
        public string? CircuitCode { get; set; }

        [JsonPropertyName("firstSeenUtc")]
        public DateTimeOffset FirstSeenUtc { get; set; }

        [JsonPropertyName("lastSeenUtc")]
        public DateTimeOffset LastSeenUtc { get; set; }

        [JsonPropertyName("kitStatus")]
        public KitStatus KitStatus { get; set; } = KitStatus.Unanswered;
    }

    /// <summary>
    /// A local profile. NOT a security boundary and not intended to be one - there is
    /// no password, no encryption, and anyone with the file can read or edit every
    /// account in it. Its entire job is to keep one person's board tracking separate
    /// from another's on a shared PC (the classroom case), which is why "signing in"
    /// is just picking a name.
    ///
    /// If this ever needs to become a real account - a server, a login, sync across
    /// machines - the shape here is intentionally close to what that would send: a
    /// stable id, a display name, and a flat list of per-module records.
    /// </summary>
    public class UserAccount
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = "";

        [JsonPropertyName("createdUtc")]
        public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

        [JsonPropertyName("modules")]
        public List<ModuleRecord> Modules { get; set; } = new();
    }

    /// <summary>On-disk shape for every account on this machine. Versioned from the
    /// start so a later format change can migrate rather than discard people's
    /// tracking.</summary>
    public class AccountsDocument
    {
        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; set; } = 1;

        /// <summary>Who was signed in when the app last closed, so it can restore that
        /// rather than making someone sign in on every launch. Null means signed out.</summary>
        [JsonPropertyName("activeAccountId")]
        public string? ActiveAccountId { get; set; }

        [JsonPropertyName("accounts")]
        public List<UserAccount> Accounts { get; set; } = new();
    }
}
