using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class ProjectileDeadZone : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        Bullet bullet = other.GetComponentInParent<Bullet>();
        bullet?.RequestRecycle();
    }

    private void Reset()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
    }
}
