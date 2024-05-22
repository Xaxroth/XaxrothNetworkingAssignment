using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class GameManager : NetworkBehaviour
{
    // Handles things such as number of players. I would put player names and other properties in here.

    public static GameManager Instance;

    private NetworkVariable<int> _numberOfPlayers = new NetworkVariable<int>();

    public static int playerCounter = 0;

    public NetworkObject PlayerPrefab;

    public Vector2 respawnPosition = Vector2.zero;
    public int NumberOfPlayers { get { return _numberOfPlayers.Value; } }
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    void Start()
    {
        NetworkManager.Singleton.OnClientConnectedCallback += (id) =>
        {
            if (IsServer)
            {
                _numberOfPlayers.Value++;
            }
        };

        NetworkManager.Singleton.OnClientDisconnectCallback += (id) =>
        {
            if (IsServer)
            {
                _numberOfPlayers.Value--;
            }
        };
    }

    [ServerRpc]
    public void RespawnPlayerServerRpc(ulong playerID)
    {
        RespawnPlayer(playerID);
    }

    [ServerRpc]
    public void GameOverServerRpc(ulong playerID)
    {
        NetworkManager.Singleton.DisconnectClient(playerID);
    }

    private void RespawnPlayer(ulong playerID)
    {
        if (IsServer)
        {
            // Retrieve the player NetworkObject using the playerID
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(playerID, out NetworkObject playerToRespawn))
            {
                // Enable the player again
                playerToRespawn.gameObject.SetActive(true);

                // We're already on the server, so we can restore health and ammo directly
                HealthComponent healthComponent = playerToRespawn.GetComponent<HealthComponent>();
                AmmoComponent ammoComponent = playerToRespawn.GetComponent<AmmoComponent>();
                PlayerController playerController = playerToRespawn.GetComponent<PlayerController>();

                if (playerController != null)
                {
                    playerController.Lives.Value--;
                }

                if (healthComponent != null)
                {
                    healthComponent.RestoreHealth(healthComponent.maxHealth);
                }

                if (ammoComponent != null)
                {
                    ammoComponent.RestoreAmmo(ammoComponent.maxAmmo);
                }

                // Move the player to the respawn position and reset the rotation
                playerToRespawn.transform.position = respawnPosition;
                playerToRespawn.transform.rotation = Quaternion.identity;

                UpdatePlayerPositionClientRpc(playerToRespawn.NetworkObjectId, respawnPosition, Quaternion.identity);
            }
            else
            {
                Debug.LogError($"Player with ID {playerID} not found!");
            }

        }
    }

    [ClientRpc]
    private void UpdatePlayerPositionClientRpc(ulong playerID, Vector3 position, Quaternion rotation)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(playerID, out NetworkObject playerToRespawn))
        {
            playerToRespawn.transform.position = position;
            playerToRespawn.transform.rotation = rotation;
        }
    }

    public void DisconnectPlayer(NetworkObject networkObject)
    {
        if (networkObject.IsOwnedByServer)
        {
            NetworkManager.Singleton.DisconnectClient(networkObject.OwnerClientId);
        }
        else
        {
            Debug.Log("ERROR : Trying to disconnect player that does not belong to the server. Not allowed");
        }
    }
}
