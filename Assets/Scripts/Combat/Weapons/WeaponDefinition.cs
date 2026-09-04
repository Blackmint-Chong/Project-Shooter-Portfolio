using UnityEngine;

[CreateAssetMenu(
    fileName = "WeaponDefinition",
    menuName = "Project Shooter/Weapon/Weapon Definition")]
public sealed class WeaponDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string displayName = "Weapon";

    [Header("Projectile")]
    [SerializeField] private BulletStats bulletStats = new(15f, 5f);

    [Header("Firing")]
    [SerializeField, Min(0f)] private float fireInterval = 0.5f;

    [Header("Visual")]
    [SerializeField] private Sprite sprite;
    [SerializeField] private Vector2 visualScale = new(0.35f, 0.12f);
    [SerializeField] private Vector2 muzzleOffset = new(0.3f, 0f);

    public string DisplayName => displayName;
    public float BulletSpeed => bulletStats.Speed;
    public float Impulse => bulletStats.Impulse;
    public float FireInterval => fireInterval;
    public BulletStats BulletStats => bulletStats;
    public Sprite Sprite => sprite;
    public Vector2 VisualScale => visualScale;
    public Vector2 MuzzleOffset => muzzleOffset;

    internal void Configure(
        string newDisplayName,
        Sprite newSprite,
        in BulletStats newBulletStats,
        float newFireInterval,
        Vector2 newVisualScale,
        Vector2 newMuzzleOffset)
    {
        displayName = newDisplayName;
        sprite = newSprite;
        bulletStats = new BulletStats(
            newBulletStats.Speed,
            newBulletStats.Impulse);
        fireInterval = Mathf.Max(0f, newFireInterval);
        visualScale = newVisualScale;
        muzzleOffset = newMuzzleOffset;
        OnValidate();
    }

    private void OnValidate()
    {
        fireInterval = Mathf.Max(0f, fireInterval);
        visualScale.x = Mathf.Max(0f, visualScale.x);
        visualScale.y = Mathf.Max(0f, visualScale.y);
    }
}
