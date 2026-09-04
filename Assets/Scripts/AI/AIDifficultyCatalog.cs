using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "AIDifficultyCatalog",
    menuName = "Project Shooter/AI/Difficulty Catalog")]
public sealed class AIDifficultyCatalog : ScriptableObject
{
    [SerializeField] private AIDifficultyDefinition defaultProfile;
    [SerializeField] private List<AIDifficultyDefinition> profiles = new();

    public AIDifficultyDefinition DefaultProfile => defaultProfile;
    public IReadOnlyList<AIDifficultyDefinition> Profiles => profiles;

    public bool Contains(AIDifficultyDefinition profile)
    {
        return profile != null && profiles.Contains(profile);
    }

    public bool TryGetProfile(
        string difficultyId,
        out AIDifficultyDefinition profile)
    {
        string normalizedDifficultyId = difficultyId?.Trim();
        if (string.IsNullOrEmpty(normalizedDifficultyId))
        {
            profile = null;
            return false;
        }

        foreach (AIDifficultyDefinition candidate in profiles)
        {
            if (candidate != null
                && string.Equals(
                    candidate.DifficultyId,
                    normalizedDifficultyId,
                    StringComparison.OrdinalIgnoreCase))
            {
                profile = candidate;
                return true;
            }
        }

        profile = null;
        return false;
    }

    public bool TryValidate(out string error)
    {
        if (profiles.Count == 0)
        {
            error = "An AI difficulty catalog requires at least one profile.";
            return false;
        }

        HashSet<AIDifficultyDefinition> uniqueProfiles = new();
        HashSet<string> uniqueDifficultyIds = new(
            StringComparer.OrdinalIgnoreCase);

        foreach (AIDifficultyDefinition profile in profiles)
        {
            if (profile == null)
            {
                error = "The AI difficulty catalog contains a missing profile.";
                return false;
            }

            if (!uniqueProfiles.Add(profile))
            {
                error =
                    $"AI difficulty '{profile.name}' is registered more than once.";
                return false;
            }

            if (!profile.TryValidate(out string profileError))
            {
                error =
                    $"AI difficulty '{profile.name}' is invalid: {profileError}";
                return false;
            }

            if (!uniqueDifficultyIds.Add(profile.DifficultyId))
            {
                error =
                    $"AI difficulty ID '{profile.DifficultyId}' is registered more than once.";
                return false;
            }
        }

        if (defaultProfile == null)
        {
            error = "The AI difficulty catalog requires a default profile.";
            return false;
        }

        if (!uniqueProfiles.Contains(defaultProfile))
        {
            error =
                "The default AI difficulty must be included in the catalog.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    internal void Configure(
        AIDifficultyDefinition newDefaultProfile,
        IEnumerable<AIDifficultyDefinition> newProfiles)
    {
        defaultProfile = newDefaultProfile;
        profiles = newProfiles == null
            ? new List<AIDifficultyDefinition>()
            : new List<AIDifficultyDefinition>(newProfiles);
    }

    private void OnValidate()
    {
        profiles ??= new List<AIDifficultyDefinition>();
    }
}
