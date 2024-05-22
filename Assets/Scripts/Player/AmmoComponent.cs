using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

public class AmmoComponent : NetworkBehaviour
{
    public float maxAmmo = 10f;
    public PlayerController playerController;

    public NetworkVariable<float> currentAmmo = new NetworkVariable<float>(100f);

    public Text ammoText;

    void Start()
    {
        playerController = GetComponent<PlayerController>();
        if (IsServer)
        {
            currentAmmo.Value = maxAmmo;
        }
        UpdateAmmoDisplay();

        // Register callback for health value changes
        currentAmmo.OnValueChanged += OnAmmoChanged;
    }

    public void Update()
    {
        if (IsLocalPlayer && Input.GetKeyDown(KeyCode.K))
        {
            // Request the server to apply damage
            SpendAmmoServerRpc(1);
        }
    }

    [ServerRpc]
    public void SpendAmmoServerRpc(float amount)
    {
        // Server handles the damage
        SpendAmmo(amount);

        // Update health display on clients
        UpdateAmmoDisplayClientRpc(currentAmmo.Value);
    }
    public void SpendAmmo(float amount)
    {
        if (IsServer)
        {
            // Apply damage on the server
            currentAmmo.Value -= amount;
            currentAmmo.Value = Mathf.Max(currentAmmo.Value, 0); // Ensure ammo doesn't go < below 0

            UpdateAmmoDisplayClientRpc(currentAmmo.Value);
        }
    }


    [ClientRpc]
    private void UpdateAmmoDisplayClientRpc(float healthValue)
    {
        if (ammoText != null)
        {
            Debug.Log("sync health");
            ammoText.text = $"{healthValue}/{maxAmmo}";
        }
    }

    public void RestoreAmmo(float amount)
    {
        if (IsServer)
        {
            currentAmmo.Value += amount;
            currentAmmo.Value = Mathf.Min(currentAmmo.Value, maxAmmo); // Ensure ammo doesn't go > above 10
        }
        else
        {
            Debug.Log("Unauthorized ammo gain, player cheating?");
        }
    }

    [ServerRpc]
    public void RestoreAmmoServerRpc(float amount)
    {
        RestoreAmmo(amount);
    }


    public void OnAmmoChanged(float oldAmmo, float newAmmo)
    {
        UpdateAmmoDisplay();
    }

    public void UpdateAmmoDisplay()
    {
        // Update health display locally
        if (ammoText != null)
        {
            ammoText.text = $"{currentAmmo.Value}/{maxAmmo}";
        }
    }

    void OnDestroy()
    {
        // Unregister callback
        currentAmmo.OnValueChanged -= OnAmmoChanged;
    }
}