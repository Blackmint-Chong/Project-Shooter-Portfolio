using UnityEngine;

public readonly struct BulletHitData
{
    public Vector2 ImpulseVector { get; }
    public Vector2 HitPoint { get; }
    public GameObject Owner { get; }

    public BulletHitData(
        Vector2 impulseVector,
        Vector2 hitPoint,
        GameObject owner)
    {
        ImpulseVector = impulseVector;
        HitPoint = hitPoint;
        Owner = owner;
    }
}
