using System.Collections;
using Unity.Netcode;
using UnityEngine;

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

    private NetworkManager networkManager;
    private bool sessionInitialized;

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
            }

            return;
        }

        if (!networkManager.IsServer || sessionInitialized)
            return;

        sessionInitialized = true;

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

        coin.Spawn();
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