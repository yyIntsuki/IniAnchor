using IniAnchor.Core.Models;

namespace IniAnchor.Core.Applying;

/// <summary>Per-file result of an Apply run: one entry per watched key, plus a file-level error if the file itself couldn't be read/written.</summary>
public class ApplyFileResult
{
    public WatchedFile File { get; }

    public IReadOnlyList<ApplyKeyResult> KeyResults { get; }

    /// <summary>Set when the file itself couldn't be read or written; null on a normal run (even one with per-key NotFound/Duplicate results).</summary>
    public string? FileErrorMessage { get; }

    public ApplyFileResult(WatchedFile file, IReadOnlyList<ApplyKeyResult> keyResults, string? fileErrorMessage)
    {
        File = file;
        KeyResults = keyResults;
        FileErrorMessage = fileErrorMessage;
    }

    public bool AnyApplied => KeyResults.Any(r => r.Outcome == ApplyKeyOutcome.Applied);
}
