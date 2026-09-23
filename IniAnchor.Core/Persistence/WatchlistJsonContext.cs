using System.Text.Json.Serialization;
using IniAnchor.Core.Models;

namespace IniAnchor.Core.Persistence;

/// <summary>
/// Compile-time generated JSON (de)serialization code for the app's JSON files (watch list
/// and settings). Needed because the published app is trimmed: in .NET 8, trimming disables the default
/// reflection-based JsonSerializer, so Serialize/Deserialize must go through this context instead.
/// Any new type saved as JSON must get its own [JsonSerializable] line here.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(List<WatchedFile>))]
[JsonSerializable(typeof(AppSettings))]
internal partial class WatchlistJsonContext : JsonSerializerContext
{
}
