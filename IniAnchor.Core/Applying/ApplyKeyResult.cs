using IniAnchor.Core.Models;

namespace IniAnchor.Core.Applying;

/// <summary>
/// What happened to a single watched key during an Apply run.
/// </summary>
public enum ApplyKeyOutcome
{
    /// <summary>The key was unique in its section and its value was written.</summary>
    Applied,

    /// <summary>The key wasn't found in the file - nothing was written for it (§3.3).</summary>
    NotFound,

    /// <summary>The key appeared more than once - too ambiguous to write safely (§3.3).</summary>
    DuplicateMatches,

    /// <summary>The file couldn't be read or written (locked, permissions, missing, etc.).</summary>
    FileError
}

/// <summary>Per-key result of an Apply run, for feeding back into the UI (§5 step 5).</summary>
public class ApplyKeyResult
{
    public WatchedKey Key { get; }

    public ApplyKeyOutcome Outcome { get; }

    public ApplyKeyResult(WatchedKey key, ApplyKeyOutcome outcome)
    {
        Key = key;
        Outcome = outcome;
    }
}
