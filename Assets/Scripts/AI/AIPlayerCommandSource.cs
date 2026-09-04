using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerCommandReceiver))]
[RequireComponent(typeof(PlayerLife))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class AIPlayerCommandSource : MonoBehaviour
{
    internal enum ExplorationVerticalAction
    {
        Jump,
        Drop
    }

    private const float MinimumDecisionInterval = 0.02f;
    private const float DirectionEpsilon = 0.01f;
    private const float PlatformSeparationEpsilon = 0.05f;
    private const int PlatformHitBufferSize = 16;

    [Header("References")]
    [SerializeField] private MatchController matchController;
    [SerializeField] private PlayerCommandReceiver commandReceiver;
    [SerializeField] private PlayerLife selfLife;

    [Header("Decision")]
    [SerializeField] private AIDifficultyDefinition difficultyProfile;
    [SerializeField, Min(MinimumDecisionInterval)]
    private float decisionInterval = 0.1f;
    [SerializeField] private int randomSeed;

    [Header("Roaming")]
    [SerializeField] private Vector2 roamDurationRange = new(0.6f, 2f);
    [SerializeField] private LayerMask platformLayers = 1 << 6;
    [SerializeField, Min(0f)] private float edgeLookAhead = 0.4f;
    [SerializeField, Min(0.01f)] private float edgeProbeDepth = 0.3f;
    [SerializeField, Min(0f)] private float landingMargin = 0.1f;

    [Header("Vertical Exploration")]
    [SerializeField] private Vector2 explorationIntervalRange = new(2.5f, 5f);
    [SerializeField, Range(0f, 1f)] private float explorationDropChance = 0.4f;

    [Header("Height Matching")]
    [SerializeField, Min(0f)] private float heightActionThreshold = 0.5f;
    [SerializeField, Min(0f)] private float shootHeightTolerance = 0.5f;
    [SerializeField, Min(0.01f)] private float upperPlatformScanDistance = 2f;
    [SerializeField, Min(0.01f)] private float lowerPlatformScanDistance = 2.25f;
    [SerializeField] private float additionalJumpVelocityThreshold = 1f;
    [SerializeField, Min(0f)] private float verticalActionCooldown = 0.15f;

    [Header("Air Recovery")]
    [SerializeField, Min(0.01f)] private float recoveryVerticalSearchDistance = 8f;
    [SerializeField, Min(0f)] private float recoveryReachSlack = 0.4f;

    private readonly List<Collider2D> platformColliders = new();
    private readonly RaycastHit2D[] platformHitBuffer =
        new RaycastHit2D[PlatformHitBufferSize];

    private MatchController subscribedController;
    private PlayerLife currentTarget;
    private PlayerController2D controller;
    private Rigidbody2D body;
    private BoxCollider2D bodyCollider;
    private System.Random random;
    private float nextDecisionTime;
    private float nextRoamDirectionChangeTime;
    private float nextVerticalActionTime;
    private float nextExplorationActionTime;
    private float roamDirection;
    private float lastHorizontalMovement;
    private bool hasSubmittedCommand;
    private bool isClimbing;
    private bool additionalJumpIssued;
    private bool hasScheduledExplorationAction;
    private PlayerCommand lastSubmittedCommand;

    public MatchController MatchController => matchController;
    public PlayerCommandReceiver CommandReceiver => commandReceiver;
    public PlayerLife SelfLife => selfLife;
    public AIDifficultyDefinition DifficultyProfile => difficultyProfile;
    public PlayerLife CurrentTarget => currentTarget;
    public float DecisionInterval => decisionInterval;
    public Vector2 RoamDurationRange => roamDurationRange;
    public Vector2 ExplorationIntervalRange => explorationIntervalRange;
    public float ExplorationDropChance => explorationDropChance;
    public float RoamDirection => roamDirection;
    public float HeightActionThreshold => heightActionThreshold;
    public float ShootHeightTolerance => shootHeightTolerance;
    public float UpperPlatformScanDistance => upperPlatformScanDistance;
    public float LowerPlatformScanDistance => lowerPlatformScanDistance;
    public float VerticalActionCooldown => verticalActionCooldown;
    public float LastHorizontalMovement => hasSubmittedCommand
        ? lastHorizontalMovement
        : 0f;
    public bool HasSubmittedCommand => hasSubmittedCommand;
    public PlayerCommand LastSubmittedCommand => lastSubmittedCommand;

    public void Bind(MatchController controller)
    {
        if (matchController == controller)
        {
            SubscribeToMatch();
            return;
        }

        UnsubscribeFromMatch();
        matchController = controller;
        currentTarget = null;
        nextDecisionTime = 0f;
        SubscribeToMatch();
    }

    public void ApplyDifficultyProfile(AIDifficultyDefinition profile)
    {
        if (profile == null)
        {
            throw new ArgumentNullException(nameof(profile));
        }

        if (!profile.TryValidate(out string error))
        {
            throw new ArgumentException(
                $"The AI difficulty profile is invalid: {error}",
                nameof(profile));
        }

        difficultyProfile = profile;
        decisionInterval = profile.DecisionInterval;
        roamDurationRange = profile.RoamDurationRange;
        explorationIntervalRange = profile.ExplorationIntervalRange;
        explorationDropChance = profile.ExplorationDropChance;
        heightActionThreshold = profile.HeightActionThreshold;
        shootHeightTolerance = profile.ShootHeightTolerance;
        upperPlatformScanDistance = profile.UpperPlatformScanDistance;
        lowerPlatformScanDistance = profile.LowerPlatformScanDistance;
        verticalActionCooldown = profile.VerticalActionCooldown;
        ResetDecisionState();
    }

    private void Awake()
    {
        ResolveLocalReferences();
        if (difficultyProfile != null)
        {
            ApplyDifficultyProfile(difficultyProfile);
        }

        ResolveMatchController();
        InitializeRandom();
        RefreshPlatformColliders();
    }

    private void OnEnable()
    {
        ResolveRuntimeReferences();
        InitializeRandom();
        RefreshPlatformColliders();
        ResetDecisionState();
    }

    private void Update()
    {
        ResolveRuntimeReferences();

        if (!CanMakeDecision())
        {
            currentTarget = null;
            ResetCommands();
            return;
        }

        if (currentTarget != null && !IsValidTarget(currentTarget))
        {
            currentTarget = null;
            nextDecisionTime = 0f;
            ResetCommands();
        }

        float now = Time.unscaledTime;
        if (NeedsImmediateEdgeTurn())
        {
            ReverseRoamDirection(now);
            nextDecisionTime = 0f;
        }

        if (now < nextDecisionTime)
        {
            return;
        }

        nextDecisionTime = now + Mathf.Max(MinimumDecisionInterval, decisionInterval);
        EvaluateDecision(now);
    }

    private void OnDisable()
    {
        UnsubscribeFromMatch();
        currentTarget = null;
        ResetDecisionState();
        ResetCommands(force: true);
    }

    internal void EvaluateDecision()
    {
        EvaluateDecision(Time.unscaledTime);
    }

    internal static float SelectRoamDirection(float randomValue)
    {
        return Mathf.Clamp01(randomValue) < 0.5f ? -1f : 1f;
    }

    internal static float SelectRoamDuration(
        float randomValue,
        float minimumDuration,
        float maximumDuration)
    {
        float minimum = Mathf.Max(0f, Mathf.Min(minimumDuration, maximumDuration));
        float maximum = Mathf.Max(minimum, Mathf.Max(minimumDuration, maximumDuration));
        return Mathf.Lerp(minimum, maximum, Mathf.Clamp01(randomValue));
    }

    internal static ExplorationVerticalAction SelectExplorationVerticalAction(
        float randomValue,
        float dropChance,
        bool hasSuitablePlatformBelow)
    {
        if (!hasSuitablePlatformBelow)
        {
            return ExplorationVerticalAction.Jump;
        }

        return Mathf.Clamp01(randomValue) < Mathf.Clamp01(dropChance)
            ? ExplorationVerticalAction.Drop
            : ExplorationVerticalAction.Jump;
    }

    internal static float CalculateRecoveryMovement(
        float currentX,
        float horizontalVelocity,
        float timeToLanding,
        float safeMinimumX,
        float safeMaximumX)
    {
        float minimum = Mathf.Min(safeMinimumX, safeMaximumX);
        float maximum = Mathf.Max(safeMinimumX, safeMaximumX);
        if (currentX < minimum)
        {
            return 1f;
        }

        if (currentX > maximum)
        {
            return -1f;
        }

        float predictedX = currentX
            + horizontalVelocity * Mathf.Max(0f, timeToLanding);

        if (predictedX < minimum)
        {
            return 1f;
        }

        if (predictedX > maximum)
        {
            return -1f;
        }

        return 0f;
    }

    internal static bool IsHeightAligned(
        float selfY,
        float targetY,
        float tolerance)
    {
        return Mathf.Abs(targetY - selfY) <= Mathf.Max(0f, tolerance);
    }

    internal void EvaluateDecision(
        float now,
        float? explorationRandomSample = null)
    {
        ResolveRuntimeReferences();

        if (!CanMakeDecision())
        {
            currentTarget = null;
            ResetCommands();
            return;
        }

        if (!IsValidTarget(currentTarget))
        {
            currentTarget = FindClosestTarget();
        }

        if (currentTarget == null)
        {
            ResetCommands();
            return;
        }

        EnsureRoamDirection(now);
        EnsureExplorationActionScheduled(now);
        RemoveInvalidPlatformColliders();
        if (platformColliders.Count == 0)
        {
            RefreshPlatformColliders();
        }

        if (controller != null && controller.IsGrounded)
        {
            isClimbing = false;
            additionalJumpIssued = false;
        }

        float horizontalMovement = roamDirection;
        if (controller != null
            && controller.IsGrounded
            && !controller.IsDroppingThroughPlatform
            && !HasGroundAhead(horizontalMovement))
        {
            ReverseRoamDirection(now);
            horizontalMovement = roamDirection;
        }

        if (controller != null && controller.IsDroppingThroughPlatform)
        {
            horizontalMovement = 0f;
        }
        else if (TryCalculateAirRecoveryMovement(out float recoveryMovement))
        {
            horizontalMovement = recoveryMovement;
        }
        else if (isClimbing && controller != null && !controller.IsGrounded)
        {
            horizontalMovement = 0f;
        }

        bool jumpPressed = false;
        bool dropPressed = false;
        bool attackPressed = false;
        float aimHorizontal = 0f;

        float selfY = GetColliderCenterY(bodyCollider, transform);
        Collider2D targetCollider = currentTarget.GetComponent<Collider2D>();
        float targetY = GetColliderCenterY(targetCollider, currentTarget.transform);
        float heightDelta = targetY - selfY;
        bool targetSettledOnGround = IsTargetSettledOnGround();
        bool heightAligned = IsHeightAligned(
            selfY,
            targetY,
            shootHeightTolerance);

        bool shouldUseAdditionalJump = targetSettledOnGround
            && now >= nextVerticalActionTime
            && controller != null
            && isClimbing
            && !controller.IsGrounded
            && !additionalJumpIssued
            && controller.RemainingAdditionalJumps > 0
            && body != null
            && body.linearVelocity.y <= additionalJumpVelocityThreshold
            && heightDelta > DirectionEpsilon;

        bool startedExplorationAction = false;
        if (shouldUseAdditionalJump)
        {
            jumpPressed = true;
            horizontalMovement = 0f;
            additionalJumpIssued = true;
            nextVerticalActionTime = now + verticalActionCooldown;
        }
        else
        {
            startedExplorationAction = TryStartExplorationAction(
                now,
                heightDelta,
                targetSettledOnGround,
                explorationRandomSample,
                ref horizontalMovement,
                out jumpPressed,
                out dropPressed);
        }

        if (!shouldUseAdditionalJump && !startedExplorationAction)
        {
            if (heightAligned)
            {
                float targetDirection = currentTarget.transform.position.x
                    - transform.position.x;
                aimHorizontal = Mathf.Abs(targetDirection) > DirectionEpsilon
                    ? Mathf.Sign(targetDirection)
                    : controller != null
                        ? controller.FacingDirection.x
                        : 0f;

                PlayerShooter shooter = commandReceiver != null
                    ? commandReceiver.Shooter
                    : null;
                attackPressed = shooter != null && shooter.CanFire;
            }
            else if (targetSettledOnGround
                && now >= nextVerticalActionTime)
            {
                if (heightDelta > heightActionThreshold)
                {
                    if (controller != null
                        && controller.IsGrounded
                        && TryFindPlatformAbove(out _))
                    {
                        jumpPressed = true;
                        horizontalMovement = 0f;
                        isClimbing = true;
                        additionalJumpIssued = false;
                        nextVerticalActionTime = now + verticalActionCooldown;
                        ScheduleNextExplorationAction(now);
                    }
                }
                else if (heightDelta < -heightActionThreshold
                    && controller != null
                    && controller.IsGrounded
                    && !controller.IsDroppingThroughPlatform
                    && TryFindPlatformBelow(out _))
                {
                    dropPressed = true;
                    horizontalMovement = 0f;
                    nextVerticalActionTime = now + verticalActionCooldown;
                    ScheduleNextExplorationAction(now);
                }
            }
        }

        SubmitCommand(
            horizontalMovement,
            jumpPressed,
            dropPressed,
            attackPressed,
            aimHorizontal);
    }

    private bool TryStartExplorationAction(
        float now,
        float heightDelta,
        bool targetSettledOnGround,
        float? randomSample,
        ref float horizontalMovement,
        out bool jumpPressed,
        out bool dropPressed)
    {
        jumpPressed = false;
        dropPressed = false;
        if (!hasScheduledExplorationAction
            || now < nextExplorationActionTime
            || now < nextVerticalActionTime
            || !targetSettledOnGround
            || Mathf.Abs(heightDelta) > heightActionThreshold
            || controller == null
            || !controller.IsGrounded
            || controller.IsDroppingThroughPlatform)
        {
            return false;
        }

        bool hasSuitablePlatformBelow = TryFindPlatformBelow(out _);
        ExplorationVerticalAction action = SelectExplorationVerticalAction(
            randomSample ?? NextRandomValue(),
            explorationDropChance,
            hasSuitablePlatformBelow);

        if (action == ExplorationVerticalAction.Drop)
        {
            dropPressed = true;
            horizontalMovement = 0f;
        }
        else
        {
            jumpPressed = true;
        }

        nextVerticalActionTime = now + verticalActionCooldown;
        ScheduleNextExplorationAction(now);
        return true;
    }

    private bool CanMakeDecision()
    {
        return matchController != null
            && matchController.isActiveAndEnabled
            && matchController.IsMatchRunning
            && selfLife != null
            && selfLife.Life > 0
            && matchController.IsParticipant(selfLife);
    }

    private bool IsValidTarget(PlayerLife candidate)
    {
        return candidate != null
            && candidate != selfLife
            && candidate.Life > 0
            && candidate.gameObject.activeInHierarchy
            && candidate.gameObject.scene == gameObject.scene
            && matchController != null
            && matchController.IsParticipant(candidate);
    }

    private PlayerLife FindClosestTarget()
    {
        PlayerLife closestTarget = null;
        float closestSqrDistance = float.PositiveInfinity;

        foreach (PlayerLife candidate in matchController.Participants)
        {
            if (!IsValidTarget(candidate))
            {
                continue;
            }

            float sqrDistance = (candidate.transform.position - transform.position)
                .sqrMagnitude;
            if (sqrDistance < closestSqrDistance)
            {
                closestTarget = candidate;
                closestSqrDistance = sqrDistance;
            }
        }

        return closestTarget;
    }

    private bool IsTargetSettledOnGround()
    {
        PlayerController2D targetController =
            currentTarget.GetComponent<PlayerController2D>();
        return targetController == null || targetController.IsGrounded;
    }

    private void EnsureRoamDirection(float now)
    {
        if (Mathf.Abs(roamDirection) > DirectionEpsilon
            && now < nextRoamDirectionChangeTime)
        {
            return;
        }

        roamDirection = SelectRoamDirection(NextRandomValue());
        nextRoamDirectionChangeTime = now + SelectRoamDuration(
            NextRandomValue(),
            roamDurationRange.x,
            roamDurationRange.y);
    }

    private void EnsureExplorationActionScheduled(float now)
    {
        if (!hasScheduledExplorationAction)
        {
            ScheduleNextExplorationAction(now);
        }
    }

    private void ScheduleNextExplorationAction(float now)
    {
        nextExplorationActionTime = now + SelectRoamDuration(
            NextRandomValue(),
            explorationIntervalRange.x,
            explorationIntervalRange.y);
        hasScheduledExplorationAction = true;
    }

    private void ReverseRoamDirection(float now)
    {
        float direction = Mathf.Abs(roamDirection) > DirectionEpsilon
            ? roamDirection
            : lastHorizontalMovement;
        roamDirection = Mathf.Abs(direction) > DirectionEpsilon
            ? -Mathf.Sign(direction)
            : SelectRoamDirection(NextRandomValue());
        nextRoamDirectionChangeTime = now + SelectRoamDuration(
            NextRandomValue(),
            roamDurationRange.x,
            roamDurationRange.y);
    }

    private bool NeedsImmediateEdgeTurn()
    {
        return hasSubmittedCommand
            && controller != null
            && controller.IsGrounded
            && !controller.IsDroppingThroughPlatform
            && Mathf.Abs(lastHorizontalMovement) > DirectionEpsilon
            && !HasGroundAhead(lastHorizontalMovement);
    }

    private bool HasGroundAhead(float direction)
    {
        if (bodyCollider == null || Mathf.Abs(direction) <= DirectionEpsilon)
        {
            return true;
        }

        Bounds bounds = bodyCollider.bounds;
        Vector2 origin = new(
            bounds.center.x
                + Mathf.Sign(direction) * (bounds.extents.x + edgeLookAhead),
            bounds.min.y + 0.15f);
        return TryBoxCastForPlatform(
            origin,
            new Vector2(0.08f, 0.04f),
            Vector2.down,
            edgeProbeDepth,
            out _);
    }

    private bool TryFindPlatformAbove(out Collider2D platform)
    {
        platform = null;
        if (bodyCollider == null)
        {
            return false;
        }

        Bounds bounds = bodyCollider.bounds;
        Vector2 origin = new(bounds.center.x, bounds.min.y + 0.05f);
        return TryBoxCastForPlatform(
            origin,
            new Vector2(Mathf.Max(0.08f, bounds.size.x - 0.2f), 0.04f),
            Vector2.up,
            upperPlatformScanDistance,
            out platform,
            candidate => candidate.bounds.max.y
                    > bounds.min.y + PlatformSeparationEpsilon
                && IsXInsideSafeRange(bounds.center.x, candidate));
    }

    private bool TryFindPlatformBelow(out Collider2D platform)
    {
        platform = null;
        if (bodyCollider == null)
        {
            return false;
        }

        Bounds bounds = bodyCollider.bounds;
        Vector2 origin = new(bounds.center.x, bounds.min.y - 0.05f);
        return TryBoxCastForPlatform(
            origin,
            new Vector2(Mathf.Max(0.08f, bounds.size.x - 0.2f), 0.04f),
            Vector2.down,
            lowerPlatformScanDistance,
            out platform,
            candidate => candidate.bounds.max.y
                    < bounds.min.y - PlatformSeparationEpsilon
                && IsSafeDropTarget(bounds, candidate));
    }

    private bool IsSafeDropTarget(
        Bounds actorBounds,
        Collider2D platform)
    {
        if (!IsUsablePlatform(platform) || body == null)
        {
            return false;
        }

        Bounds platformBounds = platform.bounds;
        float inset = actorBounds.extents.x + landingMargin;
        float safeMinimumX = platformBounds.min.x + inset;
        float safeMaximumX = platformBounds.max.x - inset;
        if (safeMinimumX > safeMaximumX)
        {
            return false;
        }

        float actorX = actorBounds.center.x;
        if (actorX < safeMinimumX || actorX > safeMaximumX)
        {
            return false;
        }

        float dropDistance = actorBounds.min.y - platformBounds.max.y;
        float gravityAcceleration = Mathf.Abs(
            Physics2D.gravity.y * body.gravityScale);
        float timeToLanding = CalculateFallTime(
            dropDistance,
            body.linearVelocity.y,
            gravityAcceleration);
        float predictedLandingX = actorX
            + body.linearVelocity.x * timeToLanding;
        return predictedLandingX >= safeMinimumX
            && predictedLandingX <= safeMaximumX;
    }

    private bool TryCalculateAirRecoveryMovement(out float movement)
    {
        movement = 0f;
        if (controller == null
            || controller.IsGrounded
            || body == null
            || bodyCollider == null
            || (isClimbing && body.linearVelocity.y > 0f))
        {
            return false;
        }

        RemoveInvalidPlatformColliders();
        if (platformColliders.Count == 0)
        {
            RefreshPlatformColliders();
        }

        Bounds actorBounds = bodyCollider.bounds;
        float actorX = actorBounds.center.x;
        float actorFootY = actorBounds.min.y;
        float gravityAcceleration = Mathf.Abs(
            Physics2D.gravity.y * body.gravityScale);

        Collider2D bestPlatform = null;
        bool bestIsReachable = false;
        float bestSurfaceY = float.NegativeInfinity;
        float bestReachMiss = float.PositiveInfinity;
        float bestTimeToLanding = 0f;
        float bestSafeMinimumX = 0f;
        float bestSafeMaximumX = 0f;

        foreach (Collider2D platform in platformColliders)
        {
            if (!TryGetSafeHorizontalRange(
                    platform,
                    out float safeMinimumX,
                    out float safeMaximumX))
            {
                continue;
            }

            float surfaceY = platform.bounds.max.y;
            float dropDistance = actorFootY - surfaceY;
            if (dropDistance <= PlatformSeparationEpsilon
                || dropDistance > recoveryVerticalSearchDistance)
            {
                continue;
            }

            float timeToLanding = CalculateFallTime(
                dropDistance,
                body.linearVelocity.y,
                gravityAcceleration);
            float horizontalGap = DistanceToRange(
                actorX,
                safeMinimumX,
                safeMaximumX);
            float reachableDistance = (controller.MoveSpeed * timeToLanding)
                + recoveryReachSlack;
            bool isReachable = horizontalGap <= reachableDistance;
            float reachMiss = Mathf.Max(0f, horizontalGap - reachableDistance);

            bool isBetter = isReachable != bestIsReachable
                ? isReachable
                : isReachable
                    ? surfaceY > bestSurfaceY
                    : reachMiss < bestReachMiss
                        || (Mathf.Approximately(reachMiss, bestReachMiss)
                            && surfaceY > bestSurfaceY);
            if (!isBetter)
            {
                continue;
            }

            bestPlatform = platform;
            bestIsReachable = isReachable;
            bestSurfaceY = surfaceY;
            bestReachMiss = reachMiss;
            bestTimeToLanding = timeToLanding;
            bestSafeMinimumX = safeMinimumX;
            bestSafeMaximumX = safeMaximumX;
        }

        if (bestPlatform != null)
        {
            movement = CalculateRecoveryMovement(
                actorX,
                body.linearVelocity.x,
                bestTimeToLanding,
                bestSafeMinimumX,
                bestSafeMaximumX);
            return true;
        }

        Collider2D nearestPlatform = FindNearestPlatformByHorizontalRange(
            actorX,
            out float fallbackMinimumX,
            out float fallbackMaximumX);
        if (nearestPlatform == null)
        {
            return false;
        }

        movement = actorX < fallbackMinimumX
            ? 1f
            : actorX > fallbackMaximumX
                ? -1f
                : 0f;
        return true;
    }

    private bool TryBoxCastForPlatform(
        Vector2 origin,
        Vector2 size,
        Vector2 direction,
        float distance,
        out Collider2D platform,
        Func<Collider2D, bool> additionalFilter = null)
    {
        platform = null;
        ContactFilter2D filter = new();
        filter.SetLayerMask(platformLayers);
        filter.useTriggers = false;
        int hitCount = Physics2D.BoxCast(
            origin,
            size,
            0f,
            direction,
            filter,
            platformHitBuffer,
            distance);

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D candidate = platformHitBuffer[i].collider;
            if (!IsUsablePlatform(candidate)
                || (additionalFilter != null && !additionalFilter(candidate)))
            {
                continue;
            }

            platform = candidate;
            return true;
        }

        return false;
    }

    private bool IsXInsideSafeRange(float xPosition, Collider2D platform)
    {
        return TryGetSafeHorizontalRange(
                platform,
                out float safeMinimumX,
                out float safeMaximumX)
            && xPosition >= safeMinimumX
            && xPosition <= safeMaximumX;
    }

    private bool TryGetSafeHorizontalRange(
        Collider2D platform,
        out float safeMinimumX,
        out float safeMaximumX)
    {
        safeMinimumX = 0f;
        safeMaximumX = 0f;
        if (!IsUsablePlatform(platform) || bodyCollider == null)
        {
            return false;
        }

        Bounds platformBounds = platform.bounds;
        float inset = bodyCollider.bounds.extents.x + landingMargin;
        safeMinimumX = platformBounds.min.x + inset;
        safeMaximumX = platformBounds.max.x - inset;
        if (safeMinimumX <= safeMaximumX)
        {
            return true;
        }

        safeMinimumX = platformBounds.center.x;
        safeMaximumX = platformBounds.center.x;
        return true;
    }

    private Collider2D FindNearestPlatformByHorizontalRange(
        float xPosition,
        out float safeMinimumX,
        out float safeMaximumX)
    {
        Collider2D nearest = null;
        safeMinimumX = 0f;
        safeMaximumX = 0f;
        float nearestDistance = float.PositiveInfinity;

        foreach (Collider2D platform in platformColliders)
        {
            if (!TryGetSafeHorizontalRange(
                    platform,
                    out float candidateMinimumX,
                    out float candidateMaximumX))
            {
                continue;
            }

            float distance = DistanceToRange(
                xPosition,
                candidateMinimumX,
                candidateMaximumX);
            if (distance >= nearestDistance)
            {
                continue;
            }

            nearest = platform;
            nearestDistance = distance;
            safeMinimumX = candidateMinimumX;
            safeMaximumX = candidateMaximumX;
        }

        return nearest;
    }

    private bool IsUsablePlatform(Collider2D candidate)
    {
        return candidate != null
            && candidate.isActiveAndEnabled
            && !candidate.isTrigger
            && candidate.gameObject.scene == gameObject.scene
            && (platformLayers.value & (1 << candidate.gameObject.layer)) != 0;
    }

    private void SubmitCommand(
        float horizontalMovement,
        bool jumpPressed,
        bool dropPressed,
        bool attackPressed,
        float aimHorizontal)
    {
        if (commandReceiver == null)
        {
            return;
        }

        PlayerCommand command = new(
            Mathf.Clamp(horizontalMovement, -1f, 1f),
            jumpPressed,
            dropPressed,
            attackPressed,
            aimHorizontal);
        commandReceiver.SubmitCommand(in command);
        lastSubmittedCommand = command;
        lastHorizontalMovement = command.HorizontalMovement;
        hasSubmittedCommand = true;
    }

    private void ResetCommands(bool force = false)
    {
        if (commandReceiver != null && (force || hasSubmittedCommand))
        {
            commandReceiver.ResetCommands();
        }

        lastSubmittedCommand = default;
        lastHorizontalMovement = 0f;
        hasSubmittedCommand = false;
    }

    private void ResetDecisionState()
    {
        currentTarget = null;
        nextDecisionTime = 0f;
        nextRoamDirectionChangeTime = 0f;
        nextVerticalActionTime = 0f;
        nextExplorationActionTime = 0f;
        roamDirection = 0f;
        isClimbing = false;
        additionalJumpIssued = false;
        hasScheduledExplorationAction = false;
    }

    private void ResolveRuntimeReferences()
    {
        ResolveLocalReferences();
        ResolveMatchController();
        SubscribeToMatch();
    }

    private void ResolveLocalReferences()
    {
        if (commandReceiver == null)
        {
            commandReceiver = GetComponent<PlayerCommandReceiver>();
        }

        if (selfLife == null)
        {
            selfLife = GetComponent<PlayerLife>();
        }

        if (controller == null)
        {
            controller = commandReceiver != null
                ? commandReceiver.Controller
                : GetComponent<PlayerController2D>();
        }

        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }

        if (bodyCollider == null)
        {
            bodyCollider = GetComponent<BoxCollider2D>();
        }
    }

    private void ResolveMatchController()
    {
        if (matchController != null
            && matchController.gameObject.scene == gameObject.scene)
        {
            return;
        }

        matchController = null;
        MatchController[] controllers = FindObjectsByType<MatchController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        foreach (MatchController candidate in controllers)
        {
            if (candidate.gameObject.scene == gameObject.scene)
            {
                matchController = candidate;
                return;
            }
        }
    }

    private void RefreshPlatformColliders()
    {
        platformColliders.Clear();
        Collider2D[] colliders = FindObjectsByType<Collider2D>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        foreach (Collider2D candidate in colliders)
        {
            if (IsUsablePlatform(candidate))
            {
                platformColliders.Add(candidate);
            }
        }
    }

    private void RemoveInvalidPlatformColliders()
    {
        platformColliders.RemoveAll(candidate => !IsUsablePlatform(candidate));
    }

    private void InitializeRandom()
    {
        if (random != null)
        {
            return;
        }

        int seed = randomSeed != 0 ? randomSeed : gameObject.GetInstanceID();
        random = new System.Random(seed);
    }

    private float NextRandomValue()
    {
        InitializeRandom();
        return (float)random.NextDouble();
    }

    private void SubscribeToMatch()
    {
        if (subscribedController == matchController)
        {
            return;
        }

        UnsubscribeFromMatch();
        if (matchController == null)
        {
            return;
        }

        matchController.MatchStarted += HandleMatchStarted;
        matchController.MatchEnded += HandleMatchEnded;
        matchController.ParticipantsChanged += HandleParticipantsChanged;
        subscribedController = matchController;
    }

    private void UnsubscribeFromMatch()
    {
        if (subscribedController == null)
        {
            subscribedController = null;
            return;
        }

        subscribedController.MatchStarted -= HandleMatchStarted;
        subscribedController.MatchEnded -= HandleMatchEnded;
        subscribedController.ParticipantsChanged -= HandleParticipantsChanged;
        subscribedController = null;
    }

    private void HandleMatchStarted()
    {
        RefreshPlatformColliders();
        ResetDecisionState();
        ResetCommands();
    }

    private void HandleMatchEnded(PlayerLife winner)
    {
        ResetDecisionState();
        ResetCommands();
    }

    private void HandleParticipantsChanged()
    {
        nextDecisionTime = 0f;
        if (!CanMakeDecision() || !IsValidTarget(currentTarget))
        {
            currentTarget = null;
            ResetCommands();
        }
    }

    private static float GetColliderCenterY(Collider2D collider, Transform fallback)
    {
        return collider != null ? collider.bounds.center.y : fallback.position.y;
    }

    private static float DistanceToRange(float value, float minimum, float maximum)
    {
        if (value < minimum)
        {
            return minimum - value;
        }

        return value > maximum ? value - maximum : 0f;
    }

    private static float CalculateFallTime(
        float dropDistance,
        float verticalVelocity,
        float gravityAcceleration)
    {
        if (dropDistance <= 0f)
        {
            return 0f;
        }

        if (gravityAcceleration <= Mathf.Epsilon)
        {
            return verticalVelocity < -DirectionEpsilon
                ? dropDistance / -verticalVelocity
                : 0.5f;
        }

        float discriminant = (verticalVelocity * verticalVelocity)
            + (2f * gravityAcceleration * dropDistance);
        return Mathf.Max(
            0f,
            (verticalVelocity + Mathf.Sqrt(discriminant)) / gravityAcceleration);
    }

    private void OnValidate()
    {
        ResolveLocalReferences();
        decisionInterval = Mathf.Max(MinimumDecisionInterval, decisionInterval);
        roamDurationRange.x = Mathf.Max(0f, roamDurationRange.x);
        roamDurationRange.y = Mathf.Max(roamDurationRange.x, roamDurationRange.y);
        edgeLookAhead = Mathf.Max(0f, edgeLookAhead);
        edgeProbeDepth = Mathf.Max(0.01f, edgeProbeDepth);
        landingMargin = Mathf.Max(0f, landingMargin);
        explorationIntervalRange.x = Mathf.Max(
            MinimumDecisionInterval,
            explorationIntervalRange.x);
        explorationIntervalRange.y = Mathf.Max(
            explorationIntervalRange.x,
            explorationIntervalRange.y);
        explorationDropChance = Mathf.Clamp01(explorationDropChance);
        heightActionThreshold = Mathf.Max(0f, heightActionThreshold);
        shootHeightTolerance = Mathf.Max(0f, shootHeightTolerance);
        upperPlatformScanDistance = Mathf.Max(0.01f, upperPlatformScanDistance);
        lowerPlatformScanDistance = Mathf.Max(0.01f, lowerPlatformScanDistance);
        verticalActionCooldown = Mathf.Max(0f, verticalActionCooldown);
        recoveryVerticalSearchDistance = Mathf.Max(
            0.01f,
            recoveryVerticalSearchDistance);
        recoveryReachSlack = Mathf.Max(0f, recoveryReachSlack);
    }
}
