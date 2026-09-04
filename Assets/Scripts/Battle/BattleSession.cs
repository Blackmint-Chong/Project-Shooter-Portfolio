using System;
using UnityEngine;

public enum BattleTeamSide
{
    Left = 0,
    Right = 1
}

public readonly struct BattleLaunchConfiguration
{
    public BattleLaunchConfiguration(
        string mapId,
        string scenePath,
        AIDifficultyDefinition aiDifficultyProfile)
    {
        MapId = mapId;
        ScenePath = scenePath;
        AIDifficultyProfile = aiDifficultyProfile;
    }

    public string MapId { get; }
    public string ScenePath { get; }
    public AIDifficultyDefinition AIDifficultyProfile { get; }
}

/// <summary>
/// Holds the transient choices made in the game setup scene.
/// The values intentionally live only for the current application session.
/// </summary>
public static class BattleSession
{
    private static BattleLaunchConfiguration configuration;

    public static bool HasConfiguration { get; private set; }
    public static BattleLaunchConfiguration Configuration => configuration;

    public static void Configure(
        BattleMapDefinition map,
        AIDifficultyDefinition aiDifficultyProfile)
    {
        if (map == null)
        {
            throw new ArgumentNullException(nameof(map));
        }

        if (!map.TryValidate(out string mapError))
        {
            throw new ArgumentException(
                $"The selected battle map is invalid: {mapError}",
                nameof(map));
        }

        if (aiDifficultyProfile == null)
        {
            throw new ArgumentNullException(nameof(aiDifficultyProfile));
        }

        if (!aiDifficultyProfile.TryValidate(out string difficultyError))
        {
            throw new ArgumentException(
                $"The AI difficulty profile is invalid: {difficultyError}",
                nameof(aiDifficultyProfile));
        }

        configuration = new BattleLaunchConfiguration(
            map.MapId,
            map.ScenePath,
            aiDifficultyProfile);
        HasConfiguration = true;
    }

    public static bool TryResolveSelectedMap(
        BattleMapCatalog catalog,
        out BattleMapDefinition map)
    {
        if (!HasConfiguration || catalog == null)
        {
            map = null;
            return false;
        }

        return catalog.TryGetMap(configuration.MapId, out map);
    }

    public static bool TryGetForScene(
        string scenePath,
        out BattleLaunchConfiguration launchConfiguration)
    {
        string normalizedScenePath = NormalizeScenePath(scenePath);
        if (HasConfiguration
            && string.Equals(
                configuration.ScenePath,
                normalizedScenePath,
                StringComparison.OrdinalIgnoreCase))
        {
            launchConfiguration = configuration;
            return true;
        }

        launchConfiguration = default;
        return false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Reset()
    {
        configuration = default;
        HasConfiguration = false;
    }

    private static string NormalizeScenePath(string value)
    {
        return (value?.Trim() ?? string.Empty).Replace('\\', '/');
    }
}
