using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

public class HealthComponent : NetworkBehaviour
{
    public float maxHealth = 100f;
    public PlayerController playerController;

    public NetworkVariable<float> currentHealth = new NetworkVariable<float>(100f);

    public Text healthText;

    void Start()
    {
        playerController = GetComponent<PlayerController>();
        if (IsServer)
        {
            currentHealth.Value = maxHealth;
        }
        UpdateHealthDisplay();

        // Register callback for health value changes
        currentHealth.OnValueChanged += OnHealthChanged;
    }

    public void Update()
    {
        if (IsLocalPlayer && Input.GetKeyDown(KeyCode.K))
        {
            // Request the server to apply damage (Debug)
            TakeDamageServerRpc(20);
        }
    }

    [ServerRpc]
    public void TakeDamageServerRpc(float amount)
    {
        // Server handles the damage
        TakeDamage(amount);

        // Update health display on clients
        UpdateHealthDisplayClientRpc(currentHealth.Value);
    }
    public void TakeDamage(float amount)
    {
        if (IsServer)
        {
            // Apply damage on the server
            currentHealth.Value -= amount;
            currentHealth.Value = Mathf.Max(currentHealth.Value, 0); // Ensure health doesn't go < below 0 (minhealth)

            if (currentHealth.Value <= 0)
            {
                currentHealth.Value = 0;
                NetworkObject.gameObject.SetActive(false);

                // Request server to respawn player
                if (playerController.Lives.Value > 0)
                {
                    // Respawn the player in the middle
                    GameManager.Instance.RespawnPlayerServerRpc(NetworkObject.NetworkObjectId);
                }
                else
                {
                    // Player has no more lives, disconnect them from the game
                    GameManager.Instance.GameOverServerRpc(NetworkObject.NetworkObjectId);
                }
            }

            UpdateHealthDisplayClientRpc(currentHealth.Value);
        }
    }


    [ClientRpc]
    private void UpdateHealthDisplayClientRpc(float healthValue)
    {
        if (healthText != null)
        {
            Debug.Log("sync health");
            healthText.text = $"{healthValue}/{maxHealth}";
        }
    }

    public void RestoreHealth(float amount)
    {
        if (IsServer)
        {
            currentHealth.Value += amount;
            currentHealth.Value = Mathf.Min(currentHealth.Value, maxHealth); // Ensure health doesn't go > above 100 (maxhealth)
        }
        else
        {
            Debug.Log("Unauthorized healing, player cheating?");
        }
    }

    [ServerRpc]
    public void RestoreHealthServerRpc(float amount)
    {
        RestoreHealth(amount);
    }


    public void OnHealthChanged(float oldHealth, float newHealth)
    {
        // Update health display on clients
        UpdateHealthDisplay();
    }

    public void UpdateHealthDisplay()
    {
        // Update health display locally
        if (healthText != null)
        {
            healthText.text = $"{currentHealth.Value}/{maxHealth}";
        }
    }

    void OnDestroy()
    {
        // Unregister callback
        currentHealth.OnValueChanged -= OnHealthChanged;
    }
}