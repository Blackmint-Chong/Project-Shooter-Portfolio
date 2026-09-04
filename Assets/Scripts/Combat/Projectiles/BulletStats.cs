using System;
using UnityEngine;

[Serializable]
public struct BulletStats
{
    [SerializeField, Min(0f)] private float speed;
    [SerializeField, Min(0f)] private float impulse;

    public readonly float Speed => speed;
    public readonly float Impulse => impulse;

    public BulletStats(float speed, float impulse)
    {
        this.speed = Mathf.Max(0f, speed);
        this.impulse = Mathf.Max(0f, impulse);
    }
}
