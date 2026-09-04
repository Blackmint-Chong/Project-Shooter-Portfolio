using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class PlayerRespawner : MonoBehaviour
{
    [SerializeField] private Vector2 respawnPosition = new(0f, 4f);
    [SerializeField, Min(0f)] private float respawnDelay = 0.5f;

    private Rigidbody2D body;
    private BattleSpawnArea respawnArea;
    private Vector2 initialSpawnPosition;
    private bool hasInitialSpawnPosition;

    public Vector2 RespawnPosition => respawnArea != null
        ? respawnArea.Center
        : respawnPosition;
    public float RespawnDelay => respawnDelay;
    public BattleSpawnArea RespawnArea => respawnArea;
    public Vector2 InitialSpawnPosition => initialSpawnPosition;
    public bool HasInitialSpawnPosition => hasInitialSpawnPosition;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();

        if (!hasInitialSpawnPosition)
        {
            BindInitialSpawnPosition(transform.position);
        }
    }

    public void Respawn()
    {
        Vector2 targetPosition = respawnArea != null
            ? respawnArea.GetRandomPosition()
            : respawnPosition;
        RespawnAt(targetPosition);
    }

    public void BindRespawnArea(BattleSpawnArea area)
    {
        respawnArea = area;
    }

    public void BindInitialSpawnPosition(Vector2 position)
    {
        initialSpawnPosition = position;
        hasInitialSpawnPosition = true;
    }

    public void RespawnAtInitialPosition()
    {
        if (!hasInitialSpawnPosition)
        {
            BindInitialSpawnPosition(transform.position);
        }

        RespawnAt(initialSpawnPosition);
    }

    public void RespawnAt(Vector2 targetPosition)
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }

        if (gameObject.activeSelf)
        {
            gameObject.SetActive(false);
        }

        transform.position = targetPosition;
        gameObject.SetActive(true);

        body.position = targetPosition;
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
    }
}
