using System;
using System.IO;
using UnityEngine;

[CreateAssetMenu(
    fileName = "BattleMapDefinition",
    menuName = "Project Shooter/Battle/Map Definition")]
public sealed class BattleMapDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string mapId = "map";
    [SerializeField] private string displayName = "Map";
    [SerializeField, TextArea(2, 4)] private string description;

    [Header("Scene")]
    [SerializeField] private string scenePath;

    [Header("Presentation")]
    [SerializeField] private Sprite thumbnail;

    public string MapId => mapId;
    public string DisplayName => displayName;
    public string Description => description;
    public string ScenePath => scenePath;
    public string SceneName => string.IsNullOrEmpty(scenePath)
        ? string.Empty
        : Path.GetFileNameWithoutExtension(scenePath);
    public Sprite Thumbnail => thumbnail;

    public bool TryValidate(out string error)
    {
        if (string.IsNullOrWhiteSpace(mapId))
        {
            error = "A battle map requires a stable map ID.";
            return false;
        }

        if (!IsValidMapId(mapId))
        {
            error =
                $"Battle map ID '{mapId}' must be a lowercase slug using letters, numbers, '-' or '_'.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            error = $"Battle map '{mapId}' requires a display name.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(scenePath))
        {
            error = $"Battle map '{mapId}' requires a scene path.";
            return false;
        }

        if (!scenePath.StartsWith("Assets/", StringComparison.Ordinal)
            || !scenePath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
        {
            error =
                $"Battle map '{mapId}' scene path must point to a .unity scene below Assets/.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    internal void Configure(
        string newMapId,
        string newDisplayName,
        string newDescription,
        string newScenePath,
        Sprite newThumbnail)
    {
        mapId = newMapId;
        displayName = newDisplayName;
        description = newDescription;
        scenePath = newScenePath;
        thumbnail = newThumbnail;
        NormalizeSerializedValues();
    }

    private void OnValidate()
    {
        NormalizeSerializedValues();
    }

    private void NormalizeSerializedValues()
    {
        mapId = mapId?.Trim() ?? string.Empty;
        displayName = displayName?.Trim() ?? string.Empty;
        description = description?.Trim() ?? string.Empty;
        scenePath = (scenePath?.Trim() ?? string.Empty).Replace('\\', '/');
    }

    private static bool IsValidMapId(string value)
    {
        if (value.Length == 0 || !IsLowercaseLetterOrDigit(value[0]))
        {
            return false;
        }

        for (int index = 1; index < value.Length; index++)
        {
            char character = value[index];
            if (!IsLowercaseLetterOrDigit(character)
                && character != '-'
                && character != '_')
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsLowercaseLetterOrDigit(char character)
    {
        return character >= 'a' && character <= 'z'
            || character >= '0' && character <= '9';
    }
}
