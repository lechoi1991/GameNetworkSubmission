using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class NetworkCoin : NetworkBehaviour
{
    // Coins currently spawned on this peer.
    // The local player uses this list to choose a coin to request.
    public static readonly List<NetworkCoin> SpawnedCoins = new();

    // Session-unique ID assigned by the server.
    // Unlike NetworkObjectId, it is never reused in a session.
    public NetworkVariable<int> CoinId = new NetworkVariable<int>(
        -1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // ID chosen by the spawner before Spawn(). A NetworkVariable
    // cannot be written until the object is spawned.
    private int pendingCoinId = -1;

    public void AssignIdBeforeSpawn(int coinId)
    {
        pendingCoinId = coinId;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
            CoinId.Value = pendingCoinId;

        SpawnedCoins.Add(this);
    }

    public override void OnNetworkDespawn()
    {
        SpawnedCoins.Remove(this);
    }

    // Called by NetworkCoinSpawner after a request is accepted.
    public void DespawnOnServer()
    {
        if (!IsServer || !IsSpawned)
            return;

        NetworkObject.Despawn();
    }
}
