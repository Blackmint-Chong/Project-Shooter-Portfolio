using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerLife))]
public sealed class BattleParticipantSlot : MonoBehaviour
{
    [SerializeField] private BattlePlayerSlot slot;

    private PlayerLife participant;

    public BattlePlayerSlot Slot => slot;
    public PlayerLife Participant
    {
        get
        {
            if (participant == null)
            {
                participant = GetComponent<PlayerLife>();
            }

            return participant;
        }
    }

    public void Assign(BattlePlayerSlot playerSlot)
    {
        slot = playerSlot;
    }

    private void Awake()
    {
        participant = GetComponent<PlayerLife>();
    }

    private void OnValidate()
    {
        participant = GetComponent<PlayerLife>();
    }
}
