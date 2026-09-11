using System;
using System.Numerics;

namespace Realm.Maps;

public static class Coordinates
{
    public static readonly Vector3 Center = new Vector3(-150f, 3f, -150f);
    public static readonly Vector3 HeroCenter = new Vector3(-150f, 3f, -150f);

    public static readonly Vector3[] QuadrantCenters = new Vector3[]
    {
        new Vector3(-150f, 3f, -150f), // P1: Northwest (Solo)
        new Vector3(   0f, 3f, -150f), // P2: North
        new Vector3( 150f, 3f, -150f), // P3: Northeast
        new Vector3( 150f, 3f,    0f), // P4: East
        new Vector3( 150f, 3f,  150f), // P5: Southeast
        new Vector3(   0f, 3f,  150f), // P6: South
        new Vector3(-150f, 3f,  150f), // P7: Southwest
        new Vector3(-150f, 3f,    0f)  // P8: West
    };

    public static Vector3 GetQuadrantCenter(int playerIndex)
    {
        if (playerIndex >= 0 && playerIndex < QuadrantCenters.Length)
        {
            return QuadrantCenters[playerIndex];
        }
        if (playerIndex >= 0 && QuadrantCenters.Length > 0)
        {
            return QuadrantCenters[playerIndex % QuadrantCenters.Length];
        }
        return QuadrantCenters[0];
    }

    public static Vector3 GetRandomSpawnPointOnRing(Vector3 center, float minRadius = 22.0f, float maxRadius = 27.0f, float spawnHeight = 3f)
    {
        float angle = Random.Shared.NextSingle() * MathF.Tau;
        float radius = MathF.Min(27.5f, minRadius + (Random.Shared.NextSingle() * (maxRadius - minRadius)));
        return new Vector3(center.X + (MathF.Cos(angle) * radius), spawnHeight, center.Z + (MathF.Sin(angle) * radius));
    }

    public static Vector3 GetRandomSpawnPointOnRing(int playerIndex, float minRadius = 22.0f, float maxRadius = 27.0f, float spawnHeight = 3f)
    {
        return GetRandomSpawnPointOnRing(GetQuadrantCenter(playerIndex), minRadius, maxRadius, spawnHeight);
    }
}

