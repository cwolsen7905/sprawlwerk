using System.Collections.Generic;
using System.Text.Json;
using Godot;
using SprawlWerk.Sim.Content;

namespace SprawlWerk;

/// <summary>
/// The content packs the game runs on, read once through Godot's file access
/// (the simulation never opens files): <c>res://content/base</c>, its
/// <c>*.sim.json</c> types for the simulation and its <c>*.art.json</c> files
/// for the views. If the base pack fails to load, the errors go to the log and
/// <see cref="Content"/> is null: the game still runs on built-in numbers, but
/// nothing grows.
/// </summary>
public static class GameContent
{
    private const string BaseDir = "res://content/base";

    private static bool _loaded;
    private static ContentSet? _content;
    private static readonly Dictionary<string, string[]> _models = new();

    public static ContentSet? Content
    {
        get
        {
            EnsureLoaded();
            return _content;
        }
    }

    /// <summary>Model files a type may be drawn with (one is picked per tile); empty when it has no art.</summary>
    public static IReadOnlyList<string> ModelsFor(string typeId)
    {
        EnsureLoaded();
        return _models.TryGetValue(typeId, out var models) ? models : [];
    }

    private static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;

        var manifest = Read($"{BaseDir}/pack.json");
        if (manifest is null) return;
        var types = new List<SourceFile>();
        var arts = new List<SourceFile>();
        foreach (var name in DirAccess.GetFilesAt($"{BaseDir}/types"))
        {
            if (name.EndsWith(".sim.json") && Read($"{BaseDir}/types/{name}") is { } sim) types.Add(sim);
            else if (name.EndsWith(".art.json") && Read($"{BaseDir}/types/{name}") is { } art) arts.Add(art);
        }

        var result = ContentLoader.Load([new PackSource(manifest.Value, types)]);
        foreach (var line in result.Log) GD.Print($"content: {line}");
        foreach (var error in result.Errors) GD.PushError($"content: {error}");
        _content = result.Content;
        if (_content is null) return;

        foreach (var art in arts) ReadArt(art);
    }

    private static SourceFile? Read(string path)
    {
        if (!FileAccess.FileExists(path))
        {
            GD.PushError($"content: missing {path}");
            return null;
        }
        return new SourceFile(path, FileAccess.GetFileAsString(path));
    }

    // Art is the view's business, so it is read here, leniently: a broken art
    // file costs that type its models (it shows as a bare lot), not the game.
    private static void ReadArt(SourceFile file)
    {
        try
        {
            using var doc = JsonDocument.Parse(file.Text);
            var root = doc.RootElement;
            string id = $"{ContentLoader.BasePack}:{root.GetProperty("id").GetString()}";
            var models = new List<string>();
            foreach (var m in root.GetProperty("models").EnumerateArray())
            {
                var path = m.GetString();
                if (path is not null && ResourceLoader.Exists(path)) models.Add(path);
                else GD.PushWarning($"content: {file.Path}: model {path} not found");
            }
            _models[id] = models.ToArray();
        }
        catch (System.Exception e) when (e is JsonException or KeyNotFoundException or System.InvalidOperationException)
        {
            GD.PushWarning($"content: {file.Path}: {e.Message}");
        }
    }
}
