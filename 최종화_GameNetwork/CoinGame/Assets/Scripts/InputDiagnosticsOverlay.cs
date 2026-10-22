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

    // Events that reached InputSystem.onEvent in the last second, by device.
    private int keyboardEventsThisSecond;
    private int mouseEventsThisSecond;
    private int otherEventsThisSecond;
    private int keyboardEventsLastSecond;
    private int mouseEventsLastSecond;
    private int otherEventsLastSecond;

    // Totals from InputSystem.metrics, which count events before device lookup.
    private int metricEventsAtSecondStart;
    private int metricEventsLastSecond;
    private uint updatesAtSecondStart;
    private uint updatesLastSecond;

    private float secondTimer;
    private bool subscribed;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
            return;

        InputSystem.onEvent += OnInputEvent;
        subscribed = true;
        metricEventsAtSecondStart = InputSystem.metrics.totalEventCount;
        updatesAtSecondStart = InputState.updateCount;
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

    // Counts events that passed the Input System's focus and routing checks.
    private void OnInputEvent(InputEventPtr eventPtr, InputDevice device)
    {
        if (device is Keyboard)
            keyboardEventsThisSecond++;
        else if (device is Mouse)
            mouseEventsThisSecond++;
        else
            otherEventsThisSecond++;
    }

    private void Update()
    {
        secondTimer += Time.unscaledDeltaTime;

        if (secondTimer < 1f)
            return;

        secondTimer = 0f;

        keyboardEventsLastSecond = keyboardEventsThisSecond;
        mouseEventsLastSecond = mouseEventsThisSecond;
        otherEventsLastSecond = otherEventsThisSecond;
        keyboardEventsThisSecond = 0;
        mouseEventsThisSecond = 0;
        otherEventsThisSecond = 0;

        int metricEvents = InputSystem.metrics.totalEventCount;
        metricEventsLastSecond = metricEvents - metricEventsAtSecondStart;
        metricEventsAtSecondStart = metricEvents;

        uint updates = InputState.updateCount;
        updatesLastSecond = updates - updatesAtSecondStart;
        updatesAtSecondStart = updates;
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
            $"[Input diagnostics] Unity {Application.unityVersion}\n" +
            $"settings={(settings != null ? settings.name : "null")}, " +
            $"background={settings?.backgroundBehavior}, " +
            $"updateMode={settings?.updateMode}\n" +
#if UNITY_EDITOR
            $"editorPlayMode={settings?.editorInputBehaviorInPlayMode}\n" +
#endif
            $"app.isFocused={Application.isFocused}, " +
            $"runInBackground={Application.runInBackground}\n" +
            $"keyboard={keyboardState}, id={keyboard?.deviceId}, " +
            $"W={keyboard?.wKey.isPressed}, anyKey={keyboard?.anyKey.isPressed}, " +
            $"imeSelected={keyboard?.imeSelected.isPressed}\n" +
            $"onEvent/s: keyboard={keyboardEventsLastSecond}, " +
            $"mouse={mouseEventsLastSecond}, other={otherEventsLastSecond}\n" +
            $"metrics events/s={metricEventsLastSecond}, " +
            $"updates/s={updatesLastSecond}, " +
            $"lastUpdateType={InputState.currentUpdateType}\n" +
            $"keyboard.lastUpdateTime={keyboard?.lastUpdateTime:F2}, " +
            $"now={InputState.currentTime:F2}\n" +
            $"player: owner={IsOwner}, server={IsServer}, " +
            $"clientId={OwnerClientId}";

        Rect rect = new Rect(10f, 10f, 560f, 150f);
        GUI.Box(rect, GUIContent.none);
        GUI.Label(new Rect(rect.x + 6f, rect.y + 4f, rect.width - 12f, rect.height - 8f), text);
    }
}
