using SprawlWerk.Sim.Content;

namespace SprawlWerk.Sim.Tests;

public class ContentLoaderTests
{
    private const string BaseManifest = """
        { "schemaVersion": 1, "id": "base", "version": "1.0.0", "license": "BSD-3-Clause" }
        """;

    private const string Road = """
        { "schemaVersion": 1, "id": "road", "role": "network", "network": "road", "cost": 15 }
        """;

    private const string House = """
        { "schemaVersion": 1, "id": "house", "role": "building", "zone": "residential",
          "density": "low", "size": [1, 1], "cost": 0, "residents": 6, "powerUse": 1 }
        """;

    private static PackSource Pack(string manifest, params (string Name, string Json)[] types) =>
        new(new SourceFile("pack.json", manifest), types.Select(t => new SourceFile($"{t.Name}.sim.json", t.Json)).ToArray());

    private static PackSource BasePack(params (string, string)[] types) => Pack(BaseManifest, types);

    private static PackSource ModPack(string id, string extra = "", params (string, string)[] types) =>
        Pack($$"""{ "schemaVersion": 1, "id": "{{id}}", "version": "1.0.0", "license": "CC0-1.0"{{extra}} }""", types);

    private static ContentSet Loads(params PackSource[] packs)
    {
        var result = ContentLoader.Load(packs);
        Assert.True(result.Succeeded, string.Join("\n", result.Errors));
        return result.Content!;
    }

    private static ContentError Fails(params PackSource[] packs)
    {
        var result = ContentLoader.Load(packs);
        Assert.False(result.Succeeded);
        return Assert.Single(result.Errors);
    }

    [Fact]
    public void A_type_gets_a_namespaced_id_and_its_numbers()
    {
        var content = Loads(BasePack(("house", House)));
        var house = content[content.Find("base:house")];
        Assert.Equal(TypeRole.Building, house.Role);
        Assert.Equal(ZoneKind.Residential, house.Zone);
        Assert.Equal(Density.Low, house.Density);
        Assert.Equal(6, house.Residents);
        Assert.Equal(1, house.PowerUse);
        Assert.Equal("base", house.Pack);
    }

    [Fact]
    public void Handles_follow_id_order_not_the_order_files_arrive_in()
    {
        var a = Loads(BasePack(("road", Road), ("house", House)));
        var b = Loads(BasePack(("house", House), ("road", Road)));
        Assert.Equal(a.Find("base:house"), b.Find("base:house"));
        Assert.Equal(new TypeHandle(1), a.Find("base:house"));
        Assert.Equal(new TypeHandle(2), a.Find("base:road"));
        Assert.True(TypeHandle.None.IsNone);
    }

    [Fact]
    public void An_unknown_key_is_an_error_with_a_suggestion()
    {
        var error = Fails(BasePack(("road", """
            { "schemaVersion": 1, "id": "road", "role": "network", "network": "road", "cost": 15, "upkep": 1 }
            """)));
        Assert.Equal("road.sim.json", error.File);
        Assert.Equal("$.upkep", error.JsonPath);
        Assert.Contains("did you mean 'upkeep'", error.Message);
        Assert.Equal("road.sim.json: $.upkep: is not a known key (did you mean 'upkeep'?)", error.ToString());
    }

    [Fact]
    public void Every_problem_in_a_file_is_reported_at_once()
    {
        var errors = ContentLoader.Load([BasePack(("bad", """
            { "schemaVersion": 1, "id": "Bad Name", "role": "tower", "cost": -5 }
            """))]).Errors;
        Assert.Contains(errors, e => e.JsonPath == "$.id");
        Assert.Contains(errors, e => e.JsonPath == "$.role" && e.Message.Contains("building, network, zone"));
        Assert.Contains(errors, e => e.JsonPath == "$.cost");
    }

    [Theory]
    [InlineData("""{ "schemaVersion": 1, "id": "z", "role": "zone", "cost": 1 }""", "$.zone")]
    [InlineData("""{ "schemaVersion": 1, "id": "z", "role": "zone", "zone": "residential", "size": [2, 2], "cost": 1 }""", "$.size")]
    [InlineData("""{ "schemaVersion": 1, "id": "n", "role": "network", "cost": 1 }""", "$.network")]
    [InlineData("""{ "schemaVersion": 1, "id": "b", "role": "building", "network": "road", "cost": 1 }""", "$.network")]
    [InlineData("""{ "schemaVersion": 1, "id": "b", "role": "building", "size": [9, 1], "cost": 1 }""", "$.size")]
    [InlineData("""{ "schemaVersion": 1, "id": "b", "role": "building", "size": [2.5, 1], "cost": 1 }""", "$.size")]
    [InlineData("""{ "schemaVersion": 1, "id": "b", "role": "building", "cost": 1.5 }""", "$.cost")]
    [InlineData("""{ "schemaVersion": 1, "id": "b", "role": "building", "cost": "1" }""", "$.cost")]
    [InlineData("""{ "schemaVersion": 1, "id": "b", "role": "building", "cost": 1, "cost": 2 }""", "$.cost")]
    [InlineData("""{ "schemaVersion": 2, "id": "b", "role": "building", "cost": 1 }""", "$.schemaVersion")]
    [InlineData("""{ "id": "b", "role": "building", "cost": 1 }""", "$.schemaVersion")]
    public void A_type_that_breaks_a_rule_is_refused_at_the_right_key(string json, string path)
    {
        var error = Fails(BasePack(("t", json)));
        Assert.Equal(path, error.JsonPath);
    }

    [Fact]
    public void A_newer_schema_says_the_pack_needs_a_newer_game()
    {
        var error = Fails(BasePack(("t", """{ "schemaVersion": 2, "id": "b", "role": "building", "cost": 1 }""")));
        Assert.Contains("newer game", error.Message);
    }

    [Fact]
    public void Broken_json_names_the_file_and_line()
    {
        var error = Fails(BasePack(("road", "{\n  \"schemaVersion\": 1,\n  \"id\": \"road\"\n  \"role\": \"network\" }")));
        Assert.Equal("road.sim.json", error.File);
        Assert.Contains("line 4", error.Message);
    }

    [Fact]
    public void Without_the_base_pack_nothing_loads()
    {
        var error = Fails(ModPack("extra"));
        Assert.Contains("'base' pack is missing", error.Message);
    }

    [Theory]
    [InlineData("""{ "schemaVersion": 1, "id": "base", "version": "1.0", "license": "MIT" }""", "$.version")]
    [InlineData("""{ "schemaVersion": 1, "id": "base", "version": "1.0.0" }""", "$.license")]
    [InlineData("""{ "schemaVersion": 1, "id": "Base", "version": "1.0.0", "license": "MIT" }""", "$.id")]
    [InlineData("""{ "schemaVersion": 1, "id": "base", "version": "1.0.0", "license": "MIT", "licence": "MIT" }""", "$.licence")]
    public void A_manifest_that_breaks_a_rule_is_refused(string manifest, string path)
    {
        var error = Fails(Pack(manifest));
        Assert.Equal("pack.json", error.File);
        Assert.Equal(path, error.JsonPath);
    }

    [Fact]
    public void A_duplicate_id_is_an_error_that_points_at_overrides()
    {
        var error = Fails(BasePack(("a", Road), ("b", Road)));
        Assert.Equal("b.sim.json", error.File);
        Assert.Contains("already defined in a.sim.json", error.Message);
        Assert.Contains("overrides", error.Message);
    }

    [Fact]
    public void The_same_name_in_another_pack_is_a_different_type()
    {
        var content = Loads(BasePack(("road", Road)), ModPack("extra", types: ("road", Road)));
        Assert.NotEqual(content.Find("base:road"), content.Find("extra:road"));
    }

    [Fact]
    public void An_override_replaces_the_base_definition_and_is_logged()
    {
        var result = ContentLoader.Load([
            BasePack(("road", Road)),
            ModPack("cheap", types: ("road", """
                { "schemaVersion": 1, "overrides": "base:road", "role": "network", "network": "road", "cost": 5 }
                """)),
        ]);
        Assert.True(result.Succeeded, string.Join("\n", result.Errors));
        var road = result.Content![result.Content.Find("base:road")];
        Assert.Equal(5, road.Cost);
        Assert.Equal("cheap", road.Pack);
        Assert.Contains("cheap overrides base:road (road.sim.json)", result.Log);
    }

    [Fact]
    public void An_override_of_something_not_loaded_is_refused()
    {
        var error = Fails(BasePack(), ModPack("cheap", types: ("road", """
            { "schemaVersion": 1, "overrides": "base:road", "role": "network", "network": "road", "cost": 5 }
            """)));
        Assert.Equal("$.overrides", error.JsonPath);
    }

    [Fact]
    public void An_override_may_not_also_name_an_id()
    {
        var error = Fails(BasePack(("road", Road)), ModPack("cheap", types: ("road", """
            { "schemaVersion": 1, "id": "road", "overrides": "base:road", "role": "network", "network": "road", "cost": 5 }
            """)));
        Assert.Equal("$.id", error.JsonPath);
    }

    [Fact]
    public void Packs_load_base_first_then_dependencies_then_by_id()
    {
        var content = Loads(
            ModPack("zeta"),
            ModPack("alpha", """, "dependsOn": ["zeta"]"""),
            ModPack("beta"),
            BasePack());
        Assert.Equal(["base", "beta", "zeta", "alpha"], content.Packs.Select(p => p.Id));
    }

    [Fact]
    public void A_missing_dependency_is_named()
    {
        var error = Fails(BasePack(), ModPack("alpha", """, "dependsOn": ["trams"]"""));
        Assert.Contains("needs pack 'trams'", error.Message);
    }

    [Fact]
    public void A_dependency_loop_is_refused()
    {
        var error = Fails(BasePack(), ModPack("a", """, "dependsOn": ["b"]"""), ModPack("b", """, "dependsOn": ["a"]"""));
        Assert.Contains("loop: a, b", error.Message);
    }

    [Fact]
    public void A_pack_given_twice_is_refused()
    {
        var error = Fails(BasePack(), ModPack("extra"), ModPack("extra"));
        Assert.Contains("more than once", error.Message);
    }

    private const string BigHouse = """
        { "schemaVersion": 1, "id": "villa", "role": "building", "zone": "residential",
          "size": [2, 2], "cost": 0, "fallback": "base:house" }
        """;

    [Fact]
    public void A_fallback_from_base_that_fits_is_accepted()
    {
        var content = Loads(BasePack(("house", House)), ModPack("villas", types: ("villa", BigHouse)));
        Assert.Equal("base:house", content[content.Find("villas:villa")].Fallback);
    }

    [Fact]
    public void A_fallback_larger_than_the_type_is_refused()
    {
        var error = Fails(
            BasePack(("house", House.Replace("[1, 1]", "[3, 3]"))),
            ModPack("villas", types: ("villa", BigHouse)));
        Assert.Contains("larger", error.Message);
    }

    [Fact]
    public void A_fallback_from_the_same_pack_is_refused_because_it_would_vanish_with_it()
    {
        var error = Fails(BasePack(), ModPack("villas", "", ("house", House), ("villa", BigHouse.Replace("base:house", "villas:house"))));
        Assert.Equal("$.fallback", error.JsonPath);
        Assert.Contains("must come from 'base'", error.Message);
    }

    [Fact]
    public void A_fallback_of_another_role_is_refused()
    {
        var error = Fails(BasePack(("road", Road)), ModPack("villas", types: ("villa", BigHouse.Replace("base:house", "base:road"))));
        Assert.Contains("is a network, not a building", error.Message);
    }

    [Fact]
    public void A_misspelt_fallback_gets_a_suggestion()
    {
        var error = Fails(BasePack(("house", House)), ModPack("villas", types: ("villa", BigHouse.Replace("base:house", "base:hose"))));
        Assert.Contains("did you mean 'base:house'", error.Message);
    }
}
