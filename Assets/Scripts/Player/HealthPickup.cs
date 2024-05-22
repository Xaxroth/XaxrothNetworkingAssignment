using UnityEngine;
using Unity.Netcode;

public class HealthPickup : NetworkBehaviour
{
    public enum TypeOfPickup
    {
        Health,
        Ammo,
        Speed
    }

    public TypeOfPickup Type;

    [SerializeField] private float RestoreAmount = 20f;

    void OnTriggerEnter2D(Collider2D other)
    {
        switch (Type)
        {
            case TypeOfPickup.Health:

                if (IsServer && other.CompareTag("Player"))
                {
                    HealthComponent healthComponent = other.GetComponent<HealthComponent>();
                    if (healthComponent != null)
                    {
                        healthComponent.RestoreHealthServerRpc(RestoreAmount);

                        NetworkObject.Despawn(true);
                    }
                }
                break;
            case TypeOfPickup.Ammo:

                if (IsServer && other.CompareTag("Player"))
                {
                    AmmoComponent ammoComponent = other.GetComponent<AmmoComponent>();
                    if (ammoComponent != null)
                    {
                        ammoComponent.RestoreAmmoServerRpc(RestoreAmount);

                        NetworkObject.Despawn(true);
                    }
                }
                break;
            case TypeOfPickup.Speed:

                if (IsServer && other.CompareTag("Player"))
                {
                    PlayerController playerController = other.GetComponent<PlayerController>();
                    if (playerController != null)
                    {
                        playerController.SuperChargeServerRpc(RestoreAmount);

                        NetworkObject.Despawn(true);
                    }
                }
                break;
        }
    }
}