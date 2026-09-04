using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerLife))]
public sealed class BattleParticipantPresentation : MonoBehaviour
{
    [SerializeField] private Sprite portrait;

    public Sprite Portrait => portrait;
}
