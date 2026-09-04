using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum MatchState
{
    Waiting,
    Countdown,
    Playing,
    Finished
}

[DisallowMultipleComponent]
public sealed class MatchController : MonoBehaviour
{
    [SerializeField] private bool autoDiscoverParticipants = true;
    [SerializeField] private bool autoStart = true;
    [SerializeField] private List<PlayerLife> participants = new();

    private readonly HashSet<PlayerLife> fallingPlayers = new();
    private readonly Dictionary<PlayerLife, Coroutine> respawnCoroutines = new();

    private Coroutine outcomeEvaluationCoroutine;
    private bool competitiveOutcomeEnabled;
    private bool restartMatchWhenReenabled;

    public event Action MatchStarted;
    public event Action<PlayerLife> PlayerFell;
    public event Action<PlayerLife, int> PlayerLifeChanged;
    public event Action<PlayerLife> PlayerRespawned;
    public event Action<PlayerLife> PlayerEliminated;
    public event Action<PlayerLife> MatchEnded;
    public event Action ParticipantsChanged;

    public MatchState State { get; private set; } = MatchState.Waiting;
    public PlayerLife Winner { get; private set; }
    public bool IsMatchRunning => State == MatchState.Playing;
    public bool IsCountingDown => State == MatchState.Countdown;
    public bool IsMatchFinished => State == MatchState.Finished;
    public IReadOnlyList<PlayerLife> Participants => participants;

    private void Awake()
    {
        if (autoDiscoverParticipants)
        {
            DiscoverSceneParticipants();
        }
    }

    private void Start()
    {
        if (autoStart)
        {
            StartMatch();
        }
    }

    private void OnEnable()
    {
        if (!restartMatchWhenReenabled)
        {
            return;
        }

        restartMatchWhenReenabled = false;
        BeginMatch(respawnAllParticipants: true);
    }

    private void Update()
    {
        if (State == MatchState.Playing && HasMissingParticipant())
        {
            ScheduleOutcomeEvaluation();
        }
    }

    public bool StartMatch()
    {
        if (!isActiveAndEnabled
            || State == MatchState.Countdown
            || State == MatchState.Playing)
        {
            return false;
        }

        return BeginMatch(respawnAllParticipants: false);
    }

    public bool RestartMatch()
    {
        if (!isActiveAndEnabled || State == MatchState.Countdown)
        {
            return false;
        }

        return BeginMatch(respawnAllParticipants: true);
    }

    public bool PrepareMatchForCountdown(bool respawnAllParticipants)
    {
        if (!isActiveAndEnabled
            || State == MatchState.Countdown
            || (State == MatchState.Playing && !respawnAllParticipants))
        {
            return false;
        }

        State = MatchState.Countdown;
        if (!ResetMatch(respawnAllParticipants))
        {
            return false;
        }
        return true;
    }

    public bool CompleteCountdown()
    {
        if (!isActiveAndEnabled || State != MatchState.Countdown)
        {
            return false;
        }

        StartPreparedMatch();
        return true;
    }

    public bool RegisterParticipant(PlayerLife participant)
    {
        if (participant == null || participants.Contains(participant))
        {
            return false;
        }

        participants.Add(participant);

        if (State == MatchState.Playing)
        {
            participant.ResetForMatch();
            competitiveOutcomeEnabled |= participants.Count >= 2;

            if (!participant.gameObject.activeSelf
                && participant.TryGetComponent(out PlayerRespawner respawner))
            {
                respawner.Respawn();
            }

            ScheduleOutcomeEvaluation();
            PlayerLifeChanged?.Invoke(participant, participant.Life);
        }

        ParticipantsChanged?.Invoke();

        return true;
    }

    public bool UnregisterParticipant(PlayerLife participant)
    {
        if (ReferenceEquals(participant, null))
        {
            return false;
        }

        int participantIndex = participants.FindIndex(
            registeredParticipant => ReferenceEquals(registeredParticipant, participant));
        if (participantIndex < 0)
        {
            return false;
        }

        participants.RemoveAt(participantIndex);

        CancelRespawn(participant);
        fallingPlayers.Remove(participant);
        ParticipantsChanged?.Invoke();

        if (State == MatchState.Playing)
        {
            ScheduleOutcomeEvaluation();
        }

        return true;
    }

    public bool IsParticipant(PlayerLife participant)
    {
        return participant != null && participants.Contains(participant);
    }

    public bool ReportPlayerFall(PlayerLife participant)
    {
        if (!isActiveAndEnabled
            || State != MatchState.Playing
            || participant == null
            || !participants.Contains(participant)
            || participant.Life <= 0
            || !fallingPlayers.Add(participant))
        {
            return false;
        }

        if (!participant.TryLoseLife())
        {
            fallingPlayers.Remove(participant);
            return false;
        }

        int remainingLife = participant.Life;
        bool isEliminated = remainingLife <= 0;
        participant.gameObject.SetActive(false);

        if (isEliminated)
        {
            ScheduleOutcomeEvaluation();
        }
        else if (!participant.TryGetComponent(out PlayerRespawner respawner))
        {
            Debug.LogError(
                "A match participant requires PlayerRespawner to return after a fall.",
                participant);
        }
        else
        {
            Coroutine respawnCoroutine = StartCoroutine(
                RespawnAfterDelay(participant, respawner));
            respawnCoroutines.Add(participant, respawnCoroutine);
        }

        PlayerFell?.Invoke(participant);
        PlayerLifeChanged?.Invoke(participant, remainingLife);
        if (isEliminated)
        {
            PlayerEliminated?.Invoke(participant);
        }

        return true;
    }

    private bool BeginMatch(bool respawnAllParticipants)
    {
        if (!ResetMatch(respawnAllParticipants))
        {
            return false;
        }

        StartPreparedMatch();
        return true;
    }

    private bool ResetMatch(bool respawnAllParticipants)
    {
        PruneParticipants();
        if (participants.Count == 0)
        {
            State = MatchState.Waiting;
            Winner = null;
            return false;
        }

        CancelOutcomeEvaluation();
        CancelAllRespawns();
        fallingPlayers.Clear();
        Winner = null;
        competitiveOutcomeEnabled = participants.Count >= 2;

        foreach (PlayerLife participant in participants)
        {
            participant.ResetForMatch();
            PlayerLifeChanged?.Invoke(participant, participant.Life);

            if (!participant.TryGetComponent(out PlayerRespawner respawner))
            {
                continue;
            }

            if (respawnAllParticipants)
            {
                respawner.RespawnAtInitialPosition();
            }
            else if (!participant.gameObject.activeSelf)
            {
                respawner.Respawn();
            }
        }

        return true;
    }

    private void StartPreparedMatch()
    {
        State = MatchState.Playing;
        ScheduleOutcomeEvaluation();
        MatchStarted?.Invoke();
    }

    private IEnumerator RespawnAfterDelay(
        PlayerLife participant,
        PlayerRespawner respawner)
    {
        yield return new WaitForSeconds(respawner.RespawnDelay);

        respawnCoroutines.Remove(participant);
        if (State != MatchState.Playing
            || participant == null
            || respawner == null
            || participant.Life <= 0
            || !participants.Contains(participant))
        {
            fallingPlayers.Remove(participant);
            yield break;
        }

        respawner.Respawn();
        if (participant.TryGetComponent(out PlayerRespawnShield respawnShield))
        {
            respawnShield.Activate();
        }

        fallingPlayers.Remove(participant);
        PlayerRespawned?.Invoke(participant);
    }

    private void ScheduleOutcomeEvaluation()
    {
        if (outcomeEvaluationCoroutine == null)
        {
            outcomeEvaluationCoroutine = StartCoroutine(EvaluateOutcomeAfterPhysicsStep());
        }
    }

    private IEnumerator EvaluateOutcomeAfterPhysicsStep()
    {
        // Wait until the next frame so every collision callback from the current
        // physics batch can report its fall before the result is decided. Unlike
        // WaitForFixedUpdate, this still completes if a result UI pauses time.
        yield return null;
        outcomeEvaluationCoroutine = null;

        if (State == MatchState.Playing)
        {
            EvaluateOutcome();
        }
    }

    private void EvaluateOutcome()
    {
        PruneParticipants();

        int survivingParticipantCount = 0;
        PlayerLife survivingParticipant = null;
        foreach (PlayerLife participant in participants)
        {
            if (participant.Life <= 0)
            {
                continue;
            }

            survivingParticipantCount++;
            survivingParticipant = participant;
        }

        if (survivingParticipantCount == 0)
        {
            FinishMatch(null);
        }
        else if (competitiveOutcomeEnabled && survivingParticipantCount == 1)
        {
            FinishMatch(survivingParticipant);
        }
    }

    private void FinishMatch(PlayerLife winner)
    {
        State = MatchState.Finished;
        Winner = winner;
        CancelAllRespawns();
        fallingPlayers.Clear();

        if (winner != null
            && !winner.gameObject.activeSelf
            && winner.TryGetComponent(out PlayerRespawner respawner))
        {
            fallingPlayers.Remove(winner);
            respawner.Respawn();
        }

        MatchEnded?.Invoke(winner);
    }

    private void DiscoverSceneParticipants()
    {
        PlayerLife[] sceneParticipants = FindObjectsByType<PlayerLife>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        foreach (PlayerLife participant in sceneParticipants)
        {
            if (participant != null
                && participant.gameObject.scene == gameObject.scene
                && !participants.Contains(participant))
            {
                participants.Add(participant);
            }
        }

        PruneParticipants();
    }

    private void PruneParticipants()
    {
        int previousParticipantCount = participants.Count;
        participants.RemoveAll(participant => participant == null);

        HashSet<PlayerLife> uniqueParticipants = new();
        for (int i = participants.Count - 1; i >= 0; i--)
        {
            if (!uniqueParticipants.Add(participants[i]))
            {
                participants.RemoveAt(i);
            }
        }

        if (participants.Count != previousParticipantCount)
        {
            ParticipantsChanged?.Invoke();
        }
    }

    private bool HasMissingParticipant()
    {
        foreach (PlayerLife participant in participants)
        {
            if (participant == null)
            {
                return true;
            }
        }

        return false;
    }

    private void CancelRespawn(PlayerLife participant)
    {
        if (respawnCoroutines.TryGetValue(participant, out Coroutine coroutine))
        {
            StopCoroutine(coroutine);
            respawnCoroutines.Remove(participant);
        }
    }

    private void CancelAllRespawns()
    {
        foreach (Coroutine coroutine in respawnCoroutines.Values)
        {
            if (coroutine != null)
            {
                StopCoroutine(coroutine);
            }
        }

        respawnCoroutines.Clear();
    }

    private void CancelOutcomeEvaluation()
    {
        if (outcomeEvaluationCoroutine == null)
        {
            return;
        }

        StopCoroutine(outcomeEvaluationCoroutine);
        outcomeEvaluationCoroutine = null;
    }

    private void OnDisable()
    {
        restartMatchWhenReenabled = State == MatchState.Playing
            || State == MatchState.Countdown;
        CancelOutcomeEvaluation();
        CancelAllRespawns();
        fallingPlayers.Clear();

        if (restartMatchWhenReenabled)
        {
            State = MatchState.Waiting;
            Winner = null;
        }
    }
}
