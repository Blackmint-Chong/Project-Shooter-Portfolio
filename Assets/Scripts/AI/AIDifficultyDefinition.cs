using System;
using UnityEngine;

[CreateAssetMenu(
    fileName = "AIDifficultyDefinition",
    menuName = "Project Shooter/AI/Difficulty Definition")]
public sealed class AIDifficultyDefinition : ScriptableObject
{
    public const float MinimumDecisionInterval = 0.02f;
    private const float MinimumScanDistance = 0.01f;

    [Header("Identity")]
    [SerializeField] private string difficultyId = "basic";
    [SerializeField] private string displayName = "Basic";

    [Header("Decision")]
    [SerializeField, Min(MinimumDecisionInterval)]
    private float decisionInterval = 0.1f;

    [Header("Roaming")]
    [SerializeField] private Vector2 roamDurationRange = new(0.6f, 2f);

    [Header("Vertical Exploration")]
    [SerializeField] private Vector2 explorationIntervalRange = new(2.5f, 5f);
    [SerializeField, Range(0f, 1f)] private float explorationDropChance = 0.4f;

    [Header("Height Matching")]
    [SerializeField, Min(0f)] private float heightActionThreshold = 0.5f;
    [SerializeField, Min(0f)] private float shootHeightTolerance = 0.5f;
    [SerializeField, Min(MinimumScanDistance)]
    private float upperPlatformScanDistance = 2f;
    [SerializeField, Min(MinimumScanDistance)]
    private float lowerPlatformScanDistance = 2.25f;
    [SerializeField, Min(0f)] private float verticalActionCooldown = 0.15f;

    public string DifficultyId => difficultyId;
    public string DisplayName => displayName;
    public float DecisionInterval => decisionInterval;
    public Vector2 RoamDurationRange => roamDurationRange;
    public Vector2 ExplorationIntervalRange => explorationIntervalRange;
    public float ExplorationDropChance => explorationDropChance;
    public float HeightActionThreshold => heightActionThreshold;
    public float ShootHeightTolerance => shootHeightTolerance;
    public float UpperPlatformScanDistance => upperPlatformScanDistance;
    public float LowerPlatformScanDistance => lowerPlatformScanDistance;
    public float VerticalActionCooldown => verticalActionCooldown;

    public bool TryValidate(out string error)
    {
        if (string.IsNullOrWhiteSpace(difficultyId))
        {
            error = "An AI difficulty requires a stable difficulty ID.";
            return false;
        }

        if (!IsValidDifficultyId(difficultyId))
        {
            error =
                $"AI difficulty ID '{difficultyId}' must be a lowercase slug using letters, numbers, '-' or '_'.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            error = $"AI difficulty '{difficultyId}' requires a display name.";
            return false;
        }

        if (!IsFinite(decisionInterval)
            || decisionInterval < MinimumDecisionInterval)
        {
            error =
                $"AI difficulty '{difficultyId}' requires a decision interval of at least {MinimumDecisionInterval}.";
            return false;
        }

        if (!TryValidateRange(roamDurationRange, 0f))
        {
            error =
                $"AI difficulty '{difficultyId}' requires a valid roaming duration range.";
            return false;
        }

        if (!TryValidateRange(
                explorationIntervalRange,
                MinimumDecisionInterval))
        {
            error =
                $"AI difficulty '{difficultyId}' requires a valid exploration interval range.";
            return false;
        }

        if (!IsFinite(explorationDropChance)
            || explorationDropChance < 0f
            || explorationDropChance > 1f)
        {
            error =
                $"AI difficulty '{difficultyId}' requires an exploration drop chance between 0 and 1.";
            return false;
        }

        if (!IsNonNegativeFinite(heightActionThreshold)
            || !IsNonNegativeFinite(shootHeightTolerance)
            || !IsNonNegativeFinite(verticalActionCooldown))
        {
            error =
                $"AI difficulty '{difficultyId}' contains a negative or non-finite action value.";
            return false;
        }

        if (!IsFinite(upperPlatformScanDistance)
            || upperPlatformScanDistance < MinimumScanDistance
            || !IsFinite(lowerPlatformScanDistance)
            || lowerPlatformScanDistance < MinimumScanDistance)
        {
            error =
                $"AI difficulty '{difficultyId}' requires positive platform scan distances.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    internal void Configure(
        string newDifficultyId,
        string newDisplayName)
    {
        difficultyId = newDifficultyId;
        displayName = newDisplayName;
        NormalizeSerializedValues();
    }

    internal void Configure(
        string newDifficultyId,
        string newDisplayName,
        float newDecisionInterval,
        Vector2 newRoamDurationRange,
        Vector2 newExplorationIntervalRange,
        float newExplorationDropChance,
        float newHeightActionThreshold,
        float newShootHeightTolerance,
        float newUpperPlatformScanDistance,
        float newLowerPlatformScanDistance,
        float newVerticalActionCooldown)
    {
        difficultyId = newDifficultyId;
        displayName = newDisplayName;
        decisionInterval = newDecisionInterval;
        roamDurationRange = newRoamDurationRange;
        explorationIntervalRange = newExplorationIntervalRange;
        explorationDropChance = newExplorationDropChance;
        heightActionThreshold = newHeightActionThreshold;
        shootHeightTolerance = newShootHeightTolerance;
        upperPlatformScanDistance = newUpperPlatformScanDistance;
        lowerPlatformScanDistance = newLowerPlatformScanDistance;
        verticalActionCooldown = newVerticalActionCooldown;
        NormalizeSerializedValues();
    }

    private void OnValidate()
    {
        NormalizeSerializedValues();
    }

    private void NormalizeSerializedValues()
    {
        difficultyId = difficultyId?.Trim() ?? string.Empty;
        displayName = displayName?.Trim() ?? string.Empty;
        decisionInterval = Mathf.Max(
            MinimumDecisionInterval,
            SanitizeFinite(decisionInterval, MinimumDecisionInterval));
        roamDurationRange = NormalizeRange(roamDurationRange, 0f);
        explorationIntervalRange = NormalizeRange(
            explorationIntervalRange,
            MinimumDecisionInterval);
        explorationDropChance = Mathf.Clamp01(
            SanitizeFinite(explorationDropChance, 0f));
        heightActionThreshold = Mathf.Max(
            0f,
            SanitizeFinite(heightActionThreshold, 0f));
        shootHeightTolerance = Mathf.Max(
            0f,
            SanitizeFinite(shootHeightTolerance, 0f));
        upperPlatformScanDistance = Mathf.Max(
            MinimumScanDistance,
            SanitizeFinite(upperPlatformScanDistance, MinimumScanDistance));
        lowerPlatformScanDistance = Mathf.Max(
            MinimumScanDistance,
            SanitizeFinite(lowerPlatformScanDistance, MinimumScanDistance));
        verticalActionCooldown = Mathf.Max(
            0f,
            SanitizeFinite(verticalActionCooldown, 0f));
    }

    private static Vector2 NormalizeRange(Vector2 value, float minimum)
    {
        float lower = Mathf.Max(minimum, SanitizeFinite(value.x, minimum));
        float upper = Mathf.Max(lower, SanitizeFinite(value.y, lower));
        return new Vector2(lower, upper);
    }

    private static bool TryValidateRange(Vector2 value, float minimum)
    {
        return IsFinite(value.x)
            && IsFinite(value.y)
            && value.x >= minimum
            && value.y >= value.x;
    }

    private static float SanitizeFinite(float value, float fallback)
    {
        return IsFinite(value) ? value : fallback;
    }

    private static bool IsNonNegativeFinite(float value)
    {
        return IsFinite(value) && value >= 0f;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static bool IsValidDifficultyId(string value)
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
