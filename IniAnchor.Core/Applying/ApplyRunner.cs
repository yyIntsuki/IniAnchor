using IniAnchor.Core.Models;
using IniAnchor.Core.Parsing;

namespace IniAnchor.Core.Applying;

/// <summary>
/// Runs the Apply pipeline described in §5: for each watched file, one read, one index
/// build (inside IniDocument), N lookups, and - if anything is actually eligible to
/// write - one write. Never re-reads or re-writes a file per key.
/// </summary>
public static class ApplyRunner
{
    public static List<ApplyFileResult> ApplyToAll(IEnumerable<WatchedFile> files) =>
        files.Select(f => ApplyToFile(f)).ToList();

    public static ApplyFileResult ApplyToFile(WatchedFile file, DateTime? appliedAtUtc = null)
    {
        appliedAtUtc ??= DateTime.UtcNow;

        if (file.WatchedKeys.Count == 0)
            return new ApplyFileResult(file, new List<ApplyKeyResult>(), null);

        IniDocument document;
        try
        {
            document = IniParser.ParseFile(file.FilePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Can't even read the file - every key for it is a file-level failure.
            var errorResults = file.WatchedKeys
                .Select(k => new ApplyKeyResult(k, ApplyKeyOutcome.FileError))
                .ToList();
            return new ApplyFileResult(file, errorResults, ex.Message);
        }

        // Step 2-3 of §5: one lookup per key (uses IniDocument's cached index), staging
        // value changes in memory without touching disk yet.
        var keyResults = new List<ApplyKeyResult>();
        var anyStaged = false;

        foreach (var key in file.WatchedKeys)
        {
            var lookup = document.LookupKey(key.Section, key.KeyName);

            switch (lookup.Status)
            {
                case KeyLookupStatus.UniqueMatch:
                    document.SetValueAtLine(lookup.MatchingLineIndices[0], key.DesiredValue);
                    keyResults.Add(new ApplyKeyResult(key, ApplyKeyOutcome.Applied));
                    anyStaged = true;
                    break;

                case KeyLookupStatus.NotFound:
                    keyResults.Add(new ApplyKeyResult(key, ApplyKeyOutcome.NotFound));
                    break;

                case KeyLookupStatus.DuplicateMatches:
                default:
                    keyResults.Add(new ApplyKeyResult(key, ApplyKeyOutcome.DuplicateMatches));
                    break;
            }
        }

        if (!anyStaged)
            return new ApplyFileResult(file, keyResults, null);

        // Step 4 of §5: one write for the whole file, regardless of how many keys changed.
        try
        {
            IniWriter.WriteToFile(document, file.FilePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The write itself failed - nothing was actually persisted, so downgrade every
            // "Applied" result (they were only staged in memory) to a file error.
            var downgraded = keyResults
                .Select(r => r.Outcome == ApplyKeyOutcome.Applied
                    ? new ApplyKeyResult(r.Key, ApplyKeyOutcome.FileError)
                    : r)
                .ToList();
            return new ApplyFileResult(file, downgraded, ex.Message);
        }

        // Write succeeded - record what was actually applied (§3.1's LastAppliedValue/AtUtc).
        foreach (var result in keyResults)
        {
            if (result.Outcome != ApplyKeyOutcome.Applied)
                continue;

            result.Key.LastAppliedValue = result.Key.DesiredValue;
            result.Key.LastAppliedAtUtc = appliedAtUtc;
        }

        return new ApplyFileResult(file, keyResults, null);
    }
}
