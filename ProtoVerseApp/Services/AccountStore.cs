using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using ProtoVerseApp.Models;

namespace ProtoVerseApp.Services
{
    /// <summary>
    /// Local profiles plus each profile's ProtoMod tracking, persisted to
    /// <c>%AppData%\ProtoVerse\accounts.json</c>.
    ///
    /// EXPLICITLY NOT SECURITY. There are no passwords and no encryption - "signing
    /// in" is picking a name off a list, and every account's data sits in one
    /// world-readable JSON file. That's the intended design (user decision,
    /// 2026-08-31: "no actual security, just for board tracking"), and the point is
    /// separating two people's kits on a shared PC, not keeping anyone out. Don't add
    /// a password field here later without also making it mean something - a fake
    /// login that looks real is worse than an obviously-informal one.
    ///
    /// APP-SIDE ONLY, AND NOTHING IS SENT TO FIRMWARE. Per the earlier decision
    /// (CHANGELOG 45), this data belongs to a person rather than to a ProtoCore, and
    /// no wire-protocol traffic is involved in maintaining it - the app already
    /// receives every PresenceReport and simply records what it already sees.
    ///
    /// Two different questions are tracked per module, and they must not be conflated:
    ///   - Has this account ever had it plugged in? (observed, automatic)
    ///   - Does this person say it's theirs? (<see cref="KitStatus"/>, only ever set
    ///     by the user clicking something)
    /// Seeing a borrowed board must never silently claim the user owns it.
    ///
    /// Every file operation degrades to "no accounts" rather than throwing: this is a
    /// convenience layer over a cosmetic UI distinction, and losing it must never be
    /// able to take down a hot-swap rebuild or the app.
    /// </summary>
    public class AccountStore
    {
        private readonly string _filePath;
        private readonly string _legacyHistoryPath;
        private AccountsDocument _document = new();

        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        /// <summary>Raised whenever the signed-in account changes, or its module data
        /// does, so the Library can repaint.</summary>
        public event Action? Changed;

        public IReadOnlyList<UserAccount> Accounts => _document.Accounts;

        public UserAccount? ActiveAccount =>
            _document.Accounts.FirstOrDefault(a => a.Id == _document.ActiveAccountId);

        public bool IsSignedIn => ActiveAccount != null;

        /// <summary>Set when the last load or save failed, so the UI can say so rather
        /// than showing someone an emptier library than they actually have.</summary>
        public string? LastError { get; private set; }

        public AccountStore(string? filePath = null)
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ProtoVerse");

            _filePath = filePath ?? Path.Combine(directory, "accounts.json");
            _legacyHistoryPath = Path.Combine(directory, "module-history.json");

            Load();
        }

        private void Load()
        {
            try
            {
                if (!File.Exists(_filePath))
                    return;

                _document = JsonSerializer.Deserialize<AccountsDocument>(
                    File.ReadAllText(_filePath), SerializerOptions) ?? new AccountsDocument();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                // Deliberately does NOT delete the bad file - overwriting it on the next
                // save is destructive enough without also removing the evidence first.
                _document = new AccountsDocument();
                LastError = $"Couldn't read accounts ({ex.GetType().Name}) - starting fresh.";
            }
        }

        private void Save()
        {
            try
            {
                var directory = Path.GetDirectoryName(_filePath);
                if (directory != null)
                    Directory.CreateDirectory(directory);

                File.WriteAllText(_filePath, JsonSerializer.Serialize(_document, SerializerOptions));
                LastError = null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Read-only or roaming profile, disk full. The in-memory state still
                // works for this session; it just won't survive a restart.
                LastError = $"Couldn't save accounts ({ex.GetType().Name}) - changes won't persist.";
            }
        }

        /// <summary>Creates a profile and signs into it. Names are not unique keys (the
        /// id is), so two people called "Sam" are allowed - but a name already in use is
        /// rejected anyway, because a sign-in list with two identical entries is
        /// unusable.</summary>
        public UserAccount? CreateAccount(string displayName)
        {
            displayName = displayName.Trim();
            if (displayName.Length == 0 || NameExists(displayName))
                return null;

            var account = new UserAccount { DisplayName = displayName };

            // First account created on a machine that already has pre-accounts tracking
            // inherits it, rather than that history being silently orphaned. Only the
            // first: there's no way to know which person later accounts belong to.
            if (_document.Accounts.Count == 0)
                ImportLegacyHistory(account);

            _document.Accounts.Add(account);
            _document.ActiveAccountId = account.Id;
            Save();
            Changed?.Invoke();
            return account;
        }

        public bool NameExists(string displayName) =>
            _document.Accounts.Any(a => string.Equals(a.DisplayName, displayName.Trim(),
                StringComparison.CurrentCultureIgnoreCase));

        public void SignIn(string accountId)
        {
            if (_document.Accounts.All(a => a.Id != accountId))
                return;

            _document.ActiveAccountId = accountId;
            Save();
            Changed?.Invoke();
        }

        public void SignOut()
        {
            _document.ActiveAccountId = null;
            Save();
            Changed?.Invoke();
        }

        /// <summary>Deletes a profile and everything it tracked. Signs out if it was the
        /// active one.</summary>
        public void DeleteAccount(string accountId)
        {
            var account = _document.Accounts.FirstOrDefault(a => a.Id == accountId);
            if (account == null)
                return;

            _document.Accounts.Remove(account);
            if (_document.ActiveAccountId == accountId)
                _document.ActiveAccountId = null;

            Save();
            Changed?.Invoke();
        }

        /// <summary>Records that these module types are plugged in right now, against
        /// the signed-in account. No-ops when signed out - tracking is per person, so
        /// there's nobody to attribute a sighting to. Never touches
        /// <see cref="KitStatus"/>: being plugged in is not a claim of ownership.</summary>
        public void RecordSightings(IEnumerable<ProtoModId> moduleIds)
        {
            var account = ActiveAccount;
            if (account == null)
                return;

            var now = DateTimeOffset.UtcNow;
            bool changed = false;

            foreach (var id in moduleIds.Distinct())
            {
                // ProtoModId.Unknown means "ProtoCore saw something it can't identify" -
                // not a type, and recording it would leave a permanently meaningless
                // entry that the user would then be asked to claim ownership of.
                if (id is ProtoModId.None or ProtoModId.Unknown or ProtoModId.Core or ProtoModId.Broadcast)
                    continue;

                var record = account.Modules.FirstOrDefault(m => m.ModuleId == (ushort)id);
                if (record == null)
                {
                    account.Modules.Add(new ModuleRecord
                    {
                        ModuleId = (ushort)id,
                        CircuitCode = ProtoModBoardCatalog.Entries.FirstOrDefault(e => e.Id == id)?.CircuitCode,
                        FirstSeenUtc = now,
                        LastSeenUtc = now
                    });
                    changed = true;
                }
                else if (record.LastSeenUtc.UtcDateTime.Date != now.UtcDateTime.Date)
                {
                    // Only rewrite when last-seen moves to a new day. A hot-swap fires
                    // PresenceReports in bursts and none of them need their own save.
                    record.LastSeenUtc = now;
                    changed = true;
                }
            }

            if (changed)
            {
                Save();
                Changed?.Invoke();
            }
        }

        /// <summary>Records the user's answer to "is this one yours?". Works for a
        /// module that's never been plugged in, so someone can mark a board they own but
        /// haven't connected yet - and is always reversible, which is the whole point:
        /// a board seen once must not be stuck in someone's kit forever.</summary>
        public void SetKitStatus(ProtoModId moduleId, KitStatus status)
        {
            var account = ActiveAccount;
            if (account == null)
                return;

            var record = account.Modules.FirstOrDefault(m => m.ModuleId == (ushort)moduleId);
            if (record == null)
            {
                record = new ModuleRecord
                {
                    ModuleId = (ushort)moduleId,
                    CircuitCode = ProtoModBoardCatalog.Entries.FirstOrDefault(e => e.Id == moduleId)?.CircuitCode,
                    // Never plugged in, so there's no sighting to claim. Left at
                    // default(DateTimeOffset) rather than "now", which would fabricate a
                    // connection that didn't happen; the Library reads FirstSeenUtc's
                    // default as "never seen".
                };
                account.Modules.Add(record);
            }

            record.KitStatus = status;
            Save();
            Changed?.Invoke();
        }

        public ModuleRecord? FindRecord(ProtoModId moduleId) =>
            ActiveAccount?.Modules.FirstOrDefault(m => m.ModuleId == (ushort)moduleId);

        /// <summary>Folds a pre-accounts <c>module-history.json</c> into an account, so
        /// upgrading doesn't silently lose what the app had already tracked. The old
        /// file is renamed rather than deleted - if the import goes wrong, the data is
        /// still on disk.</summary>
        private void ImportLegacyHistory(UserAccount account)
        {
            try
            {
                if (!File.Exists(_legacyHistoryPath))
                    return;

                using var doc = JsonDocument.Parse(File.ReadAllText(_legacyHistoryPath));
                if (!doc.RootElement.TryGetProperty("sightings", out var sightings))
                    return;

                foreach (var sighting in sightings.EnumerateArray())
                {
                    if (!sighting.TryGetProperty("moduleId", out var idElement) ||
                        !idElement.TryGetUInt16(out var moduleId))
                        continue;

                    account.Modules.Add(new ModuleRecord
                    {
                        ModuleId = moduleId,
                        CircuitCode = sighting.TryGetProperty("circuitCode", out var code) ? code.GetString() : null,
                        FirstSeenUtc = sighting.TryGetProperty("firstSeenUtc", out var first)
                            ? first.GetDateTimeOffset() : DateTimeOffset.UtcNow,
                        LastSeenUtc = sighting.TryGetProperty("lastSeenUtc", out var last)
                            ? last.GetDateTimeOffset() : DateTimeOffset.UtcNow,
                        // Imported sightings are observations, not ownership claims -
                        // the user still gets asked about each one.
                        KitStatus = KitStatus.Unanswered
                    });
                }

                File.Move(_legacyHistoryPath, _legacyHistoryPath + ".migrated", overwrite: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                // Losing a pre-accounts history import is not worth failing account
                // creation over - the user just starts with an empty library.
                LastError = $"Couldn't import previous tracking data ({ex.GetType().Name}).";
            }
        }
    }
}
