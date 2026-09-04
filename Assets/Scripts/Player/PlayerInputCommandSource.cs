using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(PlayerCommandReceiver))]
public sealed class PlayerInputCommandSource : MonoBehaviour
{
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private PlayerCommandReceiver commandReceiver;

    private InputAction moveAction;
    private InputAction jumpAction;
    private InputAction dropAction;
    private InputAction attackAction;
    private float horizontalMovement;
    private bool isSubscribed;

    public PlayerInput PlayerInput => playerInput;
    public PlayerCommandReceiver CommandReceiver => commandReceiver;
    public bool IsSubscribed => isSubscribed;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        TrySubscribe();
    }

    private void Start()
    {
        if (!TrySubscribe())
        {
            Debug.LogError(
                "PlayerInputCommandSource could not find every required Player input action.",
                this);
        }
    }

    private void OnDisable()
    {
        Unsubscribe();
        horizontalMovement = 0f;
        if (commandReceiver != null)
        {
            commandReceiver.ResetCommands();
        }
    }

    private bool TrySubscribe()
    {
        if (isSubscribed)
        {
            return true;
        }

        if (playerInput == null || playerInput.actions == null)
        {
            return false;
        }

        moveAction = playerInput.actions.FindAction("Player/Move", throwIfNotFound: false);
        jumpAction = playerInput.actions.FindAction("Player/Jump", throwIfNotFound: false);
        dropAction = playerInput.actions.FindAction("Player/Drop", throwIfNotFound: false);
        attackAction = playerInput.actions.FindAction("Player/Attack", throwIfNotFound: false);

        if (moveAction == null || jumpAction == null || dropAction == null || attackAction == null)
        {
            ClearActions();
            return false;
        }

        moveAction.performed += OnMoveChanged;
        moveAction.canceled += OnMoveChanged;
        jumpAction.performed += OnJumpPerformed;
        dropAction.performed += OnDropPerformed;
        attackAction.performed += OnAttackPerformed;
        isSubscribed = true;
        horizontalMovement = Mathf.Clamp(
            moveAction.ReadValue<Vector2>().x,
            -1f,
            1f);
        SubmitCommand();
        return true;
    }

    private void Unsubscribe()
    {
        if (!isSubscribed)
        {
            ClearActions();
            return;
        }

        moveAction.performed -= OnMoveChanged;
        moveAction.canceled -= OnMoveChanged;
        jumpAction.performed -= OnJumpPerformed;
        dropAction.performed -= OnDropPerformed;
        attackAction.performed -= OnAttackPerformed;
        isSubscribed = false;
        ClearActions();
    }

    private void OnMoveChanged(InputAction.CallbackContext context)
    {
        horizontalMovement = Mathf.Clamp(
            context.ReadValue<Vector2>().x,
            -1f,
            1f);
        SubmitCommand();
    }

    private void OnJumpPerformed(InputAction.CallbackContext context)
    {
        SubmitCommand(jumpPressed: true);
    }

    private void OnDropPerformed(InputAction.CallbackContext context)
    {
        SubmitCommand(dropPressed: true);
    }

    private void OnAttackPerformed(InputAction.CallbackContext context)
    {
        SubmitCommand(attackPressed: true);
    }

    private void SubmitCommand(
        bool jumpPressed = false,
        bool dropPressed = false,
        bool attackPressed = false)
    {
        if (commandReceiver == null)
        {
            return;
        }

        PlayerCommand command = new(
            horizontalMovement,
            jumpPressed,
            dropPressed,
            attackPressed);
        commandReceiver.SubmitCommand(in command);
    }

    private void ClearActions()
    {
        moveAction = null;
        jumpAction = null;
        dropAction = null;
        attackAction = null;
    }

    private void ResolveReferences()
    {
        if (playerInput == null)
        {
            playerInput = GetComponent<PlayerInput>();
        }

        if (commandReceiver == null)
        {
            commandReceiver = GetComponent<PlayerCommandReceiver>();
        }
    }

    private void OnValidate()
    {
        ResolveReferences();
    }
}
