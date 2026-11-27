using System.Text.Json;
using System.Text.RegularExpressions;

namespace SprawlWerk.Sim.Content;

/// <summary>What loading packs gave: the content, or every problem found.</summary>
public sealed record ContentLoadResult(ContentSet? Content, IReadOnlyList<ContentError> Errors, IReadOnlyList<string> Log)
{
    public bool Succeeded => Content is not null;
}

/// <summary>
/// Turns content packs into a <see cref="ContentSet"/>. Strict: an unknown
/// key, a bad value, a duplicate id or a broken reference fails the whole
/// load and is reported with its file and JSON path. Order never depends on
/// the order packs or files were handed in.
/// </summary>
public static partial class ContentLoader
{
    /// <summary>The pack every other pack builds on; it must be present.</summary>
    public const string BasePack = "base";

    /// <summary>The only schema version this game reads, for packs and types alike.</summary>
    public const int SchemaVersion = 1;

    /// <summary>Largest footprint side, in tiles.</summary>
    public const int MaxSize = 8;

    [GeneratedRegex("^[a-z][a-z0-9_]{0,47}$")]
    private static partial Regex NamePattern();

    private static readonly Dictionary<string, TypeRole> Roles = new()
    {
        ["zone"] = TypeRole.Zone, ["network"] = TypeRole.Network, ["building"] = TypeRole.Building,
    };

    private static readonly Dictionary<string, ZoneKind> Zones = new()
    {
        ["residential"] = ZoneKind.Residential, ["commercial"] = ZoneKind.Commercial, ["industrial"] = ZoneKind.Industrial,
    };

    private static readonly Dictionary<string, NetworkKind> Networks = new()
    {
        ["road"] = NetworkKind.Road, ["power"] = NetworkKind.Power,
    };

    private static readonly Dictionary<string, Density> Densities = new()
    {
        ["low"] = Density.Low, ["medium"] = Density.Medium, ["high"] = Density.High,
    };

    private const long MaxMoney = 1_000_000_000;
    private const int MaxAmount = 1_000_000;

    public static ContentLoadResult Load(IEnumerable<PackSource> sources)
    {
        var errors = new List<ContentError>();
        var log = new List<string>();

        // 1. Manifests.
        var packs = new List<(PackInfo Info, PackSource Source)>();
        foreach (var source in sources)
        {
            var info = ReadManifest(source.Manifest, errors);
            if (info is null) continue;
            if (packs.Any(p => p.Info.Id == info.Id))
                errors.Add(new ContentError(source.Manifest.Path, "$.id", $"pack '{info.Id}' is given more than once"));
            else
                packs.Add((info, source));
        }
        if (errors.Count > 0) return Fail(errors, log);
        if (!packs.Any(p => p.Info.Id == BasePack))
            return Fail([new ContentError("(packs)", "", $"the '{BasePack}' pack is missing")], log);

        // 2. Load order: base, then dependencies before dependants, ties by id.
        var ordered = Order(packs, errors);
        if (ordered is null) return Fail(errors, log);

        // 3. Types, pack by pack, so an override always follows what it replaces.
        var types = new Dictionary<string, (BuildingType Type, string File)>(StringComparer.Ordinal);
        foreach (var (info, source) in ordered)
        {
            foreach (var file in source.Types.OrderBy(f => f.Path, StringComparer.Ordinal))
            {
                var read = ReadType(file, info.Id, errors);
                if (read is not { } entry) continue;
                var (type, overrides) = entry;
                if (overrides is not null)
                {
                    if (!types.ContainsKey(overrides))
                    {
                        errors.Add(new ContentError(file.Path, "$.overrides", $"'{overrides}' is not defined by an earlier pack"));
                        continue;
                    }
                    types[overrides] = (type with { Id = overrides }, file.Path);
                    log.Add($"{info.Id} overrides {overrides} ({file.Path})");
                }
                else if (types.TryGetValue(type.Id, out var existing))
                {
                    errors.Add(new ContentError(file.Path, "$.id",
                        $"'{type.Id}' is already defined in {existing.File}; to replace it, use \"overrides\""));
                }
                else
                {
                    types[type.Id] = (type, file.Path);
                }
            }
        }

        // 4. References between types.
        var packById = ordered.ToDictionary(p => p.Info.Id, p => p.Info, StringComparer.Ordinal);
        foreach (var (id, (type, file)) in types.OrderBy(t => t.Key, StringComparer.Ordinal))
            CheckFallback(type, file, types, packById, errors);

        if (errors.Count > 0) return Fail(errors, log);
        var content = new ContentSet(ordered.Select(p => p.Info).ToArray(), types.Values.Select(t => t.Type));
        return new ContentLoadResult(content, [], log);
    }

    private static ContentLoadResult Fail(List<ContentError> errors, List<string> log) => new(null, errors, log);

    private static JsonElement? Parse(SourceFile file, List<ContentError> errors)
    {
        try
        {
            using var doc = JsonDocument.Parse(file.Text, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
            if (doc.RootElement.ValueKind == JsonValueKind.Object) return doc.RootElement.Clone();
            errors.Add(new ContentError(file.Path, "$", "must be a JSON object"));
        }
        catch (JsonException e)
        {
            errors.Add(new ContentError(file.Path, "", $"is not valid JSON (line {e.LineNumber + 1}): {e.Message}"));
        }
        return null;
    }

    private static bool CheckSchema(JsonFields fields)
    {
        var version = fields.Integer("schemaVersion", 1, int.MaxValue, required: true);
        if (version is null or SchemaVersion) return version is not null;
        fields.Error("schemaVersion", version > SchemaVersion
            ? $"is {version}; this game reads {SchemaVersion}. The pack needs a newer game"
            : $"is {version}; this game reads {SchemaVersion}");
        return false;
    }

    private static bool CheckName(JsonFields fields, string key, string? name)
    {
        if (name is null || NamePattern().IsMatch(name)) return true;
        fields.Error(key, $"'{name}' must be lowercase letters, digits and _, starting with a letter");
        return false;
    }

    private static PackInfo? ReadManifest(SourceFile file, List<ContentError> errors)
    {
        if (Parse(file, errors) is not { } root) return null;
        int before = errors.Count;
        var f = new JsonFields(root, file.Path, "$", errors);
        CheckSchema(f);
        var id = f.String("id", required: true);
        CheckName(f, "id", id);
        f.String("name");
        var versionText = f.String("version", required: true);
        Version? version = null;
        if (versionText is not null && !(Version.TryParse(versionText, out version) && version.Build >= 0 && version.Revision < 0))
            f.Error("version", $"'{versionText}' must be three numbers, like 1.0.0");
        var license = f.String("license", required: true);
        var authors = f.Strings("authors") ?? [];
        var depends = f.Strings("dependsOn") ?? [];
        for (int i = 0; i < depends.Count; i++) CheckName(f, $"dependsOn[{i}]", depends[i]);
        // Recorded now, enforced by the pack manager (S-38).
        var gameVersion = f.String("gameVersion");
        f.RejectUnknown();
        if (errors.Count > before || id is null || version is null || license is null) return null;
        return new PackInfo(id, version, license, authors, depends, gameVersion);
    }

    private static List<(PackInfo Info, PackSource Source)>? Order(
        List<(PackInfo Info, PackSource Source)> packs, List<ContentError> errors)
    {
        var byId = packs.ToDictionary(p => p.Info.Id, StringComparer.Ordinal);
        foreach (var (info, source) in packs)
            foreach (var dep in info.DependsOn.Where(d => !byId.ContainsKey(d)))
                errors.Add(new ContentError(source.Manifest.Path, "$.dependsOn", $"needs pack '{dep}', which is not loaded"));
        if (errors.Count > 0) return null;

        // Kahn's algorithm; every pack implicitly depends on base.
        var ordered = new List<(PackInfo, PackSource)>();
        var done = new HashSet<string>(StringComparer.Ordinal);
        while (ordered.Count < packs.Count)
        {
            var next = packs
                .Where(p => !done.Contains(p.Info.Id))
                .Where(p => p.Info.Id == BasePack || done.Contains(BasePack))
                .Where(p => p.Info.DependsOn.All(done.Contains))
                .OrderBy(p => p.Info.Id, StringComparer.Ordinal)
                .FirstOrDefault();
            if (next.Info is null)
            {
                var stuck = packs.Where(p => !done.Contains(p.Info.Id)).Select(p => p.Info.Id).Order(StringComparer.Ordinal);
                errors.Add(new ContentError("(packs)", "", $"packs depend on each other in a loop: {string.Join(", ", stuck)}"));
                return null;
            }
            ordered.Add(next);
            done.Add(next.Info.Id);
        }
        return ordered;
    }

    private static (BuildingType Type, string? Overrides)? ReadType(SourceFile file, string pack, List<ContentError> errors)
    {
        if (Parse(file, errors) is not { } root) return null;
        int before = errors.Count;
        var f = new JsonFields(root, file.Path, "$", errors);
        CheckSchema(f);

        var overrides = f.String("overrides");
        var name = f.String("id", required: overrides is null);
        CheckName(f, "id", name);
        if (overrides is not null && name is not null)
            f.Error("id", "must be left out when the type overrides another; it takes the overridden id");
        if (overrides is not null) CheckFullId(f, "overrides", overrides);

        var role = f.Choice("role", Roles, required: true);
        var zone = f.Choice("zone", Zones);
        var network = f.Choice("network", Networks);
        var density = f.Choice("density", Densities);
        var size = f.Pair("size", 1, MaxSize);
        var cost = f.Integer("cost", 0, MaxMoney, required: true);
        var upkeep = f.Integer("upkeep", 0, MaxMoney);
        var residents = f.Integer("residents", 0, MaxAmount);
        var jobs = f.Integer("jobs", 0, MaxAmount);
        var supply = f.Integer("powerSupply", 0, MaxAmount);
        var use = f.Integer("powerUse", 0, MaxAmount);
        var fallback = f.String("fallback");
        if (fallback is not null) CheckFullId(f, "fallback", fallback);
        f.RejectUnknown();

        switch (role)
        {
            case TypeRole.Zone:
                if (zone is null) f.Error("zone", "is required for a zone");
                if (network is not null) f.Error("network", "is only for networks");
                if (size is not null && size != (1, 1)) f.Error("size", "must be [1, 1] for a zone; zones are painted per tile");
                break;
            case TypeRole.Network:
                if (network is null) f.Error("network", "is required for a network");
                if (zone is not null) f.Error("zone", "is not for networks");
                if (size is not null && size != (1, 1)) f.Error("size", "must be [1, 1] for a network");
                break;
            case TypeRole.Building:
                if (network is not null) f.Error("network", "is only for networks");
                // A building with a zone grows there by itself and is privately owned.
                if (zone is not null && cost is > 0) f.Error("cost", "must be 0 for a building that grows on a zone; the city does not pay for it");
                if (zone is not null && upkeep is > 0) f.Error("upkeep", "must be 0 for a building that grows on a zone");
                break;
        }

        if (errors.Count > before || role is null || cost is null) return null;
        var (w, h) = size ?? (1, 1);
        var type = new BuildingType
        {
            Id = name is null ? "" : $"{pack}:{name}",
            Pack = pack,
            Role = role.Value,
            Zone = zone,
            Network = network,
            Density = density,
            Width = w,
            Height = h,
            Cost = cost.Value,
            Upkeep = upkeep ?? 0,
            Residents = (int)(residents ?? 0),
            Jobs = (int)(jobs ?? 0),
            PowerSupply = (int)(supply ?? 0),
            PowerUse = (int)(use ?? 0),
            Fallback = fallback,
        };
        return (type, overrides);
    }

    private static void CheckFullId(JsonFields f, string key, string id)
    {
        int colon = id.IndexOf(':');
        if (colon > 0 && NamePattern().IsMatch(id[..colon]) && NamePattern().IsMatch(id[(colon + 1)..])) return;
        f.Error(key, $"'{id}' must be a full id, pack:name, like base:road");
    }

    private static void CheckFallback(
        BuildingType type, string file,
        Dictionary<string, (BuildingType Type, string File)> types,
        Dictionary<string, PackInfo> packs, List<ContentError> errors)
    {
        if (type.Fallback is not { } id) return;
        void Error(string message) => errors.Add(new ContentError(file, "$.fallback", message));

        if (!types.TryGetValue(id, out var target))
        {
            Error($"'{id}' is not a loaded type{JsonFields.Suggest(id, types.Keys)}");
            return;
        }
        // A fallback stands in when this type's pack is gone, so it must not
        // vanish with it: base, or a pack this one depends on.
        string fallbackPack = id[..id.IndexOf(':')];
        if (fallbackPack == type.Pack || (fallbackPack != BasePack && !packs[type.Pack].DependsOn.Contains(fallbackPack)))
            Error($"'{id}' must come from '{BasePack}' or a pack that '{type.Pack}' depends on");
        else if (target.Type.Role != type.Role)
            Error($"'{id}' is a {target.Type.Role.ToString().ToLowerInvariant()}, not a {type.Role.ToString().ToLowerInvariant()}");
        else if (target.Type.Width > type.Width || target.Type.Height > type.Height)
            Error($"'{id}' is {target.Type.Width}x{target.Type.Height}, larger than this type's {type.Width}x{type.Height}; a fallback may not be larger");
    }
}
