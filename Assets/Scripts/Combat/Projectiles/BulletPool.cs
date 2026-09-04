using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

[DisallowMultipleComponent]
public sealed class BulletPool : MonoBehaviour
{
    [SerializeField] private Bullet bulletPrefab;
    [SerializeField, Min(0)] private int initialSize = 16;
    [SerializeField, Min(1)] private int maxInactiveSize = 64;

    private ObjectPool<Bullet> pool;

    public int CountActive => pool?.CountActive ?? 0;
    public int CountInactive => pool?.CountInactive ?? 0;
    public int CountAll => pool?.CountAll ?? 0;

    private void Awake()
    {
        if (bulletPrefab == null)
        {
            Debug.LogError($"{nameof(BulletPool)} on {name} needs a Bullet prefab.", this);
            enabled = false;
            return;
        }

        int safeMaxSize = Mathf.Max(1, maxInactiveSize);
        int safeInitialSize = Mathf.Clamp(initialSize, 0, safeMaxSize);

        pool = new ObjectPool<Bullet>(
            CreateBullet,
            OnTakeFromPool,
            OnReturnedToPool,
            OnDestroyPooledBullet,
            collectionCheck: true,
            defaultCapacity: safeInitialSize,
            maxSize: safeMaxSize);

        Prewarm(safeInitialSize);
    }

    public Bullet Spawn(Vector2 position, Vector2 direction, GameObject owner)
    {
        if (bulletPrefab == null)
        {
            return null;
        }

        BulletStats stats = bulletPrefab.DefaultStats;
        return Spawn(position, direction, owner, in stats);
    }

    public Bullet Spawn(
        Vector2 position,
        Vector2 direction,
        GameObject owner,
        in BulletStats stats)
    {
        if (pool == null)
        {
            Debug.LogError($"{nameof(BulletPool)} on {name} is not initialized.", this);
            return null;
        }

        if (direction.sqrMagnitude <= Mathf.Epsilon)
        {
            Debug.LogWarning("A bullet cannot be spawned with a zero direction.", this);
            return null;
        }

        Bullet bullet = pool.Get();
        Vector2 normalizedDirection = direction.normalized;
        bullet.Spawn(position, normalizedDirection, owner, in stats);
        return bullet;
    }

    internal void Release(Bullet bullet)
    {
        if (pool == null || bullet == null)
        {
            return;
        }

        pool.Release(bullet);
    }

    private Bullet CreateBullet()
    {
        Bullet bullet = Instantiate(bulletPrefab, transform);
        bullet.name = bulletPrefab.name;
        bullet.BindToPool(this);
        bullet.ResetForPool();
        bullet.gameObject.SetActive(false);
        return bullet;
    }

    private static void OnTakeFromPool(Bullet bullet)
    {
        // Spawn configures all runtime state before activating the GameObject.
    }

    private static void OnReturnedToPool(Bullet bullet)
    {
        bullet.ResetForPool();
        bullet.gameObject.SetActive(false);
    }

    private static void OnDestroyPooledBullet(Bullet bullet)
    {
        if (bullet != null)
        {
            Destroy(bullet.gameObject);
        }
    }

    private void Prewarm(int count)
    {
        if (count <= 0)
        {
            return;
        }

        List<Bullet> bullets = new(count);
        for (int i = 0; i < count; i++)
        {
            bullets.Add(pool.Get());
        }

        for (int i = 0; i < bullets.Count; i++)
        {
            pool.Release(bullets[i]);
        }
    }

    private void OnValidate()
    {
        initialSize = Mathf.Max(0, initialSize);
        maxInactiveSize = Mathf.Max(1, maxInactiveSize);
        initialSize = Mathf.Min(initialSize, maxInactiveSize);
    }
}
