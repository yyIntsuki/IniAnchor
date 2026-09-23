namespace IniAnchor.Core.Models;

/// <summary>
/// App preferences (not ini-file state, not the watch list), stored in settings.json.
/// Null means "not saved yet - use the default". Add future settings here.
/// </summary>
public class AppSettings
{
    public int? WindowWidth { get; set; }

    public int? WindowHeight { get; set; }
}
