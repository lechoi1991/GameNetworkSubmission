using Unity.Netcode;
using UnityEngine;

public class NetworkScorePlayer : NetworkBehaviour
{
    // The server owns the score value.
    // The value is synchronized with connected clients.
    public NetworkVariable<int> Score = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public void AddScoreFromCoin()
    {
        // Only the server can award points.
        if (!IsServer || !IsSpawned)
            return;

        Score.Value += 1;

        Debug.Log(
            $"Player {OwnerClientId} score: {Score.Value}"
        );
    }
}