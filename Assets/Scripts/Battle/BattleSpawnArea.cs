using UnityEngine;

[DisallowMultipleComponent]
public sealed class BattleSpawnArea : MonoBehaviour
{
    [SerializeField, Min(0f)] private float horizontalExtent;

    public Vector2 Center => transform.position;
    public float HorizontalExtent => horizontalExtent;

    public Vector2 Evaluate(float normalizedHorizontalPosition)
    {
        float offset = Mathf.Lerp(
            -horizontalExtent,
            horizontalExtent,
            Mathf.Clamp01(normalizedHorizontalPosition));
        Vector2 center = Center;
        return new Vector2(center.x + offset, center.y);
    }

    public Vector2 GetRandomPosition()
    {
        return Evaluate(Random.value);
    }

    public bool Contains(Vector2 position, float tolerance = 0.001f)
    {
        float safeTolerance = Mathf.Max(0f, tolerance);
        Vector2 center = Center;
        return Mathf.Abs(position.y - center.y) <= safeTolerance
            && position.x >= center.x - horizontalExtent - safeTolerance
            && position.x <= center.x + horizontalExtent + safeTolerance;
    }

    private void OnValidate()
    {
        horizontalExtent = Mathf.Max(0f, horizontalExtent);
    }

    private void OnDrawGizmosSelected()
    {
        Vector2 center = Center;
        Gizmos.color = new Color(0.2f, 0.85f, 1f, 1f);
        Gizmos.DrawLine(
            new Vector3(center.x - horizontalExtent, center.y, 0f),
            new Vector3(center.x + horizontalExtent, center.y, 0f));
        Gizmos.DrawWireSphere(center, 0.08f);
    }
}
