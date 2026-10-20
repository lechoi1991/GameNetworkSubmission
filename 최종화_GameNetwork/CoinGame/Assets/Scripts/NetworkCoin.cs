using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class NetworkCoin : NetworkBehaviour
{
    private bool isCollected;

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer || !IsSpawned || isCollected)
            return;

        NetworkObject playerObject =
            other.GetComponentInParent<NetworkObject>();

        if (playerObject == null || !playerObject.IsPlayerObject)
            return;

        NetworkScorePlayer scorePlayer =
            playerObject.GetComponent<NetworkScorePlayer>();

        if (scorePlayer == null)
        {
            Debug.LogWarning(
                "Player prefab is missing NetworkScorePlayer.");
            return;
        }

        // Prevent multiple pickups of the same coin.
        isCollected = true;

        // Award the point on the server.
        scorePlayer.AddScoreFromCoin();

        // Schedule a replacement, then remove this network coin.
        if (NetworkCoinSpawner.Instance != null)
            NetworkCoinSpawner.Instance.NotifyCoinCollected();

        NetworkObject.Despawn();
    }
}