using System;
using System.Numerics;

namespace Realm.Maps;

public static class Coordinates
{
    public static readonly Vector3 Center = new Vector3(-40f, 3f, -40f);
    public static readonly Vector3 HeroCenter = new Vector3(-40f, 3f, -40f);

    public static readonly Vector3[] QuadrantCenters = new Vector3[]
    {
        new Vector3(-40f, 3f, -40f),
        new Vector3(40f, 3f, -40f),
        new Vector3(-40f, 3f, 40f),
        new Vector3(40f, 3f, 40f)
    };

    public static Vector3 GetQuadrantCenter(int playerIndex)
    {
        if (playerIndex >= 0 && playerIndex < QuadrantCenters.Length)
        {
            return QuadrantCenters[playerIndex];
        }
        return QuadrantCenters[0];
    }

    public static Vector3 GetRandomSpawnPointOnRing(Vector3 center, float minRadius = 11f, float maxRadius = 15f, float spawnHeight = 3f)
    {
        float angle = Random.Shared.NextSingle() * MathF.Tau;
        float radius = MathF.Min(15.5f, minRadius + (Random.Shared.NextSingle() * (maxRadius - minRadius)));
        return new Vector3(center.X + (MathF.Cos(angle) * radius), spawnHeight, center.Z + (MathF.Sin(angle) * radius));
    }

    public static Vector3 GetRandomSpawnPointOnRing(int playerIndex, float minRadius = 11f, float maxRadius = 15f, float spawnHeight = 3f)
    {
        return GetRandomSpawnPointOnRing(GetQuadrantCenter(playerIndex), minRadius, maxRadius, spawnHeight);
    }
}

