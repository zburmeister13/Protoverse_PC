using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProtoVerseApp.Models.Registry
{
    /// <summary>On-disk shape of the registry file: a free-text comment (JSON has no
    /// native comment syntax; "$comment" is a widely-used convention, e.g. JSON
    /// Schema uses the same key) plus the entries themselves.</summary>
    public record ProtoModRegistryDocument(
        [property: JsonPropertyName("$comment")] string? Comment,
        [property: JsonPropertyName("entries")] IReadOnlyList<ProtoModRegistryEntry> Entries);

    /// <summary>
    /// Reads a registry JSON file (see <see cref="ProtoModRegistryEntry"/> for the
    /// shape) into plain data. Deliberately does not validate - see
    /// <see cref="ProtoModRegistryValidator"/> - so a loader used for the stress test
    /// (which deliberately feeds it malformed/colliding synthetic data to prove the
    /// validator catches it) never has to fight the loader itself rejecting the file
    /// first.
    /// </summary>
    public static class ProtoModRegistryLoader
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public static ProtoModRegistryDocument LoadFromFile(string path)
        {
            var json = File.ReadAllText(path);
            return LoadFromJson(json);
        }

        public static ProtoModRegistryDocument LoadFromJson(string json) =>
            JsonSerializer.Deserialize<ProtoModRegistryDocument>(json, Options)
            ?? new ProtoModRegistryDocument(null, new List<ProtoModRegistryEntry>());
    }
}
