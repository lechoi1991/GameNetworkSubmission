using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Shows the Input System state on screen for the local player.
// Used to diagnose keyboard input not reaching the game in the Editor.
public class InputDiagnosticsOverlay : NetworkBehaviour
{
    [Header("Diagnostics")]
    [SerializeField] private bool showDiagnostics = true;

    private int keyboardEventsThisSecond;
    private int keyboardEventsLastSecond;
    private float secondTimer;
    private bool subscribed;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
            return;

        InputSystem.onEvent += OnInputEvent;
        subscribed = true;
    }

    public override void OnNetworkDespawn()
    {
        Unsubscribe();
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        Unsubscribe();
    }

    private void Unsubscribe()
    {
        if (!subscribed)
            return;

        InputSystem.onEvent -= OnInputEvent;
        subscribed = false;
    }

    // Counts keyboard events that passed the Input System's focus checks.
    private void OnInputEvent(InputEventPtr eventPtr, InputDevice device)
    {
        if (device is Keyboard)
            keyboardEventsThisSecond++;
    }

    private void Update()
    {
        secondTimer += Time.unscaledDeltaTime;

        if (secondTimer < 1f)
            return;

        secondTimer = 0f;
        keyboardEventsLastSecond = keyboardEventsThisSecond;
        keyboardEventsThisSecond = 0;
    }

    private void OnGUI()
    {
        if (!showDiagnostics || !IsSpawned || !IsOwner)
            return;

        InputSettings settings = InputSystem.settings;
        Keyboard keyboard = Keyboard.current;

        string keyboardState = keyboard == null
            ? "null"
            : keyboard.enabled ? "enabled" : "DISABLED";

        string text =
            "[Input diagnostics]\n" +
            $"settings={(settings != null ? settings.name : "null")}, " +
            $"background={settings?.backgroundBehavior}\n" +
#if UNITY_EDITOR
            $"editorPlayMode={settings?.editorInputBehaviorInPlayMode}\n" +
#endif
            $"app.isFocused={Application.isFocused}, " +
            $"runInBackground={Application.runInBackground}\n" +
            $"keyboard={keyboardState}, " +
            $"W={keyboard?.wKey.isPressed}, anyKey={keyboard?.anyKey.isPressed}\n" +
            $"keyboard events/s={keyboardEventsLastSecond}\n" +
            $"player: owner={IsOwner}, server={IsServer}, " +
            $"clientId={OwnerClientId}";

        Rect rect = new Rect(10f, 10f, 520f, 120f);
        GUI.Box(rect, GUIContent.none);
        GUI.Label(new Rect(rect.x + 6f, rect.y + 4f, rect.width - 12f, rect.height - 8f), text);
    }
}
