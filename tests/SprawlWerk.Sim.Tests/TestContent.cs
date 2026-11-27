using SprawlWerk.Sim.Content;

namespace SprawlWerk.Sim.Tests;

/// <summary>Content for tests: the shipped base pack, read from <c>game/content/base</c>, or a variant of it.</summary>
internal static class TestContent
{
    public static string BasePackDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SprawlWerk.sln"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir.FullName, "game", "content", "base");
    }

    /// <summary>The base pack's files; <paramref name="edit"/> may rewrite a type file's text by file name.</summary>
    public static PackSource ReadBasePack(Func<string, string, string>? edit = null)
    {
        var root = BasePackDir();
        SourceFile Read(string path)
        {
            string name = Path.GetRelativePath(root, path);
            string text = File.ReadAllText(path);
            return new SourceFile(name, edit is null ? text : edit(Path.GetFileName(path), text));
        }
        return new PackSource(
            Read(Path.Combine(root, "pack.json")),
            Directory.GetFiles(Path.Combine(root, "types"), "*.sim.json").Order(StringComparer.Ordinal).Select(Read).ToArray());
    }

    public static ContentSet Load(Func<string, string, string>? edit = null)
    {
        var result = ContentLoader.Load([ReadBasePack(edit)]);
        Assert.True(result.Succeeded, string.Join("\n", result.Errors));
        return result.Content!;
    }

    /// <summary>The base pack with one number in one type file replaced.</summary>
    public static ContentSet With(string file, string key, long value) =>
        Load((name, text) => name != file ? text : System.Text.RegularExpressions.Regex.Replace(
            text, $"\"{key}\":\\s*\\d+", $"\"{key}\": {value}"));
}
