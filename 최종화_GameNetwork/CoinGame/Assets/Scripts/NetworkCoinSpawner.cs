using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// Result of a coin pickup request decided by the server.
public enum CoinCollectResult
{
    Accepted,
    InvalidCoinId,
    AlreadyCollected,
    TooFar
}

public class NetworkCoinSpawner : MonoBehaviour
{
    public static NetworkCoinSpawner Instance { get; private set; }

    [Header("Coin Settings")]
    [SerializeField] private NetworkObject coinPrefab;
    [SerializeField, Min(1)] private int coinCount = 10;
    [SerializeField, Min(0f)] private float respawnDelay = 3f;

    [Header("Spawn Area")]
    [SerializeField] private float areaWidth = 12f;
    [SerializeField] private float areaDepth = 12f;
    [SerializeField] private float spawnHeight = 0.3f;

    [Header("Spawn Safety")]
    [SerializeField, Min(0f)] private float minPlayerDistance = 2f;
    [SerializeField, Min(1)] private int maxSpawnAttempts = 30;

    [Header("Pickup Rules")]
    [SerializeField, Min(0.1f)] private float pickupRange = 1.5f;

    private NetworkManager networkManager;
    private bool sessionInitialized;

    // Server-side coin state for validating pickup requests.
    private readonly Dictionary<int, NetworkCoin> activeCoins = new();
    private readonly HashSet<int> collectedCoinIds = new();
    private int nextCoinId = 1;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("Duplicate NetworkCoinSpawner found.");
            enabled = false;
            return;
        }

        Instance = this;
        networkManager = NetworkManager.Singleton;
    }

    private void Update()
    {
        if (networkManager == null)
            networkManager = NetworkManager.Singleton;

        if (networkManager == null || !networkManager.IsListening)
        {
            if (sessionInitialized)
            {
                StopAllCoroutines();
                sessionInitialized = false;
                ResetCoinState();
            }

            return;
        }

        if (!networkManager.IsServer || sessionInitialized)
            return;

        sessionInitialized = true;
        ResetCoinState();

        if (coinPrefab == null)
        {
            Debug.LogError(
                "NetworkCoinSpawner: Coin Prefab is not assigned.");
            return;
        }

        StartCoroutine(SpawnInitialCoins());
    }

    private IEnumerator SpawnInitialCoins()
    {
        for (int i = 0; i < coinCount; i++)
        {
            while (sessionInitialized &&
                   networkManager != null &&
                   networkManager.IsListening &&
                   networkManager.IsServer)
            {
                if (SpawnOneCoin())
                    break;

                Debug.LogWarning(
                    "No safe coin spawn position found. Retrying.");

                yield return new WaitForSeconds(1f);
            }

            if (!sessionInitialized)
                yield break;
        }
    }

    // Server only: validate a pickup request and apply it if allowed.
    public CoinCollectResult TryCollectCoin(
        int coinId,
        NetworkScorePlayer player)
    {
        if (!activeCoins.TryGetValue(coinId, out NetworkCoin coin) ||
            coin == null ||
            !coin.IsSpawned)
        {
            return collectedCoinIds.Contains(coinId)
                ? CoinCollectResult.AlreadyCollected
                : CoinCollectResult.InvalidCoinId;
        }

        // Check horizontal distance with the server's player position.
        Vector3 offset =
            coin.transform.position - player.transform.position;
        offset.y = 0f;

        if (offset.sqrMagnitude > pickupRange * pickupRange)
            return CoinCollectResult.TooFar;

        // Mark the coin first so a later request for it is rejected.
        activeCoins.Remove(coinId);
        collectedCoinIds.Add(coinId);

        player.AddScoreFromCoin();
        coin.DespawnOnServer();
        NotifyCoinCollected();

        return CoinCollectResult.Accepted;
    }

    private void ResetCoinState()
    {
        activeCoins.Clear();
        collectedCoinIds.Clear();
        nextCoinId = 1;
    }

    public void NotifyCoinCollected()
    {
        if (networkManager == null)
            networkManager = NetworkManager.Singleton;

        if (networkManager == null ||
            !networkManager.IsListening ||
            !networkManager.IsServer ||
            !sessionInitialized)
        {
            return;
        }

        StartCoroutine(RespawnAfterDelay());
    }

    private IEnumerator RespawnAfterDelay()
    {
        yield return new WaitForSeconds(respawnDelay);

        while (sessionInitialized &&
               networkManager != null &&
               networkManager.IsListening &&
               networkManager.IsServer)
        {
            if (SpawnOneCoin())
                yield break;

            Debug.LogWarning(
                "No safe respawn position found. Retrying.");

            yield return new WaitForSeconds(1f);
        }
    }

    private bool SpawnOneCoin()
    {
        if (!TryFindSpawnPosition(out Vector3 spawnPosition))
            return false;

        NetworkObject coin = Instantiate(
            coinPrefab,
            spawnPosition,
            Quaternion.identity
        );

        NetworkCoin networkCoin = coin.GetComponent<NetworkCoin>();
        int coinId = nextCoinId++;

        // Set the ID before spawning so clients receive it with the coin.
        networkCoin.CoinId.Value = coinId;
        coin.Spawn();

        activeCoins[coinId] = networkCoin;
        return true;
    }

    private bool TryFindSpawnPosition(out Vector3 spawnPosition)
    {
        for (int i = 0; i < maxSpawnAttempts; i++)
        {
            Vector3 candidate = new Vector3(
                Random.Range(-areaWidth / 2f, areaWidth / 2f),
                spawnHeight,
                Random.Range(-areaDepth / 2f, areaDepth / 2f)
            );

            if (IsFarEnoughFromPlayers(candidate))
            {
                spawnPosition = candidate;
                return true;
            }
        }

        spawnPosition = Vector3.zero;
        return false;
    }

    private bool IsFarEnoughFromPlayers(Vector3 candidate)
    {
        if (networkManager == null)
            return false;

        float minDistanceSqr =
            minPlayerDistance * minPlayerDistance;

        foreach (NetworkClient client
                 in networkManager.ConnectedClientsList)
        {
            if (client.PlayerObject == null)
                continue;

            Vector3 playerPosition =
                client.PlayerObject.transform.position;

            // Check horizontal distance only.
            float dx = candidate.x - playerPosition.x;
            float dz = candidate.z - playerPosition.z;

            if (dx * dx + dz * dz < minDistanceSqr)
                return false;
        }

        return true;
    }

    private void OnDestroy()
    {
        StopAllCoroutines();

        if (Instance == this)
            Instance = null;
    }
}