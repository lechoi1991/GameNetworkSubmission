using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

// Sends coin pickup requests from the owner and returns the server's decision.
// The server decides the result in NetworkCoinSpawner.TryCollectCoin.
[RequireComponent(typeof(NetworkScorePlayer))]
public class NetworkCoinCollector : NetworkBehaviour
{
    [Header("Test Input")]
    // F1-F4 send requests that the server must reject.
    [SerializeField] private bool enableTestInput = true;

    // No coin ever uses this ID, so the server must reject it.
    private const int NonexistentCoinId = 999999;
    private const int MaxResultLines = 5;

    private NetworkScorePlayer scorePlayer;
    private int lastRequestedCoinId = -1;

    // Recent results for the local player, shown by NetworkScoreUI.
    private static readonly Queue<string> localResults = new();

    public static string LocalResultText =>
        string.Join("\n", localResults);

    private void Awake()
    {
        scorePlayer = GetComponent<NetworkScorePlayer>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
            localResults.Clear();
    }

    private void Update()
    {
        if (!IsSpawned || !IsOwner)
            return;

        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
            return;

        // E: request the nearest coin.
        if (keyboard.eKey.wasPressedThisFrame)
            RequestCoin(FindCoinId(nearest: true));

        if (!enableTestInput)
            return;

        // F1: coin ID that does not exist.
        if (keyboard.f1Key.wasPressedThisFrame)
            RequestCoin(NonexistentCoinId);

        // F2: farthest coin, outside the pickup range.
        if (keyboard.f2Key.wasPressedThisFrame)
            RequestCoin(FindCoinId(nearest: false));

        // F3: the same coin twice in one frame.
        if (keyboard.f3Key.wasPressedThisFrame)
        {
            int coinId = FindCoinId(nearest: true);
            RequestCoin(coinId);
            RequestCoin(coinId);
        }

        // F4: the last requested coin again.
        if (keyboard.f4Key.wasPressedThisFrame)
            RequestCoin(lastRequestedCoinId);
    }

    private int FindCoinId(bool nearest)
    {
        int foundId = -1;
        float bestDistance = nearest ? float.MaxValue : -1f;

        foreach (NetworkCoin coin in NetworkCoin.SpawnedCoins)
        {
            Vector3 offset = coin.transform.position - transform.position;
            offset.y = 0f;
            float distance = offset.sqrMagnitude;

            bool better = nearest
                ? distance < bestDistance
                : distance > bestDistance;

            if (better)
            {
                bestDistance = distance;
                foundId = coin.CoinId.Value;
            }
        }

        return foundId;
    }

    private void RequestCoin(int coinId)
    {
        if (coinId < 0)
        {
            AddLocalResult("No coin to request");
            return;
        }

        lastRequestedCoinId = coinId;
        RequestCollectCoinRpc(coinId);
    }

    [Rpc(SendTo.Server)]
    private void RequestCollectCoinRpc(
        int coinId,
        RpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;

        // Accept requests only from this player's owner.
        if (senderId != OwnerClientId)
        {
            Debug.LogWarning(
                $"[CoinRequest] Rejected: client {senderId} " +
                $"is not the owner of player {OwnerClientId}.");
            return;
        }

        CoinCollectResult result = NetworkCoinSpawner.Instance != null
            ? NetworkCoinSpawner.Instance.TryCollectCoin(coinId, scorePlayer)
            : CoinCollectResult.InvalidCoinId;

        Debug.Log(
            $"[CoinRequest] client={senderId} coin={coinId} " +
            $"result={result} score={scorePlayer.Score.Value}");

        CollectResultRpc(coinId, result);
    }

    [Rpc(SendTo.Owner)]
    private void CollectResultRpc(int coinId, CoinCollectResult result)
    {
        Debug.Log($"[CoinResult] coin={coinId} result={result}");
        AddLocalResult($"Coin {coinId}: {result}");
    }

    private static void AddLocalResult(string line)
    {
        localResults.Enqueue(line);

        while (localResults.Count > MaxResultLines)
            localResults.Dequeue();
    }
}
