using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerController2D))]
public sealed class PlayerCommandReceiver : MonoBehaviour
{
    [SerializeField] private PlayerController2D controller;
    [SerializeField] private PlayerShooter shooter;

    public PlayerController2D Controller => controller;
    public PlayerShooter Shooter => shooter;

    private void Awake()
    {
        ResolveReferences();
    }

    public void SubmitCommand(in PlayerCommand command)
    {
        ResolveReferences();
        controller.ApplyCommand(in command);

        if (command.AttackPressed && shooter != null && shooter.isActiveAndEnabled)
        {
            shooter.Fire();
        }
    }

    public void ResetCommands()
    {
        ResolveReferences();
        controller.ResetCommandState();
    }

    private void ResolveReferences()
    {
        if (controller == null)
        {
            controller = GetComponent<PlayerController2D>();
        }

        if (shooter == null)
        {
            shooter = GetComponent<PlayerShooter>();
        }
    }

    private void OnValidate()
    {
        ResolveReferences();
    }
}
