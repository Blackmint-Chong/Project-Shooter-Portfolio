using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerRespawnShield : MonoBehaviour, IIncomingImpulseModifier
{
    [SerializeField, Min(0f)] private float duration = 3f;
    [SerializeField, Range(0f, 1f)] private float impulseReduction = 0.8f;
    [SerializeField] private SpriteRenderer barrierRenderer;

    private float expiresAt = float.NegativeInfinity;

    public float Duration => duration;
    public float ImpulseReduction => impulseReduction;
    public bool IsActive => IsActiveAt(Time.time);
    public float RemainingDuration => IsActive
        ? Mathf.Max(0f, expiresAt - Time.time)
        : 0f;
    public float IncomingImpulseMultiplier => IsActive
        ? 1f - impulseReduction
        : 1f;
    public SpriteRenderer BarrierRenderer => barrierRenderer;
    public bool IsBarrierVisible =>
        barrierRenderer != null && barrierRenderer.gameObject.activeSelf;

    private void Awake()
    {
        RefreshVisualAt(Time.time);
    }

    private void OnEnable()
    {
        RefreshVisualAt(Time.time);
    }

    private void Update()
    {
        RefreshVisualAt(Time.time);
    }

    public void Activate()
    {
        ActivateAt(Time.time);
    }

    public Vector2 ModifyIncomingImpulse(Vector2 incomingImpulse)
    {
        return incomingImpulse * IncomingImpulseMultiplier;
    }

    private void OnDisable()
    {
        Deactivate();
    }

    private void OnValidate()
    {
        duration = Mathf.Max(0f, duration);
        impulseReduction = Mathf.Clamp01(impulseReduction);
    }

    internal void Deactivate()
    {
        expiresAt = float.NegativeInfinity;
        SetBarrierVisible(false);
    }

    internal void ActivateAt(float currentTime)
    {
        expiresAt = currentTime + duration;
        RefreshVisualAt(currentTime);
    }

    internal bool IsActiveAt(float currentTime)
    {
        return isActiveAndEnabled && currentTime < expiresAt;
    }

    internal float GetIncomingImpulseMultiplierAt(float currentTime)
    {
        return IsActiveAt(currentTime) ? 1f - impulseReduction : 1f;
    }

    internal void RefreshVisualAt(float currentTime)
    {
        SetBarrierVisible(IsActiveAt(currentTime));
    }

    private void SetBarrierVisible(bool visible)
    {
        if (barrierRenderer == null
            || barrierRenderer.gameObject.activeSelf == visible)
        {
            return;
        }

        barrierRenderer.gameObject.SetActive(visible);
    }
}
