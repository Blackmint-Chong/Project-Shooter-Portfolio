using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerController2D))]
public sealed class PlayerShooter : MonoBehaviour
{
    [SerializeField] private BulletPool bulletPool;
    [SerializeField] private PlayerController2D controller;
    [SerializeField] private Transform weaponFacingRoot;
    [SerializeField] private Weapon equippedWeapon;
    [SerializeField, Min(0f)] private float spawnOffset = 0.6f;

    private float nextFireTime;

    public Weapon EquippedWeapon => equippedWeapon;
    public bool CanFire => isActiveAndEnabled && Time.time >= nextFireTime;
    public float RemainingFireCooldown => Mathf.Max(0f, nextFireTime - Time.time);

    public void Bind(BulletPool pool)
    {
        bulletPool = pool;
    }

    private void Awake()
    {
        ResolveReferences();
        ApplyWeaponFacing();
    }

    private void OnEnable()
    {
        ResolveReferences();
        nextFireTime = Time.time;
        ApplyWeaponFacing();
    }

    private void LateUpdate()
    {
        ApplyWeaponFacing();
    }

    public Bullet Fire()
    {
        ResolveReferences();
        if (bulletPool == null || controller == null || !CanFire)
        {
            return null;
        }

        Vector2 direction = controller.FacingDirection;
        ApplyWeaponFacing();

        if (equippedWeapon != null && equippedWeapon.Definition != null)
        {
            BulletStats weaponStats = equippedWeapon.BulletStats;
            Bullet bullet = bulletPool.Spawn(
                equippedWeapon.MuzzlePosition,
                direction,
                gameObject,
                in weaponStats);
            StartFireCooldownIfSpawned(bullet, equippedWeapon.FireInterval);
            return bullet;
        }

        Vector2 spawnPosition = (Vector2)transform.position + direction * spawnOffset;
        return bulletPool.Spawn(spawnPosition, direction, gameObject);
    }

    public bool EquipWeapon(WeaponDefinition definition)
    {
        ResolveReferences();
        if (equippedWeapon == null || !equippedWeapon.Equip(definition))
        {
            return false;
        }

        nextFireTime = Time.time;
        return true;
    }

    private void StartFireCooldownIfSpawned(Bullet bullet, float fireInterval)
    {
        if (bullet != null)
        {
            nextFireTime = Time.time + Mathf.Max(0f, fireInterval);
        }
    }

    private void ResolveReferences()
    {
        if (controller == null)
        {
            controller = GetComponent<PlayerController2D>();
        }

        if (equippedWeapon == null)
        {
            equippedWeapon = GetComponentInChildren<Weapon>(true);
        }

        if (weaponFacingRoot == null && equippedWeapon != null)
        {
            weaponFacingRoot = equippedWeapon.transform.parent;
        }

        if (bulletPool == null)
        {
            bulletPool = FindFirstObjectByType<BulletPool>();
        }
    }

    private void ApplyWeaponFacing()
    {
        if (weaponFacingRoot == null || controller == null)
        {
            return;
        }

        Vector3 scale = weaponFacingRoot.localScale;
        float horizontalScale = Mathf.Abs(scale.x);
        if (horizontalScale <= Mathf.Epsilon)
        {
            horizontalScale = 1f;
        }

        scale.x = horizontalScale * Mathf.Sign(controller.FacingDirection.x);
        weaponFacingRoot.localScale = scale;
    }

    private void OnValidate()
    {
        spawnOffset = Mathf.Max(0f, spawnOffset);
        if (controller == null)
        {
            controller = GetComponent<PlayerController2D>();
        }

        if (equippedWeapon == null)
        {
            equippedWeapon = GetComponentInChildren<Weapon>(true);
        }

        if (weaponFacingRoot == null && equippedWeapon != null)
        {
            weaponFacingRoot = equippedWeapon.transform.parent;
        }
    }
}
