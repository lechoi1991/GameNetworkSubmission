using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class NetworkPlayerMovement : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private float gravity = -20f;

    private CharacterController characterController;
    private InputAction moveAction;

    // Input currently held by this player.
    private Vector2 localMoveInput;

    // Latest input received by the server.
    private Vector2 serverMoveInput;

    private float verticalVelocity;
    private float lastInputTime;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();

        moveAction = new InputAction(
            name: "Move",
            type: InputActionType.Value,
            binding: "<Gamepad>/leftStick"
        );

        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");

        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/upArrow")
            .With("Down", "<Keyboard>/downArrow")
            .With("Left", "<Keyboard>/leftArrow")
            .With("Right", "<Keyboard>/rightArrow");
    }

    public override void OnNetworkSpawn()
    {
        // Only the server moves CharacterControllers.
        characterController.enabled = IsServer;

        if (IsOwner)
            moveAction.Enable();

        localMoveInput = Vector2.zero;
        serverMoveInput = Vector2.zero;
        verticalVelocity = 0f;
        lastInputTime = Time.time;
    }

    private void Update()
    {
        if (!IsSpawned)
            return;

        // Read input only for the locally controlled player.
        if (IsOwner)
        {
            localMoveInput = moveAction.ReadValue<Vector2>();
            localMoveInput = Vector2.ClampMagnitude(localMoveInput, 1f);

            if (IsServer)
            {
                // Host: process local input directly.
                serverMoveInput = localMoveInput;
                lastInputTime = Time.time;
            }
            else
            {
                // Client: send input to the server.
                SubmitMovementRpc(localMoveInput);
            }
        }

        // Only the server moves player CharacterControllers.
        if (IsServer)
        {
            if (Time.time - lastInputTime > 0.25f)
                serverMoveInput = Vector2.zero;

            MoveOnServer();
        }
    }
    
    [Rpc(SendTo.Server)]
    private void SubmitMovementRpc(
        Vector2 input,
        RpcParams rpcParams = default)
    {
        // Accept input only from this player's owner.
        if (rpcParams.Receive.SenderClientId != OwnerClientId)
            return;

        serverMoveInput = Vector2.ClampMagnitude(input, 1f);
        lastInputTime = Time.time;
    }

private void MoveOnServer()
{
    if (!characterController.enabled)
        return;

    Vector3 before = transform.position;

    Vector3 horizontalMovement = new Vector3(
        serverMoveInput.x,
        0f,
        serverMoveInput.y
    ) * moveSpeed;

    if (characterController.isGrounded && verticalVelocity < 0f)
        verticalVelocity = -2f;

    verticalVelocity += gravity * Time.deltaTime;

    Vector3 movement = horizontalMovement;
    movement.y = verticalVelocity;

    characterController.Move(movement * Time.deltaTime);

    if (serverMoveInput != Vector2.zero)
    {
        Debug.Log(
            $"Input: {serverMoveInput}, " +
            $"Before: {before}, After: {transform.position}"
        );
    }
}

    public override void OnNetworkDespawn()
    {
        if (moveAction != null)
            moveAction.Disable();

        localMoveInput = Vector2.zero;
        serverMoveInput = Vector2.zero;
        verticalVelocity = 0f;
    }

    public override void OnDestroy()
    {
        base.OnDestroy();

        if (moveAction != null)
        {
            moveAction.Dispose();
            moveAction = null;
        }
    }    
}