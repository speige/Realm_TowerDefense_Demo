using System;
using System.Numerics;

namespace Realm.Maps;

public static class Coordinates
{
    public static readonly Vector3 Center = new Vector3(0f, 3f, 0f);
    public static readonly Vector3 HeroCenter = new Vector3(0f, 3f, 0f);

    public static readonly Vector3[] QuadrantCenters = new Vector3[]
    {
        new Vector3(-45f, 3f, -45f),
        new Vector3(45f, 3f, -45f),
        new Vector3(-45f, 3f, 45f),
        new Vector3(45f, 3f, 45f)
    };

    public static readonly string[] QuadrantHeroWeapons = new string[]
    {
        "green_magic_sword",
        "ice_crystal_spear",
        "flame_spear",
        "crystal_enchanted_sword"
    };

    public static Vector3 GetQuadrantCenter(int playerIndex)
    {
        if (playerIndex >= 0 && playerIndex < QuadrantCenters.Length)
        {
            return QuadrantCenters[playerIndex];
        }
        return Center;
    }

    public static string GetHeroWeapon(int playerIndex)
    {
        if (playerIndex >= 0 && playerIndex < QuadrantHeroWeapons.Length)
        {
            return QuadrantHeroWeapons[playerIndex];
        }
        return "green_magic_sword";
    }

    public static Vector3 GetRandomSpawnPointOnRing(Vector3 center, float minRadius = 20f, float maxRadius = 26f, float spawnHeight = 3f)
    {
        float angle = Random.Shared.NextSingle() * MathF.Tau;
        float radius = minRadius + (Random.Shared.NextSingle() * (maxRadius - minRadius));
        return new Vector3(center.X + (MathF.Cos(angle) * radius), spawnHeight, center.Z + (MathF.Sin(angle) * radius));
    }

    public static Vector3 GetRandomSpawnPointOnRing(int playerIndex, float minRadius = 20f, float maxRadius = 26f, float spawnHeight = 3f)
    {
        return GetRandomSpawnPointOnRing(GetQuadrantCenter(playerIndex), minRadius, maxRadius, spawnHeight);
    }
}
