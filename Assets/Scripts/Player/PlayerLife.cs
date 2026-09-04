using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerRespawner))]
public sealed class PlayerLife : MonoBehaviour
{
    [FormerlySerializedAs("life")]
    [SerializeField, Min(1)] private int startingLife = 3;

    private int currentLife;

    public int StartingLife => startingLife;
    public int Life => currentLife;

    private void Awake()
    {
        ResetForMatch();
    }

    internal bool TryLoseLife()
    {
        if (currentLife <= 0)
        {
            return false;
        }

        currentLife--;
        return true;
    }

    internal void ResetForMatch()
    {
        currentLife = startingLife;
    }

    private void OnValidate()
    {
        startingLife = Mathf.Max(1, startingLife);
    }
}
