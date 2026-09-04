using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-1000)]
[DisallowMultipleComponent]
public sealed class BattleSetup : MonoBehaviour
{
    [SerializeField] private MatchController matchController;
    [SerializeField] private BulletPool bulletPool;
    [SerializeField] private MatchLifeHud lifeHud;
    [SerializeField] private BattleCountdown countdown;
    [SerializeField] private BattleMapContext mapContext;
    [SerializeField] private bool autoStart = true;

    private readonly List<BattleParticipantSlot> participantSlots = new();

    public MatchController MatchController => matchController;
    public BulletPool BulletPool => bulletPool;
    public MatchLifeHud LifeHud => lifeHud;
    public BattleCountdown Countdown => countdown;
    public BattleMapContext MapContext => mapContext;
    public IReadOnlyList<BattleParticipantSlot> ParticipantSlots =>
        participantSlots;
    public bool IsInitialized { get; private set; }

    private void Awake()
    {
        Initialize();
    }

    private void Start()
    {
        if (autoStart)
        {
            StartBattle();
        }
    }

    public bool Initialize()
    {
        if (IsInitialized)
        {
            return true;
        }

        ResolveSystemReferences();
        if (!TryValidateSystems(out string error)
            || !TryResolveMapContext(out error)
            || !mapContext.TryValidate(out error)
            || !TryApplyLaunchConfiguration(out error)
            || !TryResolveParticipantSlots(out error))
        {
            Debug.LogError($"BattleSetup initialization failed: {error}", this);
            return false;
        }

        if (!mapContext.BindFallDeathZones(matchController))
        {
            Debug.LogError(
                "BattleSetup initialization failed while binding player fall death zones.",
                this);
            return false;
        }

        lifeHud.Bind(matchController, mapContext);
        countdown.Bind(matchController);

        foreach (BattleParticipantSlot participantSlot in participantSlots)
        {
            PlayerLife participant = participantSlot.Participant;
            if (!mapContext.TryGetSpawnSlot(
                    participantSlot.Slot,
                    out BattleSpawnSlot spawnSlot))
            {
                Debug.LogError(
                    $"BattleSetup could not resolve the spawn slot for player slot '{participantSlot.Slot}'.",
                    participantSlot);
                return false;
            }

            PlayerRespawner respawner = participant.GetComponent<PlayerRespawner>();
            respawner.BindRespawnArea(spawnSlot.RespawnArea);
            respawner.BindInitialSpawnPosition(spawnSlot.InitialPosition);

            if (participant.TryGetComponent(out PlayerShooter shooter))
            {
                shooter.Bind(bulletPool);
            }

            if (participant.TryGetComponent(out AIPlayerCommandSource aiSource))
            {
                aiSource.Bind(matchController);
            }

            if (!matchController.IsParticipant(participant)
                && !matchController.RegisterParticipant(participant))
            {
                Debug.LogError(
                    $"BattleSetup could not register participant '{participant.name}'.",
                    participant);
                return false;
            }
        }

        ApplyInitialFacingDirections();
        IsInitialized = true;
        lifeHud.Refresh();
        return true;
    }

    public bool StartBattle()
    {
        if (!Initialize())
        {
            return false;
        }

        if (matchController.IsMatchRunning || countdown.IsCountingDown)
        {
            return true;
        }

        return StartCountdown(respawnAllParticipants: false);
    }

    public bool RestartBattle()
    {
        if (!Initialize() || countdown.IsCountingDown)
        {
            return false;
        }

        return StartCountdown(respawnAllParticipants: true);
    }

    public bool TryGetParticipant(
        BattlePlayerSlot playerSlot,
        out PlayerLife participant)
    {
        foreach (BattleParticipantSlot participantSlot in participantSlots)
        {
            if (participantSlot != null && participantSlot.Slot == playerSlot)
            {
                participant = participantSlot.Participant;
                return participant != null;
            }
        }

        participant = null;
        return false;
    }

    private void ResolveSystemReferences()
    {
        if (matchController == null)
        {
            matchController = GetComponentInChildren<MatchController>(true);
        }

        if (bulletPool == null)
        {
            bulletPool = GetComponentInChildren<BulletPool>(true);
        }

        if (lifeHud == null)
        {
            lifeHud = GetComponentInChildren<MatchLifeHud>(true);
        }

        if (countdown == null)
        {
            countdown = GetComponentInChildren<BattleCountdown>(true);
        }
    }

    private bool TryValidateSystems(out string error)
    {
        if (matchController == null)
        {
            error = "A MatchController is required.";
            return false;
        }

        if (bulletPool == null)
        {
            error = "A BulletPool is required.";
            return false;
        }

        if (lifeHud == null)
        {
            error = "A MatchLifeHud is required.";
            return false;
        }

        if (countdown == null)
        {
            error = "A BattleCountdown is required.";
            return false;
        }

        if (matchController.gameObject.scene != gameObject.scene
            || bulletPool.gameObject.scene != gameObject.scene
            || lifeHud.gameObject.scene != gameObject.scene
            || countdown.gameObject.scene != gameObject.scene)
        {
            error =
                "Every BattleSetup system must belong to the same scene as the setup.";
            return false;
        }

        if (matchController.State != MatchState.Waiting)
        {
            error =
                "The MatchController must be waiting when BattleSetup initializes.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private bool StartCountdown(bool respawnAllParticipants)
    {
        if (!matchController.PrepareMatchForCountdown(
                respawnAllParticipants))
        {
            return false;
        }

        ApplyInitialFacingDirections();
        if (countdown.StartCountdown(HandleCountdownCompleted))
        {
            return true;
        }

        return matchController.CompleteCountdown();
    }

    private void ApplyInitialFacingDirections()
    {
        if (mapContext == null
            || !mapContext.TryGetSideSpawnSlots(
                out BattleSpawnSlot leftSpawnSlot,
                out BattleSpawnSlot rightSpawnSlot))
        {
            return;
        }

        ApplyInitialFacing(leftSpawnSlot, 1f);
        ApplyInitialFacing(rightSpawnSlot, -1f);
    }

    private void ApplyInitialFacing(
        BattleSpawnSlot spawnSlot,
        float horizontalDirection)
    {
        if (spawnSlot == null
            || !TryGetParticipant(spawnSlot.Slot, out PlayerLife participant)
            || !participant.TryGetComponent(out PlayerController2D controller))
        {
            return;
        }

        controller.SetFacingDirection(horizontalDirection);
    }

    private void HandleCountdownCompleted()
    {
        if (matchController == null || !matchController.CompleteCountdown())
        {
            Debug.LogError(
                "BattleSetup could not complete the prepared match countdown.",
                this);
        }
    }

    private bool TryResolveParticipantSlots(out string error)
    {
        participantSlots.Clear();

        BattleParticipantSlot[] sceneSlotComponents =
            FindObjectsByType<BattleParticipantSlot>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        foreach (BattleParticipantSlot participantSlot in sceneSlotComponents)
        {
            if (participantSlot != null
                && participantSlot.gameObject.scene == gameObject.scene)
            {
                participantSlots.Add(participantSlot);
            }
        }

        if (participantSlots.Count != mapContext.SpawnSlots.Count)
        {
            error =
                $"BattleSetup requires {mapContext.SpawnSlots.Count} participant slot assignments, but found {participantSlots.Count}.";
            participantSlots.Clear();
            return false;
        }

        HashSet<BattlePlayerSlot> uniqueSlots = new();
        HashSet<PlayerLife> uniqueParticipants = new();
        foreach (BattleParticipantSlot participantSlot in participantSlots)
        {
            BattlePlayerSlot playerSlot = participantSlot.Slot;
            if (!Enum.IsDefined(typeof(BattlePlayerSlot), playerSlot))
            {
                error =
                    $"Participant slot on '{participantSlot.name}' has an invalid player slot.";
                participantSlots.Clear();
                return false;
            }

            if (!uniqueSlots.Add(playerSlot))
            {
                error =
                    $"More than one participant is assigned to player slot '{playerSlot}'.";
                participantSlots.Clear();
                return false;
            }

            PlayerLife participant = participantSlot.Participant;
            if (participant == null)
            {
                error =
                    $"Participant slot '{playerSlot}' is missing its PlayerLife.";
                participantSlots.Clear();
                return false;
            }

            if (participant.gameObject.scene != gameObject.scene)
            {
                error =
                    $"Participant for player slot '{playerSlot}' must belong to the battle scene.";
                participantSlots.Clear();
                return false;
            }

            if (!uniqueParticipants.Add(participant))
            {
                error =
                    $"Participant '{participant.name}' is assigned to more than one player slot.";
                participantSlots.Clear();
                return false;
            }

            if (!mapContext.TryGetSpawnSlot(playerSlot, out _))
            {
                error =
                    $"The battle map does not define player slot '{playerSlot}'.";
                participantSlots.Clear();
                return false;
            }

            if (!participant.TryGetComponent(out PlayerRespawner _))
            {
                error =
                    $"Participant '{participant.name}' requires a PlayerRespawner.";
                participantSlots.Clear();
                return false;
            }
        }

        PlayerLife[] sceneParticipants = FindObjectsByType<PlayerLife>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        foreach (PlayerLife participant in sceneParticipants)
        {
            if (participant == null
                || participant.gameObject.scene != gameObject.scene)
            {
                continue;
            }

            if (!uniqueParticipants.Contains(participant))
            {
                error =
                    $"Participant '{participant.name}' requires a BattleParticipantSlot.";
                participantSlots.Clear();
                return false;
            }
        }

        participantSlots.Sort(
            (left, right) => left.Slot.CompareTo(right.Slot));
        error = string.Empty;
        return true;
    }

    private bool TryApplyLaunchConfiguration(out string error)
    {
        if (!BattleSession.TryGetForScene(
                gameObject.scene.path,
                out BattleLaunchConfiguration configuration))
        {
            error = string.Empty;
            return true;
        }

        PlayerInputCommandSource[] humanSources =
            FindSceneComponents<PlayerInputCommandSource>();
        AIPlayerCommandSource[] aiSources =
            FindSceneComponents<AIPlayerCommandSource>();

        if (humanSources.Length != 1 || aiSources.Length != 1)
        {
            error =
                "The current single-player roster requires exactly one human participant and one AI participant.";
            return false;
        }

        aiSources[0].ApplyDifficultyProfile(
            configuration.AIDifficultyProfile);

        BattleParticipantSlot humanParticipantSlot =
            humanSources[0].GetComponent<BattleParticipantSlot>();
        BattleParticipantSlot aiParticipantSlot =
            aiSources[0].GetComponent<BattleParticipantSlot>();
        if (humanParticipantSlot == null || aiParticipantSlot == null)
        {
            error =
                "Every configured participant requires a BattleParticipantSlot.";
            return false;
        }

        if (!mapContext.TryGetSideSpawnSlot(
                BattleTeamSide.Left,
                out BattleSpawnSlot humanSpawnSlot)
            || !mapContext.TryGetSideSpawnSlot(
                BattleTeamSide.Right,
                out BattleSpawnSlot aiSpawnSlot))
        {
            error =
                "The battle map could not resolve distinct left and right spawn slots.";
            return false;
        }

        humanParticipantSlot.Assign(humanSpawnSlot.Slot);
        aiParticipantSlot.Assign(aiSpawnSlot.Slot);
        PlaceParticipantAtInitialSpawn(
            humanParticipantSlot.Participant,
            humanSpawnSlot);
        PlaceParticipantAtInitialSpawn(
            aiParticipantSlot.Participant,
            aiSpawnSlot);

        error = string.Empty;
        return true;
    }

    private T[] FindSceneComponents<T>() where T : Component
    {
        T[] allComponents = FindObjectsByType<T>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        List<T> sceneComponents = new();
        foreach (T component in allComponents)
        {
            if (component != null
                && component.gameObject.scene == gameObject.scene)
            {
                sceneComponents.Add(component);
            }
        }

        return sceneComponents.ToArray();
    }

    private static void PlaceParticipantAtInitialSpawn(
        PlayerLife participant,
        BattleSpawnSlot spawnSlot)
    {
        Vector2 spawnPosition = spawnSlot.InitialPosition;
        Vector3 currentPosition = participant.transform.position;
        participant.transform.position = new Vector3(
            spawnPosition.x,
            spawnPosition.y,
            currentPosition.z);

        if (!participant.TryGetComponent(out Rigidbody2D body))
        {
            return;
        }

        body.position = spawnPosition;
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
    }

    private bool TryResolveMapContext(out string error)
    {
        if (mapContext != null)
        {
            if (mapContext.gameObject.scene == gameObject.scene)
            {
                error = string.Empty;
                return true;
            }

            error = "The assigned BattleMapContext belongs to another scene.";
            return false;
        }

        BattleMapContext[] contexts = FindObjectsByType<BattleMapContext>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        foreach (BattleMapContext candidate in contexts)
        {
            if (candidate == null || candidate.gameObject.scene != gameObject.scene)
            {
                continue;
            }

            if (mapContext != null)
            {
                mapContext = null;
                error =
                    "More than one BattleMapContext exists in the battle scene.";
                return false;
            }

            mapContext = candidate;
        }

        if (mapContext == null)
        {
            error = "A BattleMapContext is required in the battle scene.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private void OnValidate()
    {
        ResolveSystemReferences();
    }
}
