using System.Collections.Generic;
using Godot;
using SprawlWerk.Sim;

namespace SprawlWerk;

/// <summary>
/// One model to draw on a tile: which file, turned how far about the vertical
/// axis (degrees, counter-clockwise seen from above), moved how far from the
/// tile's centre, and either scaled by <see cref="Scale"/> or, when
/// <see cref="Fit"/> is set, centred and shrunk so its footprint spans that
/// fraction of a tile. <see cref="Stretch"/> lengthens the model along its own
/// X axis only (before it is turned).
/// </summary>
public readonly record struct ModelPlacement(string Model, float Rotation = 0f, Vector3 Offset = default, float Scale = 1f, float Fit = 0f, float Stretch = 1f);

/// <summary>
/// The one place that decides what a tile looks like in the 3D view. Today it
/// reads <see cref="TileKind"/>; when tiles carry content type ids instead, only
/// this class changes. Zones are not here: they are flat lots tinted from
/// <see cref="Palette"/> until buildings grow on them.
///
/// Roads and power lines look at their four neighbours: a road picks a
/// straight, bend, T, crossroad or dead-end piece; a pole turns to run along
/// its line.
/// </summary>
public static class TileModels
{
    private const string Roads = "res://assets/models/kenney/city-kit-roads/";
    private const string Industrial = "res://assets/models/kenney/city-kit-industrial/";

    public const string RoadStraight = Roads + "road-straight.glb";
    public const string RoadBend = Roads + "road-bend.glb";
    public const string RoadT = Roads + "road-intersection.glb";
    public const string RoadCross = Roads + "road-crossroad.glb";
    public const string RoadEnd = Roads + "road-end.glb";
    public const string Pole = Roads + "electricity-pole.glb";
    public const string PlantHall = Industrial + "building-c.glb";
    public const string PlantChimney = Industrial + "chimney-large.glb";

    /// <summary>Every model this class may ask for, so the view can load them up front.</summary>
    public static readonly string[] All = [RoadStraight, RoadBend, RoadT, RoadCross, RoadEnd, Pole, PlantHall, PlantChimney];

    /// <summary>True for tiles drawn as a flat tinted lot rather than models.</summary>
    public static bool IsLot(TileKind kind) => kind is TileKind.Residential or TileKind.Commercial or TileKind.Industrial;

    /// <summary>Add the models for tile (x, y) to <paramref name="into"/>.</summary>
    public static void For(CityMap map, int x, int y, List<ModelPlacement> into)
    {
        switch (map[x, y].Kind)
        {
            case TileKind.Road:
                into.Add(RoadPiece(map, x, y));
                break;
            case TileKind.RoadWithLine:
            {
                into.Add(RoadPiece(map, x, y));
                bool lineAlongX = LineRunsAlongX(map, x, y);
                bool roadAlongX = IsRoad(map, x - 1, y) || IsRoad(map, x + 1, y);
                if (lineAlongX != roadAlongX)
                    // Crossing the road: one span over the carriageway, posts on the kerbs.
                    into.Add(new ModelPlacement(Pole, PoleTurn(lineAlongX), Stretch: 1.45f));
                else
                    // Running along the road: on the verge, not in the middle of the carriageway.
                    into.Add(new ModelPlacement(Pole, PoleTurn(lineAlongX),
                        lineAlongX ? new Vector3(0, 0, -0.44f) : new Vector3(-0.44f, 0, 0)));
                break;
            }
            case TileKind.PowerLine:
                into.Add(new ModelPlacement(Pole, PoleTurn(LineRunsAlongX(map, x, y))));
                break;
            case TileKind.PowerPlant:
                into.Add(new ModelPlacement(PlantHall, Fit: 0.95f));
                into.Add(new ModelPlacement(PlantChimney, Offset: new Vector3(0.22f, 0, 0.22f), Scale: 0.45f));
                break;
        }
    }

    private static bool IsRoad(CityMap map, int x, int y) =>
        map.InBounds(x, y) && map[x, y].Kind is TileKind.Road or TileKind.RoadWithLine;

    private static bool CarriesLine(CityMap map, int x, int y) =>
        map.InBounds(x, y) && map[x, y].Kind is TileKind.PowerLine or TileKind.RoadWithLine or TileKind.PowerPlant;

    // The pole model is one span of line: two posts with the wires between them along its own X axis.
    private static float PoleTurn(bool lineAlongX) => lineAlongX ? 0f : 90f;

    private static bool LineRunsAlongX(CityMap map, int x, int y)
    {
        bool alongX = CarriesLine(map, x - 1, y) || CarriesLine(map, x + 1, y);
        bool alongZ = CarriesLine(map, x, y - 1) || CarriesLine(map, x, y + 1);
        if (alongX != alongZ) return alongX;
        // A lone pole, or a corner: across the road if on one, else along X.
        if (map[x, y].Kind == TileKind.RoadWithLine) return !(IsRoad(map, x - 1, y) || IsRoad(map, x + 1, y));
        return true;
    }

    // Tile y grows along world +Z, so north (y - 1) is -Z and east (x + 1) is +X.
    // Each piece's own orientation: straight runs along X; bend joins +X and +Z;
    // T is open on every side but -Z; a dead end opens to +X.
    private static ModelPlacement RoadPiece(CityMap map, int x, int y)
    {
        bool e = IsRoad(map, x + 1, y), w = IsRoad(map, x - 1, y);
        bool s = IsRoad(map, x, y + 1), n = IsRoad(map, x, y - 1);
        int count = (e ? 1 : 0) + (w ? 1 : 0) + (s ? 1 : 0) + (n ? 1 : 0);

        switch (count)
        {
            case 4:
                return new ModelPlacement(RoadCross);
            case 3:
                return new ModelPlacement(RoadT, !n ? 0f : !e ? -90f : !s ? 180f : 90f);
            case 2 when (e && w) || (n && s):
                return new ModelPlacement(RoadStraight, e ? 0f : 90f);
            case 2:
                return new ModelPlacement(RoadBend, e && s ? 0f : s && w ? -90f : w && n ? 180f : 90f);
            case 1:
                return new ModelPlacement(RoadEnd, e ? 0f : s ? -90f : w ? 180f : 90f);
            default:
                // A lone road tile: a straight piece along X.
                return new ModelPlacement(RoadStraight);
        }
    }
}
