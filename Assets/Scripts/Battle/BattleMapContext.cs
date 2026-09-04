using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BattleMapContext : MonoBehaviour
{
    [SerializeField] private List<BattleSpawnSlot> spawnSlots = new();
    [SerializeField] private List<PlayerFallDeathZone> fallDeathZones = new();

    public IReadOnlyList<BattleSpawnSlot> SpawnSlots => spawnSlots;
    public IReadOnlyList<PlayerFallDeathZone> FallDeathZones => fallDeathZones;

    public bool TryGetSpawnSlot(
        BattlePlayerSlot playerSlot,
        out BattleSpawnSlot spawnSlot)
    {
        foreach (BattleSpawnSlot candidate in spawnSlots)
        {
            if (candidate != null && candidate.Slot == playerSlot)
            {
                spawnSlot = candidate;
                return true;
            }
        }

        spawnSlot = null;
        return false;
    }

    public bool TryGetSpawnSlot(
        PlayerLife participant,
        out BattleSpawnSlot spawnSlot)
    {
        if (participant != null
            && participant.TryGetComponent(
                out BattleParticipantSlot participantSlot))
        {
            return TryGetSpawnSlot(participantSlot.Slot, out spawnSlot);
        }

        spawnSlot = null;
        return false;
    }

    public bool TryGetRespawnArea(
        BattlePlayerSlot playerSlot,
        out BattleSpawnArea respawnArea)
    {
        if (TryGetSpawnSlot(playerSlot, out BattleSpawnSlot spawnSlot))
        {
            respawnArea = spawnSlot.RespawnArea;
            return respawnArea != null;
        }

        respawnArea = null;
        return false;
    }

    public bool TryGetRespawnArea(
        PlayerLife participant,
        out BattleSpawnArea respawnArea)
    {
        if (TryGetSpawnSlot(participant, out BattleSpawnSlot spawnSlot))
        {
            respawnArea = spawnSlot.RespawnArea;
            return respawnArea != null;
        }

        respawnArea = null;
        return false;
    }

    public bool TryGetSideSpawnSlot(
        BattleTeamSide side,
        out BattleSpawnSlot spawnSlot)
    {
        if (!TryGetSideSpawnSlots(
                out BattleSpawnSlot leftSpawnSlot,
                out BattleSpawnSlot rightSpawnSlot))
        {
            spawnSlot = null;
            return false;
        }

        spawnSlot = side switch
        {
            BattleTeamSide.Left => leftSpawnSlot,
            BattleTeamSide.Right => rightSpawnSlot,
            _ => null
        };
        return spawnSlot != null;
    }

    public bool TryGetSideSpawnSlots(
        out BattleSpawnSlot leftSpawnSlot,
        out BattleSpawnSlot rightSpawnSlot)
    {
        leftSpawnSlot = null;
        rightSpawnSlot = null;

        foreach (BattleSpawnSlot spawnSlot in spawnSlots)
        {
            if (spawnSlot == null)
            {
                continue;
            }

            if (leftSpawnSlot == null
                || spawnSlot.InitialPosition.x
                < leftSpawnSlot.InitialPosition.x)
            {
                leftSpawnSlot = spawnSlot;
            }

            if (rightSpawnSlot == null
                || spawnSlot.InitialPosition.x
                > rightSpawnSlot.InitialPosition.x)
            {
                rightSpawnSlot = spawnSlot;
            }
        }

        return leftSpawnSlot != null
            && rightSpawnSlot != null
            && leftSpawnSlot != rightSpawnSlot
            && !Mathf.Approximately(
                leftSpawnSlot.InitialPosition.x,
                rightSpawnSlot.InitialPosition.x);
    }

    public bool BindFallDeathZones(MatchController matchController)
    {
        if (matchController == null)
        {
            return false;
        }

        foreach (PlayerFallDeathZone fallDeathZone in fallDeathZones)
        {
            if (fallDeathZone == null)
            {
                return false;
            }

            fallDeathZone.Bind(matchController);
        }

        return true;
    }

    public bool TryValidate(out string error)
    {
        int expectedSlotCount = Enum.GetValues(typeof(BattlePlayerSlot)).Length;
        if (spawnSlots.Count != expectedSlotCount)
        {
            error =
                $"A battle map requires exactly {expectedSlotCount} player spawn slots.";
            return false;
        }

        HashSet<BattlePlayerSlot> uniqueSlots = new();
        foreach (BattleSpawnSlot spawnSlot in spawnSlots)
        {
            if (spawnSlot == null)
            {
                error = "The battle map contains a missing player spawn slot.";
                return false;
            }

            BattlePlayerSlot playerSlot = spawnSlot.Slot;
            if (!Enum.IsDefined(typeof(BattlePlayerSlot), playerSlot))
            {
                error = $"Spawn slot '{spawnSlot.name}' has an invalid player slot.";
                return false;
            }

            if (!uniqueSlots.Add(playerSlot))
            {
                error = $"Player slot '{playerSlot}' is registered more than once.";
                return false;
            }

            if (!IsInContextScene(spawnSlot.gameObject))
            {
                error =
                    $"Spawn slot '{spawnSlot.name}' must belong to the battle map scene.";
                return false;
            }

            BattleSpawnArea respawnArea = spawnSlot.RespawnArea;
            if (respawnArea == null)
            {
                error =
                    $"Player slot '{playerSlot}' is missing a respawn area.";
                return false;
            }

            if (!IsInContextScene(respawnArea.gameObject))
            {
                error =
                    $"The respawn area for player slot '{playerSlot}' must belong to the battle map scene.";
                return false;
            }
        }

        foreach (BattlePlayerSlot requiredSlot in
                 Enum.GetValues(typeof(BattlePlayerSlot)))
        {
            if (!uniqueSlots.Contains(requiredSlot))
            {
                error = $"The battle map is missing player slot '{requiredSlot}'.";
                return false;
            }
        }

        if (!TryGetSideSpawnSlots(out _, out _))
        {
            error =
                "The left and right player spawn slots must use different horizontal positions.";
            return false;
        }

        if (fallDeathZones.Count == 0)
        {
            error = "A battle map requires at least one player fall death zone.";
            return false;
        }

        HashSet<PlayerFallDeathZone> uniqueFallDeathZones = new();
        foreach (PlayerFallDeathZone fallDeathZone in fallDeathZones)
        {
            if (fallDeathZone == null)
            {
                error = "The battle map contains a missing player fall death zone.";
                return false;
            }

            if (!uniqueFallDeathZones.Add(fallDeathZone))
            {
                error =
                    $"Player fall death zone '{fallDeathZone.name}' is registered more than once.";
                return false;
            }

            if (!IsInContextScene(fallDeathZone.gameObject))
            {
                error =
                    $"Player fall death zone '{fallDeathZone.name}' must belong to the battle map scene.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    private bool IsInContextScene(GameObject target)
    {
        return target != null && target.scene == gameObject.scene;
    }
}
