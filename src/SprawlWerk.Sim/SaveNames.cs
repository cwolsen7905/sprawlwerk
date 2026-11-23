using System.Text;

namespace SprawlWerk.Sim;

/// <summary>
/// Turns what a player types into a safe save file name, so the game never
/// builds a path from raw input. Letters, digits, spaces, dashes and
/// underscores are kept; everything else is dropped.
/// </summary>
public static class SaveNames
{
    public const int MaxLength = 40;
    public const string Extension = ".json";

    /// <summary>The cleaned name, or null if nothing usable is left (an empty field, only symbols).</summary>
    public static string? Clean(string? typed)
    {
        if (typed is null) return null;

        var sb = new StringBuilder();
        foreach (char c in typed)
        {
            if (char.IsAsciiLetterOrDigit(c) || c is '-' or '_') sb.Append(c);
            else if (char.IsWhiteSpace(c) && sb.Length > 0 && sb[^1] != ' ') sb.Append(' ');
        }

        var name = sb.ToString().Trim();
        if (name.Length > MaxLength) name = name[..MaxLength].TrimEnd();
        return name.Length == 0 ? null : name;
    }

    /// <summary>The file name for a typed name, or null if it cleans to nothing.</summary>
    public static string? ToFileName(string? typed) => Clean(typed) is { } name ? name + Extension : null;

    /// <summary>The display name for a file name; null if it is not a save file.</summary>
    public static string? FromFileName(string fileName) =>
        fileName.EndsWith(Extension, StringComparison.OrdinalIgnoreCase) && fileName.Length > Extension.Length
            ? fileName[..^Extension.Length]
            : null;
}
