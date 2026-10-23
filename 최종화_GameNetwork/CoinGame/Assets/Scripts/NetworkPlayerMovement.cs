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

    // Input currently held by this player.
    private Vector2 localMoveInput;

    // Latest input received by the server.
    private Vector2 serverMoveInput;

    private float verticalVelocity;
    private float lastInputTime;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    public override void OnNetworkSpawn()
    {
        // Only the server moves CharacterControllers.
        characterController.enabled = IsServer;

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
            localMoveInput = ReadMoveInput();

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

    // Read WASD/arrow keys and the left stick directly from the devices.
    private static Vector2 ReadMoveInput()
    {
        Vector2 input = Vector2.zero;
        Keyboard keyboard = Keyboard.current;

        if (keyboard != null)
        {
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
                input.y += 1f;

            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
                input.y -= 1f;

            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                input.x += 1f;

            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                input.x -= 1f;
        }

        Gamepad gamepad = Gamepad.current;

        if (gamepad != null)
            input += gamepad.leftStick.ReadValue();

        return Vector2.ClampMagnitude(input, 1f);
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
    }

    public override void OnNetworkDespawn()
    {
        localMoveInput = Vector2.zero;
        serverMoveInput = Vector2.zero;
        verticalVelocity = 0f;
    }
}
