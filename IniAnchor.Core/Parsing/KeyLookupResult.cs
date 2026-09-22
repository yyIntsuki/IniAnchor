namespace IniAnchor.Core.Parsing;

public enum KeyLookupStatus
{
    NotFound,
    UniqueMatch,
    DuplicateMatches
}

/// <summary>
/// The outcome of looking up a (section, key) pair in an <see cref="IniDocument"/>.
/// Only <see cref="KeyLookupStatus.UniqueMatch"/> is safe to apply a new value to.
/// </summary>
public class KeyLookupResult
{
    public KeyLookupStatus Status { get; }

    public IReadOnlyList<int> MatchingLineIndices { get; }

    private KeyLookupResult(KeyLookupStatus status, IReadOnlyList<int> matchingLineIndices)
    {
        Status = status;
        MatchingLineIndices = matchingLineIndices;
    }

    internal static KeyLookupResult From(List<int> matches) => matches.Count switch
    {
        0 => new KeyLookupResult(KeyLookupStatus.NotFound, matches),
        1 => new KeyLookupResult(KeyLookupStatus.UniqueMatch, matches),
        _ => new KeyLookupResult(KeyLookupStatus.DuplicateMatches, matches)
    };
}
