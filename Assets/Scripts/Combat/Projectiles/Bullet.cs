using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public sealed class Bullet : MonoBehaviour
{
    [Header("Fallback Stats")]
    [SerializeField, Min(0f)] private float speed = 15f;
    [SerializeField, Min(0f)] private float impulse = 5f;

    [Header("Lifetime")]
    [SerializeField, Min(0.01f)] private float maxLifetime = 5f;

    private Rigidbody2D body;
    private BulletPool owningPool;
    private BulletStats activeStats;
    private Vector2 direction;
    private GameObject owner;
    private float elapsedLifetime;
    private bool isSpawned;
    private bool hitResolved;
    private bool releaseRequested;

    public BulletStats DefaultStats => new(speed, impulse);
    public BulletStats ActiveStats => activeStats;
    public Vector2 Direction => direction;
    public GameObject Owner => owner;
    public bool IsSpawned => isSpawned;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
    }

    private void FixedUpdate()
    {
        if (!isSpawned || releaseRequested)
        {
            return;
        }

        elapsedLifetime += Time.fixedDeltaTime;
        if (elapsedLifetime >= maxLifetime)
        {
            RequestRecycle();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isSpawned || releaseRequested)
        {
            return;
        }

        if (IsDeadZone(other))
        {
            RequestRecycle();
            return;
        }

        if (IsOwnedCollider(other))
        {
            return;
        }

        int playerLayer = LayerMask.NameToLayer("Player");
        if (other.gameObject.layer != playerLayer || hitResolved)
        {
            return;
        }

        hitResolved = true;

        try
        {
            CombatResolver.ResolveBulletHit(
                other,
                body.position,
                direction,
                in activeStats,
                owner);
        }
        finally
        {
            RequestRecycle();
        }
    }

    public void RequestRecycle()
    {
        if (!isSpawned || releaseRequested)
        {
            return;
        }

        releaseRequested = true;

        if (owningPool != null)
        {
            owningPool.Release(this);
            return;
        }

        ResetForPool();
        gameObject.SetActive(false);
    }

    internal void BindToPool(BulletPool pool)
    {
        owningPool = pool;
    }

    internal void Spawn(
        Vector2 position,
        Vector2 normalizedDirection,
        GameObject spawnOwner,
        in BulletStats stats)
    {
        float rotationDegrees = Mathf.Atan2(
            normalizedDirection.y,
            normalizedDirection.x) * Mathf.Rad2Deg;

        transform.SetPositionAndRotation(
            position,
            Quaternion.Euler(0f, 0f, rotationDegrees));
        body.position = position;
        body.rotation = rotationDegrees;
        body.angularVelocity = 0f;

        direction = normalizedDirection;
        owner = spawnOwner;
        activeStats = new BulletStats(stats.Speed, stats.Impulse);
        elapsedLifetime = 0f;
        hitResolved = false;
        releaseRequested = false;
        isSpawned = true;

        gameObject.SetActive(true);
        body.linearVelocity = direction * activeStats.Speed;
        body.simulated = true;
    }

    internal void ResetForPool()
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }

        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
        body.rotation = 0f;
        body.simulated = false;
        transform.rotation = Quaternion.identity;

        activeStats = default;
        direction = Vector2.zero;
        owner = null;
        elapsedLifetime = 0f;
        isSpawned = false;
        hitResolved = false;
        releaseRequested = false;
    }

    private bool IsOwnedCollider(Collider2D other)
    {
        if (owner == null)
        {
            return false;
        }

        Transform ownerTransform = owner.transform;
        return other.transform == ownerTransform || other.transform.IsChildOf(ownerTransform);
    }

    private static bool IsDeadZone(Collider2D other)
    {
        int deadZoneLayer = LayerMask.NameToLayer("ProjectileDeadZone");
        return other.gameObject.layer == deadZoneLayer
            || other.GetComponentInParent<ProjectileDeadZone>() != null;
    }
}
