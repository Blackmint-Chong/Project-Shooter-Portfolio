using UnityEngine;

public static class CombatResolver
{
    public static void ResolveBulletHit(
        Collider2D targetCollider,
        Vector2 projectilePosition,
        Vector2 direction,
        in BulletStats stats,
        GameObject owner)
    {
        if (targetCollider == null)
        {
            return;
        }

        Vector2 hitPoint = targetCollider.ClosestPoint(projectilePosition);
        BulletHitData hitData = new(
            direction * stats.Impulse,
            hitPoint,
            owner);

        IBulletHitReceiver receiver =
            targetCollider.GetComponentInParent<IBulletHitReceiver>();
        receiver?.ReceiveBulletHit(in hitData);
    }

    public static bool TryApplyImpulse(
        Rigidbody2D targetBody,
        in BulletHitData hitData)
    {
        if (targetBody == null || targetBody.mass <= Mathf.Epsilon)
        {
            return false;
        }

        Vector2 incomingImpulse = hitData.ImpulseVector;
        IIncomingImpulseModifier impulseModifier =
            targetBody.GetComponent<IIncomingImpulseModifier>();
        if (impulseModifier != null)
        {
            incomingImpulse = impulseModifier.ModifyIncomingImpulse(incomingImpulse);
        }

        Vector2 velocity = targetBody.linearVelocity;
        velocity.x = incomingImpulse.x / targetBody.mass;

        targetBody.WakeUp();
        targetBody.linearVelocity = velocity;
        return true;
    }
}
