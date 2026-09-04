using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class PlayerFallDeathZone : MonoBehaviour
{
    [SerializeField] private MatchController matchController;

    private BoxCollider2D triggerCollider;
    private bool missingControllerReported;

    public MatchController MatchController => matchController;

    private void Awake()
    {
        triggerCollider = GetComponent<BoxCollider2D>();
        triggerCollider.isTrigger = true;
        ResolveMatchController();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerLife playerLife = other.GetComponentInParent<PlayerLife>();
        if (playerLife == null)
        {
            return;
        }

        ResolveMatchController();
        if (matchController == null)
        {
            if (!missingControllerReported)
            {
                Debug.LogError(
                    "PlayerFallDeathZone requires a MatchController in the scene.",
                    this);
                missingControllerReported = true;
            }

            return;
        }

        matchController.ReportPlayerFall(playerLife);
    }

    private void Reset()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    public void Bind(MatchController controller)
    {
        matchController = controller;
        missingControllerReported = false;
    }

    private void OnValidate()
    {
        if (triggerCollider == null)
        {
            triggerCollider = GetComponent<BoxCollider2D>();
        }

        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }

    private void ResolveMatchController()
    {
        if (matchController == null)
        {
            matchController = FindFirstObjectByType<MatchController>();
        }
    }
}
