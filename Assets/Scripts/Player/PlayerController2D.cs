using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class PlayerController2D : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField, Min(0f)] private float moveSpeed = 6f;
    [SerializeField, Min(0f)] private float horizontalAcceleration = 120f;
    [SerializeField, Min(0f)] private float jumpSpeed = 10f;
    [SerializeField, Min(0)] private int additionalJumpCount = 1;

    [Header("Visual")]
    [SerializeField] private SpriteRenderer visualRenderer;
    [SerializeField] private Sprite groundedSprite;
    [SerializeField] private Sprite airborneSprite;
    [SerializeField] private bool spriteFacesRight = true;

    [Header("One-way Platform")]
    [SerializeField] private LayerMask groundLayers;
    [SerializeField, Range(0f, 1f)] private float minimumGroundNormalY = 0.5f;
    [SerializeField, Min(0f)] private float dropSpeed = 0f;
    [SerializeField, Min(0f)] private float dropClearance = 0.02f;

    private readonly HashSet<Collider2D> supportingColliders = new();
    private readonly List<Collider2D> ignoredPlatforms = new();

    private Rigidbody2D body;
    private BoxCollider2D playerCollider;
    private Collider2D standingPlatform;
    private float horizontalInput;
    private float facingSign = 1f;
    private int remainingAdditionalJumps;
    private bool jumpRequested;
    private bool dropRequested;

    public Vector2 FacingDirection => new(facingSign, 0f);
    public bool IsGrounded => supportingColliders.Count > 0 && ignoredPlatforms.Count == 0;
    public bool IsDroppingThroughPlatform => ignoredPlatforms.Count > 0;
    public float MoveSpeed => moveSpeed;
    public float JumpSpeed => jumpSpeed;
    public int AdditionalJumpCount => additionalJumpCount;
    public int RemainingAdditionalJumps => remainingAdditionalJumps;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        playerCollider = GetComponent<BoxCollider2D>();
        ResolveVisualRenderer();
        ApplyFacingToVisual();
        ApplyMovementSprite();
        RefillAdditionalJumps();
    }

    private void OnEnable()
    {
        ResolveVisualRenderer();
        ApplyFacingToVisual();
        ApplyMovementSprite();
    }

    private void FixedUpdate()
    {
        RemoveInvalidSupportingColliders();
        UpdateIgnoredPlatform();

        bool droppedThisStep = TryBeginDrop();
        Vector2 velocity = body.linearVelocity;

        if (!droppedThisStep && jumpRequested)
        {
            bool isGroundJump = IsGrounded;
            if (isGroundJump || remainingAdditionalJumps > 0)
            {
                velocity.y = jumpSpeed;
                if (!isGroundJump)
                {
                    remainingAdditionalJumps--;
                }

                supportingColliders.Clear();
                standingPlatform = null;
            }
        }

        float targetHorizontalSpeed = horizontalInput * moveSpeed;
        velocity.x = Mathf.MoveTowards(
            velocity.x,
            targetHorizontalSpeed,
            horizontalAcceleration * Time.fixedDeltaTime);

        body.linearVelocity = velocity;
        ApplyMovementSprite();
        jumpRequested = false;
        dropRequested = false;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        EvaluateGroundContact(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        EvaluateGroundContact(collision);
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        Collider2D other = GetOtherCollider(collision);
        supportingColliders.Remove(other);

        if (standingPlatform == other)
        {
            standingPlatform = FindAnySupportingCollider();
        }

        ApplyMovementSprite();
    }

    private void OnDisable()
    {
        RestoreIgnoredPlatformCollision();
        supportingColliders.Clear();
        standingPlatform = null;
        RefillAdditionalJumps();
        ResetCommandState();
    }

    private void OnDestroy()
    {
        RestoreIgnoredPlatformCollision();
    }

    public void ApplyCommand(in PlayerCommand command)
    {
        SetHorizontalInput(command.HorizontalMovement);
        ApplyAimHorizontal(command.AimHorizontal);
        if (command.JumpPressed)
        {
            RequestJump();
        }

        if (command.DropPressed)
        {
            RequestDrop();
        }
    }

    internal void ResetCommandState()
    {
        horizontalInput = 0f;
        jumpRequested = false;
        dropRequested = false;
    }

    internal void SetHorizontalInput(float input)
    {
        horizontalInput = Mathf.Clamp(input, -1f, 1f);
        SetFacingDirection(horizontalInput);
    }

    public void SetFacingDirection(float horizontalDirection)
    {
        if (Mathf.Abs(horizontalDirection) <= 0.01f)
        {
            return;
        }

        facingSign = Mathf.Sign(horizontalDirection);
        ApplyFacingToVisual();
    }

    private void ApplyAimHorizontal(float aimHorizontal)
    {
        SetFacingDirection(aimHorizontal);
    }

    internal void RequestJump()
    {
        jumpRequested = true;
    }

    internal void RequestDrop()
    {
        dropRequested = true;
    }

    internal void SetMovementSprites(Sprite grounded, Sprite airborne)
    {
        groundedSprite = grounded;
        airborneSprite = airborne;
        ApplyMovementSprite();
    }

    private bool TryBeginDrop()
    {
        if (!dropRequested || !IsGrounded || standingPlatform == null || ignoredPlatforms.Count > 0)
        {
            return false;
        }

        foreach (Collider2D platform in supportingColliders)
        {
            if (platform == null || !platform.isActiveAndEnabled)
            {
                continue;
            }

            ignoredPlatforms.Add(platform);
            Physics2D.IgnoreCollision(playerCollider, platform, true);
        }

        if (ignoredPlatforms.Count == 0)
        {
            return false;
        }

        supportingColliders.Clear();
        standingPlatform = null;
        jumpRequested = false;

        Vector2 velocity = body.linearVelocity;
        velocity.y = Mathf.Min(velocity.y, -dropSpeed);
        body.WakeUp();
        body.linearVelocity = velocity;
        return true;
    }

    private void UpdateIgnoredPlatform()
    {
        for (int i = ignoredPlatforms.Count - 1; i >= 0; i--)
        {
            Collider2D platform = ignoredPlatforms[i];
            if (platform == null)
            {
                ignoredPlatforms.RemoveAt(i);
                continue;
            }

            if (!platform.isActiveAndEnabled)
            {
                RestoreIgnoredPlatformCollisionAt(i);
                continue;
            }

            Bounds playerBounds = playerCollider.bounds;
            Bounds platformBounds = platform.bounds;
            bool hasPassedBelow = playerBounds.max.y
                < platformBounds.min.y - dropClearance;
            bool hasPassedSideways = playerBounds.max.x
                    < platformBounds.min.x - dropClearance
                || playerBounds.min.x
                    > platformBounds.max.x + dropClearance;

            if (hasPassedBelow || hasPassedSideways)
            {
                RestoreIgnoredPlatformCollisionAt(i);
            }
        }
    }

    private void RestoreIgnoredPlatformCollision()
    {
        for (int i = ignoredPlatforms.Count - 1; i >= 0; i--)
        {
            RestoreIgnoredPlatformCollisionAt(i);
        }
    }

    private void RestoreIgnoredPlatformCollisionAt(int index)
    {
        Collider2D platform = ignoredPlatforms[index];
        ignoredPlatforms.RemoveAt(index);

        if (playerCollider != null && platform != null)
        {
            Physics2D.IgnoreCollision(playerCollider, platform, false);
        }
    }

    private void EvaluateGroundContact(Collision2D collision)
    {
        Collider2D other = GetOtherCollider(collision);
        if (other == null
            || ignoredPlatforms.Contains(other)
            || !IsInGroundLayers(other.gameObject.layer))
        {
            return;
        }

        if (body.linearVelocity.y > 0.01f)
        {
            supportingColliders.Remove(other);
            if (standingPlatform == other)
            {
                standingPlatform = FindAnySupportingCollider();
            }

            ApplyMovementSprite();
            return;
        }

        bool supportsPlayer = false;
        for (int i = 0; i < collision.contactCount; i++)
        {
            if (collision.GetContact(i).normal.y >= minimumGroundNormalY)
            {
                supportsPlayer = true;
                break;
            }
        }

        if (supportsPlayer)
        {
            supportingColliders.Add(other);
            standingPlatform = other;
            RefillAdditionalJumps();
        }
        else
        {
            supportingColliders.Remove(other);
            if (standingPlatform == other)
            {
                standingPlatform = FindAnySupportingCollider();
            }
        }

        ApplyMovementSprite();
    }

    private Collider2D GetOtherCollider(Collision2D collision)
    {
        return collision.collider == playerCollider
            ? collision.otherCollider
            : collision.collider;
    }

    private bool IsInGroundLayers(int layer)
    {
        return (groundLayers.value & (1 << layer)) != 0;
    }

    private void RemoveInvalidSupportingColliders()
    {
        supportingColliders.RemoveWhere(
            collider => collider == null || !collider.isActiveAndEnabled);
        if (standingPlatform == null || !supportingColliders.Contains(standingPlatform))
        {
            standingPlatform = FindAnySupportingCollider();
        }
    }

    private Collider2D FindAnySupportingCollider()
    {
        foreach (Collider2D collider in supportingColliders)
        {
            if (collider != null)
            {
                return collider;
            }
        }

        return null;
    }

    private void RefillAdditionalJumps()
    {
        remainingAdditionalJumps = additionalJumpCount;
    }

    private void ResolveVisualRenderer()
    {
        if (visualRenderer == null)
        {
            visualRenderer = GetComponentInChildren<SpriteRenderer>(true);
        }
    }

    private void ApplyFacingToVisual()
    {
        if (visualRenderer == null)
        {
            return;
        }

        bool isFacingRight = facingSign > 0f;
        visualRenderer.flipX = spriteFacesRight != isFacingRight;
    }

    private void ApplyMovementSprite()
    {
        if (visualRenderer == null)
        {
            return;
        }

        Sprite targetSprite = IsGrounded && groundedSprite != null
            ? groundedSprite
            : airborneSprite;

        if (targetSprite == null)
        {
            targetSprite = groundedSprite;
        }

        if (targetSprite != null && visualRenderer.sprite != targetSprite)
        {
            visualRenderer.sprite = targetSprite;
        }
    }

    private void OnValidate()
    {
        ResolveVisualRenderer();
        ApplyFacingToVisual();
        moveSpeed = Mathf.Max(0f, moveSpeed);
        horizontalAcceleration = Mathf.Max(0f, horizontalAcceleration);
        jumpSpeed = Mathf.Max(0f, jumpSpeed);
        additionalJumpCount = Mathf.Max(0, additionalJumpCount);
        remainingAdditionalJumps = Mathf.Clamp(
            remainingAdditionalJumps,
            0,
            additionalJumpCount);
        dropSpeed = Mathf.Max(0f, dropSpeed);
        dropClearance = Mathf.Max(0f, dropClearance);
    }
}
