public readonly struct PlayerCommand
{
    public float HorizontalMovement { get; }
    public bool JumpPressed { get; }
    public bool DropPressed { get; }
    public bool AttackPressed { get; }
    public float AimHorizontal { get; }

    public PlayerCommand(
        float horizontalMovement,
        bool jumpPressed = false,
        bool dropPressed = false,
        bool attackPressed = false,
        float aimHorizontal = 0f)
    {
        HorizontalMovement = horizontalMovement;
        JumpPressed = jumpPressed;
        DropPressed = dropPressed;
        AttackPressed = attackPressed;
        AimHorizontal = aimHorizontal;
    }
}
