using System.Text.Json.Serialization;
using IniAnchor.Core.Models;

namespace IniAnchor.Core.Persistence;

/// <summary>
/// Compile-time generated JSON (de)serialization code for the watch list.
/// Needed because the published app is trimmed: in .NET 8, trimming disables the default
/// reflection-based JsonSerializer, so Serialize/Deserialize must go through this context instead.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(List<WatchedFile>))]
internal partial class WatchlistJsonContext : JsonSerializerContext
{
}
