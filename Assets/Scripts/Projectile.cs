using UnityEngine;
using Unity.Netcode;

public class Projectile : NetworkBehaviour
{
    public float speed = 10f;
    public float damageAmount = 20f;
    public float lifeTime = 3;
    public LayerMask playerLayer;
    HealthComponent healthComponent;
    private Rigidbody2D rb;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        if (IsServer)
        {
            rb.velocity = transform.up * speed;
            Invoke("DespawnProjectile", lifeTime);
        }
    }

    void FixedUpdate()
    {
        if (IsServer)
        {
            rb.velocity = transform.up * speed;
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (IsServer && other.CompareTag("Player"))
        {
            healthComponent = other.GetComponent<HealthComponent>();

            if (healthComponent != null)
            {
                DealDamageServerRpc();
            }
        }
    }

    private void DealDamage()
    {
        if (IsServer)
        {
            healthComponent.TakeDamage(damageAmount);
            NetworkObject.Despawn(true);
        }
    }

    // Since we cant call the serverrpc method in playercontroller from the projectile directly, we contact the server through the projectile instead to deal damage to the player.
    [ServerRpc]
    private void DealDamageServerRpc()
    {
        DealDamage();
    }

    private void DespawnProjectile()
    {
        if (IsServer)
        {
            NetworkObject.Despawn(true);
        }
    }
}