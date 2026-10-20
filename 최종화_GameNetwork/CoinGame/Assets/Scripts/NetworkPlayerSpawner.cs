using Unity.Netcode;
using UnityEngine;

public class NetworkPlayerSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private float spawnSpacing = 3f;

    private NetworkManager networkManager;

    private void Awake()
    {
        networkManager = GetComponent<NetworkManager>();
    }

    private void OnEnable()
    {
        if (networkManager == null)
            networkManager = GetComponent<NetworkManager>();

        if (networkManager != null)
        {
            networkManager.OnServerStarted += OnServerStarted;
            networkManager.OnClientConnectedCallback += OnClientConnected;
        }
    }

    private void OnDisable()
    {
        if (networkManager != null)
        {
            networkManager.OnServerStarted -= OnServerStarted;
            networkManager.OnClientConnectedCallback -= OnClientConnected;
        }
    }

    private void OnServerStarted()
    {
        // Host is also a local client.
        // Assign the Host's player position after it is spawned.
        if (networkManager.IsHost)
        {
            SetPlayerSpawnPosition(networkManager.LocalClientId);
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        if (!networkManager.IsServer)
            return;

        SetPlayerSpawnPosition(clientId);
    }

    private void SetPlayerSpawnPosition(ulong clientId)
    {
        if (!networkManager.ConnectedClients.TryGetValue(
                clientId, out NetworkClient client))
        {
            return;
        }

        if (client.PlayerObject == null)
            return;

        // Assign a different horizontal position to each client.
        Vector3 spawnPosition = new Vector3(
            (float)clientId * spawnSpacing,
            1f,
            0f
        );

        client.PlayerObject.transform.position = spawnPosition;
    }
}