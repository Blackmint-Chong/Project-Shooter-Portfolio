using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "BattleMapCatalog",
    menuName = "Project Shooter/Battle/Map Catalog")]
public sealed class BattleMapCatalog : ScriptableObject
{
    [SerializeField] private BattleMapDefinition defaultMap;
    [SerializeField] private List<BattleMapDefinition> maps = new();

    public BattleMapDefinition DefaultMap => defaultMap;
    public IReadOnlyList<BattleMapDefinition> Maps => maps;

    public bool Contains(BattleMapDefinition map)
    {
        return map != null && maps.Contains(map);
    }

    public bool TryGetMap(
        string mapId,
        out BattleMapDefinition map)
    {
        string normalizedMapId = mapId?.Trim();
        if (string.IsNullOrEmpty(normalizedMapId))
        {
            map = null;
            return false;
        }

        foreach (BattleMapDefinition candidate in maps)
        {
            if (candidate != null
                && string.Equals(
                    candidate.MapId,
                    normalizedMapId,
                    StringComparison.OrdinalIgnoreCase))
            {
                map = candidate;
                return true;
            }
        }

        map = null;
        return false;
    }

    public bool TryGetMapByScenePath(
        string scenePath,
        out BattleMapDefinition map)
    {
        string normalizedScenePath = NormalizeScenePath(scenePath);
        if (string.IsNullOrEmpty(normalizedScenePath))
        {
            map = null;
            return false;
        }

        foreach (BattleMapDefinition candidate in maps)
        {
            if (candidate != null
                && string.Equals(
                    candidate.ScenePath,
                    normalizedScenePath,
                    StringComparison.OrdinalIgnoreCase))
            {
                map = candidate;
                return true;
            }
        }

        map = null;
        return false;
    }

    public bool TryValidate(out string error)
    {
        if (maps.Count == 0)
        {
            error = "A battle map catalog requires at least one map.";
            return false;
        }

        HashSet<BattleMapDefinition> uniqueMaps = new();
        HashSet<string> uniqueMapIds = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> uniqueScenePaths = new(StringComparer.OrdinalIgnoreCase);

        foreach (BattleMapDefinition map in maps)
        {
            if (map == null)
            {
                error = "The battle map catalog contains a missing map.";
                return false;
            }

            if (!uniqueMaps.Add(map))
            {
                error = $"Battle map '{map.name}' is registered more than once.";
                return false;
            }

            if (!map.TryValidate(out string mapError))
            {
                error = $"Battle map '{map.name}' is invalid: {mapError}";
                return false;
            }

            if (!uniqueMapIds.Add(map.MapId))
            {
                error = $"Battle map ID '{map.MapId}' is registered more than once.";
                return false;
            }

            if (!uniqueScenePaths.Add(map.ScenePath))
            {
                error =
                    $"Battle scene '{map.ScenePath}' is registered more than once.";
                return false;
            }
        }

        if (defaultMap == null)
        {
            error = "The battle map catalog requires a default map.";
            return false;
        }

        if (!uniqueMaps.Contains(defaultMap))
        {
            error = "The default battle map must be included in the catalog.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    internal void Configure(
        BattleMapDefinition newDefaultMap,
        IEnumerable<BattleMapDefinition> newMaps)
    {
        defaultMap = newDefaultMap;
        maps = newMaps == null
            ? new List<BattleMapDefinition>()
            : new List<BattleMapDefinition>(newMaps);
    }

    private void OnValidate()
    {
        maps ??= new List<BattleMapDefinition>();
    }

    private static string NormalizeScenePath(string value)
    {
        return (value?.Trim() ?? string.Empty).Replace('\\', '/');
    }
}
