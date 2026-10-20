using System.Text;
using TMPro;
using Unity.Netcode;
using UnityEngine;

public class NetworkScoreUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text scoreText;

    private void Update()
    {
        if (scoreText == null)
            return;

        NetworkManager networkManager = NetworkManager.Singleton;

        if (networkManager == null || !networkManager.IsListening)
        {
            scoreText.text = "Scores\nWaiting for players...";
            return;
        }

        if (!networkManager.IsClient)
        {
            scoreText.text = "Scores\nWaiting for connection...";
            return;
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Scores");

        foreach (NetworkClient client in networkManager.ConnectedClientsList)
        {
            if (client.PlayerObject == null)
            {
                builder.AppendLine(
                    $"Player {client.ClientId}: Waiting..."
                );
                continue;
            }

            NetworkScorePlayer scorePlayer =
                client.PlayerObject.GetComponent<NetworkScorePlayer>();

            if (scorePlayer == null)
            {
                builder.AppendLine(
                    $"Player {client.ClientId}: Missing score component"
                );
                continue;
            }

            builder.AppendLine(
                $"Player {client.ClientId}: {scorePlayer.Score.Value}"
            );
        }

        scoreText.text = builder.ToString();
    }
}