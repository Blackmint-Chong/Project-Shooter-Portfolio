using UnityEngine;

[DisallowMultipleComponent]
public sealed class Weapon : MonoBehaviour
{
    [SerializeField] private WeaponDefinition definition;
    [SerializeField] private SpriteRenderer weaponRenderer;
    [SerializeField] private Transform muzzle;

    public WeaponDefinition Definition => definition;
    public SpriteRenderer Renderer => weaponRenderer;
    public Transform Muzzle => muzzle;
    public Vector2 MuzzlePosition => muzzle != null
        ? (Vector2)muzzle.position
        : (Vector2)transform.position;
    public BulletStats BulletStats => definition != null
        ? definition.BulletStats
        : default;
    public float FireInterval => definition != null
        ? definition.FireInterval
        : 0f;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
    }

    public bool Equip(WeaponDefinition newDefinition)
    {
        if (newDefinition == null)
        {
            return false;
        }

        definition = newDefinition;
        ApplyDefinition();
        return true;
    }

    [ContextMenu("Apply Weapon Definition")]
    public void ApplyDefinition()
    {
        ResolveReferences();

        if (definition == null)
        {
            return;
        }

        if (weaponRenderer != null)
        {
            weaponRenderer.sprite = definition.Sprite;
            weaponRenderer.transform.localScale = new Vector3(
                definition.VisualScale.x,
                definition.VisualScale.y,
                1f);
        }

        if (muzzle != null)
        {
            Vector2 muzzleOffset = definition.MuzzleOffset;
            muzzle.localPosition = new Vector3(muzzleOffset.x, muzzleOffset.y, 0f);
        }
    }

    private void ResolveReferences()
    {
        if (weaponRenderer == null)
        {
            weaponRenderer = GetComponentInChildren<SpriteRenderer>(true);
        }

        if (muzzle == null)
        {
            Transform muzzleTransform = transform.Find("Muzzle");
            if (muzzleTransform != null)
            {
                muzzle = muzzleTransform;
            }
        }
    }

    private void OnValidate()
    {
        ResolveReferences();
    }
}
