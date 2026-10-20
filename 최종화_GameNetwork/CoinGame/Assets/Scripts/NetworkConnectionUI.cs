using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UI;

public class NetworkConnectionUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button hostButton;
    [SerializeField] private Button clientButton;
    [SerializeField] private Button disconnectButton;
    [SerializeField] private TMP_Text statusText;

    [Header("Connection Settings")]
    [SerializeField] private string address = "127.0.0.1";
    [SerializeField] private ushort port = 7777;

    private NetworkManager networkManager;

    private bool isConnecting;
    private string statusMessage = "Status: Offline";

    private void Awake()
    {
        if (hostButton != null)
            hostButton.onClick.AddListener(StartHostFromButton);

        if (clientButton != null)
            clientButton.onClick.AddListener(StartClientFromButton);

        if (disconnectButton != null)
            disconnectButton.onClick.AddListener(DisconnectFromButton);
    }

    private void Start()
    {
        RefreshNetworkManager();

        if (networkManager == null)
        {
            statusMessage = "NetworkManager Not Found";

            Debug.LogError(
                "NetworkManager.Singleton is null. " +
                "Check that an active NetworkManager exists in the scene."
            );

            UpdateUI();
            return;
        }

        networkManager.OnClientConnectedCallback += OnClientConnected;
        networkManager.OnClientDisconnectCallback += OnClientDisconnected;

        statusMessage = "Status: Offline";
        UpdateUI();
    }

    private void Update()
    {
        if (networkManager == null)
            RefreshNetworkManager();

        if (networkManager == null)
        {
            UpdateUI();
            return;
        }

        if (networkManager.IsHost)
        {
            isConnecting = false;
            statusMessage =
                $"Host: Online | Players: " +
                $"{networkManager.ConnectedClientsIds.Count}";
        }
        else if (networkManager.IsServer)
        {
            isConnecting = false;
            statusMessage = "Server: Online";
        }
        else if (networkManager.IsClient &&
                 networkManager.IsConnectedClient)
        {
            isConnecting = false;
            statusMessage = "Client: Connected to Host";
        }
        else if (isConnecting)
        {
            statusMessage = "Client: Connecting...";
        }
        else if (!networkManager.IsListening &&
                 statusMessage != "Client: Disconnected" &&
                 statusMessage != "Connection Failed" &&
                 statusMessage != "NetworkManager Not Found")
        {
            statusMessage = "Status: Offline";
        }

        UpdateUI();
    }

    public void StartHostFromButton()
    {
        if (!CanStartConnection())
            return;

        UnityTransport transport =
            networkManager.GetComponent<UnityTransport>();

        if (transport == null)
        {
            statusMessage = "UnityTransport Not Found";
            UpdateUI();
            return;
        }

        isConnecting = true;
        statusMessage = "Starting Host...";
        UpdateUI();

        transport.SetConnectionData(address, port, address);

        bool started = networkManager.StartHost();

        if (!started)
        {
            isConnecting = false;
            statusMessage = "Failed to Start Host";
            UpdateUI();
            return;
        }

        isConnecting = false;
        statusMessage = "Host: Online";
        UpdateUI();
    }

    public void StartClientFromButton()
    {
        if (!CanStartConnection())
            return;

        UnityTransport transport =
            networkManager.GetComponent<UnityTransport>();

        if (transport == null)
        {
            statusMessage = "UnityTransport Not Found";
            UpdateUI();
            return;
        }

        isConnecting = true;
        statusMessage = "Client: Connecting...";
        UpdateUI();

        transport.SetConnectionData(address, port);

        bool started = networkManager.StartClient();

        if (!started)
        {
            isConnecting = false;
            statusMessage = "Failed to Start Client";
            UpdateUI();
            return;
        }

        // A successful StartClient call does not guarantee
        // that the Host connection has completed.
    }

    private bool CanStartConnection()
    {
        RefreshNetworkManager();

        if (networkManager == null)
        {
            statusMessage = "NetworkManager Not Found";
            UpdateUI();
            return false;
        }

        if (networkManager.IsListening || isConnecting)
        {
            statusMessage = "Network Is Already Running";
            UpdateUI();
            return false;
        }

        return true;
    }

    private void DisconnectFromButton()
    {
        RefreshNetworkManager();

        if (networkManager != null &&
            networkManager.IsListening)
        {
            networkManager.Shutdown();
        }

        isConnecting = false;
        statusMessage = "Status: Offline";
        UpdateUI();
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"Client connected: {clientId}");

        isConnecting = false;

        if (networkManager == null)
            return;

        if (networkManager.IsHost)
        {
            statusMessage =
                $"Host: Online | Players: " +
                $"{networkManager.ConnectedClientsIds.Count}";
        }
        else if (networkManager.IsClient &&
                 clientId == networkManager.LocalClientId)
        {
            statusMessage = "Client: Connected to Host";
        }

        UpdateUI();
    }

    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"Client disconnected: {clientId}");

        if (networkManager == null)
        {
            isConnecting = false;
            statusMessage = "Status: Offline";
            UpdateUI();
            return;
        }

        if (networkManager.IsHost && networkManager.IsListening)
        {
            // A remote Client disconnected; keep the Host running.
            statusMessage =
                $"Host: Online | Players: " +
                $"{networkManager.ConnectedClientsIds.Count}";
        }
        else if (networkManager.IsListening)
        {
            isConnecting = false;
            statusMessage = "Client: Disconnected";
        }
        else
        {
            isConnecting = false;
            statusMessage = "Status: Offline";
        }

        UpdateUI();
    }

    private void RefreshNetworkManager()
    {
        if (networkManager == null)
            networkManager = NetworkManager.Singleton;
    }

    private void UpdateUI()
    {
        bool isListening =
            networkManager != null &&
            networkManager.IsListening;

        bool isBusy = isListening || isConnecting;

        if (hostButton != null)
            hostButton.interactable = !isBusy;

        if (clientButton != null)
            clientButton.interactable = !isBusy;

        if (disconnectButton != null)
            disconnectButton.interactable = isBusy;

        if (statusText != null)
            statusText.text = statusMessage;
    }

    private void OnDestroy()
    {
        if (hostButton != null)
            hostButton.onClick.RemoveListener(StartHostFromButton);

        if (clientButton != null)
            clientButton.onClick.RemoveListener(StartClientFromButton);

        if (disconnectButton != null)
            disconnectButton.onClick.RemoveListener(DisconnectFromButton);

        if (networkManager != null)
        {
            networkManager.OnClientConnectedCallback -= OnClientConnected;
            networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }
}