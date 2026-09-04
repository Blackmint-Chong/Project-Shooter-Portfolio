using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class BulletImpulseReceiver : MonoBehaviour, IBulletHitReceiver
{
    private Rigidbody2D body;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
    }

    public void ReceiveBulletHit(in BulletHitData hitData)
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }

        CombatResolver.TryApplyImpulse(body, in hitData);
    }
}
