using System;
using System.Collections.Generic;
using Godot;
using SprawlWerk.Sim;

namespace SprawlWerk;

/// <summary>One file in the saves folder. <see cref="Info"/> is null when the file cannot be read; <see cref="Problem"/> says why.</summary>
public sealed record SaveEntry(string FileName, string Name, long Modified, SaveInfo? Info, string? Problem);

/// <summary>
/// The saves folder, user://saves/*.json. File names always come from
/// <see cref="SaveNames"/> (or from a listing of the folder), never from raw input.
/// Every method reports a problem as text for the player instead of throwing.
/// </summary>
public static class SaveStore
{
    public const string Folder = "user://saves";

    public static string PathFor(string fileName) => $"{Folder}/{fileName}";

    /// <summary>Newest first. Unreadable files are listed too, so the player can delete them.</summary>
    public static List<SaveEntry> List()
    {
        var entries = new List<SaveEntry>();
        using var dir = DirAccess.Open(Folder);
        if (dir is null) return entries;

        foreach (var file in dir.GetFiles())
        {
            if (SaveNames.FromFileName(file) is not { } name) continue;
            long modified = (long)FileAccess.GetModifiedTime(PathFor(file));
            try
            {
                var info = SaveGame.ReadInfo(FileAccess.GetFileAsString(PathFor(file)));
                entries.Add(new SaveEntry(file, name, modified, info, null));
            }
            catch (SaveFormatException e)
            {
                entries.Add(new SaveEntry(file, name, modified, null, e.Message));
            }
        }

        entries.Sort((a, b) => b.Modified != a.Modified
            ? b.Modified.CompareTo(a.Modified)
            : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return entries;
    }

    public static bool Exists(string fileName) => FileAccess.FileExists(PathFor(fileName));

    /// <returns>null on success, otherwise what went wrong.</returns>
    public static string? Write(string fileName, string json)
    {
        DirAccess.MakeDirRecursiveAbsolute(Folder);
        return WriteAtomic(PathFor(fileName), json);
    }

    /// <summary>
    /// Write to a scratch file, then rename it over the real one, so a failed or
    /// interrupted write never leaves a half-written save in place of a good one.
    /// </summary>
    /// <returns>null on success, otherwise what went wrong.</returns>
    public static string? WriteAtomic(string path, string json)
    {
        var temp = SaveNames.TempFileName(path);
        using (var file = FileAccess.Open(temp, FileAccess.ModeFlags.Write))
        {
            if (file is null) return Strings.SaveFailed;
            bool stored = file.StoreString(json);
            file.Close();
            if (!stored) { DirAccess.RemoveAbsolute(temp); return Strings.SaveFailed; }
        }

        if (DirAccess.RenameAbsolute(temp, path) != Error.Ok)
        {
            DirAccess.RemoveAbsolute(temp);
            return Strings.SaveFailed;
        }
        return null;
    }

    /// <returns>The file's text, or null with the reason in <paramref name="problem"/>.</returns>
    public static string? Read(string fileName, out string? problem)
    {
        problem = null;
        if (!Exists(fileName)) { problem = Strings.SaveMissing; return null; }
        return FileAccess.GetFileAsString(PathFor(fileName));
    }

    /// <returns>null on success, otherwise what went wrong.</returns>
    public static string? Delete(string fileName)
    {
        using var dir = DirAccess.Open(Folder);
        if (dir is null || dir.Remove(fileName) != Error.Ok) return Strings.DeleteFailed;
        return null;
    }
}
