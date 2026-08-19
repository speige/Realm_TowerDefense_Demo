using Realm.MapAPI;
using System.Numerics;

namespace Realm.Maps;

public static class Coordinates
{
    public static readonly Vector3 HeroCenter = new Vector3(0f, 3f, 0f);

    public static readonly Coordinate Base = new Coordinate(
        new Vector3(-20.00f, 0f, -20.00f),
        new Vector3(20.00f, 0f, 20.00f)
    );

    public static readonly Coordinate SpawnNorth = new Coordinate(
        new Vector3(-15.00f, 0f, -66.00f),
        new Vector3(15.00f, 0f, -60.00f)
    );

    public static readonly Coordinate SpawnSouth = new Coordinate(
        new Vector3(-15.00f, 0f, 60.00f),
        new Vector3(15.00f, 0f, 66.00f)
    );

    public static readonly Coordinate SpawnEast = new Coordinate(
        new Vector3(60.00f, 0f, -15.00f),
        new Vector3(66.00f, 0f, 15.00f)
    );

    public static readonly Coordinate SpawnWest = new Coordinate(
        new Vector3(-66.00f, 0f, -15.00f),
        new Vector3(-60.00f, 0f, 15.00f)
    );
}