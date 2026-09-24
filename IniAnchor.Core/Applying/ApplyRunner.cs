using IniAnchor.Core.Models;
using IniAnchor.Core.Parsing;

namespace IniAnchor.Core.Applying;

/// <summary>
/// Runs the Apply pipeline described in §5. Entries for the same ini file (the same file
/// in several folders) are merged first, so each file gets one read, one index build
/// (inside IniDocument), N lookups, and - only if at least one value actually differs -
/// one write. Never re-reads or re-writes a file per key or per entry.
///
/// When several entries set the same key, the LAST entry in the given order wins (callers
/// pass Watchlist.FilesInApplyOrder, so that's the folder lowest in the sidebar). Losing
/// keys with a different value are reported as <see cref="ApplyKeyOutcome.Overridden"/>.
/// </summary>
public static class ApplyRunner
{
    /// <summary>Applies every entry; returns one result per entry, in the given order.</summary>
    public static List<ApplyFileResult> ApplyToAll(IEnumerable<WatchedFile> files)
    {
        var entries = files.ToList();

        var resultByEntry = new Dictionary<WatchedFile, ApplyFileResult>();

        // GroupBy keeps the original order within each group, so "last wins" still means
        // "lowest in the sidebar".
        foreach (var sameFile in entries.GroupBy(f => f.FilePath, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var result in ApplyToSameFile(sameFile.ToList()))
                resultByEntry[result.File] = result;
        }

        return entries.Select(e => resultByEntry[e]).ToList();
    }

    /// <summary>Applies a single entry (the one-entry case of <see cref="ApplyToAll"/>).</summary>
    public static ApplyFileResult ApplyToFile(WatchedFile file) =>
        ApplyToSameFile(new List<WatchedFile> { file })[0];

    /// <summary>All given entries point at the same ini file.</summary>
    private static List<ApplyFileResult> ApplyToSameFile(List<WatchedFile> entries)
    {
        var filePath = entries[0].FilePath;

        if (entries.All(e => e.WatchedKeys.Count == 0))
            return entries.Select(e => new ApplyFileResult(e, new List<ApplyKeyResult>(), null)).ToList();

        IniDocument document;
        try
        {
            document = IniParser.ParseFile(filePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Can't even read the file - every key of every entry is a file-level failure.
            return entries.Select(e => new ApplyFileResult(e,
                e.WatchedKeys.Select(k => new ApplyKeyResult(k, ApplyKeyOutcome.FileError)).ToList(),
                ex.Message)).ToList();
        }

        // Pass 1: one lookup per key (uses IniDocument's cached index). For every line that
        // a key matches uniquely, remember which key wins: the last one, since later
        // entries overwrite earlier ones in this dictionary.
        var lookups = new List<(WatchedFile Entry, WatchedKey Key, KeyLookupResult Lookup)>();
        var winnerByLine = new Dictionary<int, WatchedKey>();

        foreach (var entry in entries)
        {
            foreach (var key in entry.WatchedKeys)
            {
                var lookup = document.LookupKey(key.Section, key.KeyName);
                lookups.Add((entry, key, lookup));

                if (lookup.Status == KeyLookupStatus.UniqueMatch)
                    winnerByLine[lookup.MatchingLineIndices[0]] = key;
            }
        }

        // Pass 2: stage each winning key's desired state in memory, but only if the line
        // doesn't already match it: on = active with the desired value (exact comparison,
        // same as IniLine.SetValue); off = commented out (its value is left alone). If
        // nothing differs, the file isn't written at all - so its timestamp is untouched,
        // file watchers aren't triggered, and a locked/read-only file that's already correct
        // doesn't produce a FileError.
        var outcomeByLine = new Dictionary<int, ApplyKeyOutcome>();
        var anyStaged = false;

        foreach (var (lineIndex, winner) in winnerByLine)
        {
            var line = document.Lines[lineIndex];
            var alreadyMatches = winner.CommentedOut
                ? line.IsCommentedOut
                : !line.IsCommentedOut && line.Value == winner.DesiredValue;

            if (alreadyMatches)
            {
                outcomeByLine[lineIndex] = ApplyKeyOutcome.AlreadySet;
            }
            else
            {
                if (!winner.CommentedOut)
                    document.SetValueAtLine(lineIndex, winner.DesiredValue);

                document.SetCommentedOutAtLine(lineIndex, winner.CommentedOut);
                outcomeByLine[lineIndex] = ApplyKeyOutcome.Applied;
                anyStaged = true;
            }
        }

        // Pass 3: every key's outcome. A losing key that wants the same as the winner shares
        // the winner's outcome (that's what ends up in the file); otherwise it's Overridden.
        var keyResultsByEntry = entries.ToDictionary(e => e, _ => new List<ApplyKeyResult>());

        foreach (var (entry, key, lookup) in lookups)
        {
            ApplyKeyOutcome outcome;

            switch (lookup.Status)
            {
                case KeyLookupStatus.UniqueMatch:
                    var lineIndex = lookup.MatchingLineIndices[0];
                    var winner = winnerByLine[lineIndex];
                    outcome = winner == key || winner.HasSameDesiredStateAs(key)
                        ? outcomeByLine[lineIndex]
                        : ApplyKeyOutcome.Overridden;
                    break;

                case KeyLookupStatus.NotFound:
                    outcome = ApplyKeyOutcome.NotFound;
                    break;

                case KeyLookupStatus.DuplicateMatches:
                default:
                    outcome = ApplyKeyOutcome.DuplicateMatches;
                    break;
            }

            keyResultsByEntry[entry].Add(new ApplyKeyResult(key, outcome));
        }

        List<ApplyFileResult> Results(string? errorMessage) =>
            entries.Select(e => new ApplyFileResult(e, keyResultsByEntry[e], errorMessage)).ToList();

        if (!anyStaged)
            return Results(null);

        // One write for the whole file, however many keys and entries changed.
        try
        {
            IniWriter.WriteToFile(document, filePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The write itself failed - nothing was actually persisted, so downgrade every
            // "Applied" result (they were only staged in memory) to a file error.
            foreach (var entry in entries)
            {
                keyResultsByEntry[entry] = keyResultsByEntry[entry]
                    .Select(r => r.Outcome == ApplyKeyOutcome.Applied
                        ? new ApplyKeyResult(r.Key, ApplyKeyOutcome.FileError)
                        : r)
                    .ToList();
            }

            return Results(ex.Message);
        }

        return Results(null);
    }
}
