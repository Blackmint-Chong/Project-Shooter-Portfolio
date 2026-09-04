using UnityEngine;

[DisallowMultipleComponent]
public sealed class BattleSpawnSlot : MonoBehaviour
{
    [SerializeField] private BattlePlayerSlot slot;
    [SerializeField] private BattleSpawnArea respawnArea;

    public BattlePlayerSlot Slot => slot;
    public Vector2 InitialPosition => transform.position;
    public BattleSpawnArea RespawnArea => respawnArea;

    public void Configure(
        BattlePlayerSlot playerSlot,
        BattleSpawnArea playerRespawnArea)
    {
        slot = playerSlot;
        respawnArea = playerRespawnArea;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = slot == BattlePlayerSlot.P1
            ? new Color(0.2f, 0.65f, 1f, 1f)
            : new Color(1f, 0.45f, 0.2f, 1f);
        Gizmos.DrawWireSphere(transform.position, 0.15f);
    }
}
