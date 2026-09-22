using System;
using System.Numerics;
using Realm.MapAPI;

namespace Realm.Maps;

public static class Coordinates
{
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
        if (QuadrantCenters.Length > 0)
        {
            return QuadrantCenters[Math.Abs(playerIndex) % QuadrantCenters.Length];
        }
        return Vector3.Zero;
    }

    public static Vector3 GetRandomSpawnPointOnRing(IGameAPI api, Vector3 center, float minRadius = 22.0f, float maxRadius = 27.0f, float spawnHeight = 3f)
    {
        float angle = api.RandomFloat(0f, MathF.Tau);
        float radius = MathF.Min(27.5f, minRadius + api.RandomFloat(0f, maxRadius - minRadius));
        return new Vector3(center.X + (MathF.Cos(angle) * radius), spawnHeight, center.Z + (MathF.Sin(angle) * radius));
    }
}

