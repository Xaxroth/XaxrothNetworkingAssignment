using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using static PlayerInput;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : NetworkBehaviour, IPlayerActions
{
    [Header("Logistics")]
    [SerializeField] private NetworkObject _networkObject;

    [SerializeField] private PlayerInput _playerInput;
    [SerializeField] private AmmoComponent _ammoComponent;
    [SerializeField] private HealthComponent _healthComponent;

    [SerializeField] private Transform _shipTransform;
    [SerializeField] private Transform turretPivotTransform;

    [SerializeField] private Rigidbody2D _rb;
    [SerializeField] private BoxCollider2D _collider;

    [SerializeField] private Vector2 _moveInput = new Vector2();
    [SerializeField] private Vector2 _cursorLocation;

    [Header("Cosmetics")]
    [SerializeField] private string playerName;
    [SerializeField] private Text playerNumberText;
    [SerializeField] private int playerNumber;

    [Header("Settings")]
    [SerializeField] private float normalMovementSpeed = 5f;
    [SerializeField] private float movementSpeed = 5f;
    [SerializeField] private float shipRotationSpeed = 100f;
    [SerializeField] private float turretRotationSpeed = 4f;
    [SerializeField] private float fireCooldown = 1f;
    public NetworkVariable<int> Lives = new NetworkVariable<int>(3);
    private bool _superCharged = false;
    private float timeUntilNextFire;

    public NetworkObject projectilePrefab;

    [Header("UI")]
    [SerializeField] private GameObject deathCanvas;

    private void Start()
    {
        _rb = GetComponent<Rigidbody2D>();
        _collider = GetComponent<BoxCollider2D>();
        _ammoComponent = GetComponent<AmmoComponent>();
        _healthComponent = GetComponent<HealthComponent>();

        _shipTransform = transform;
        turretPivotTransform = transform.Find("PivotTurret");
        if (turretPivotTransform == null) Debug.LogError("PivotTurret is not found", gameObject);

        if (IsOwner)
        {
            _playerInput = new PlayerInput();
            _playerInput.Player.SetCallbacks(this);
            _playerInput.Player.Enable();
        }

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

        AssignPlayerNumberServerRpc();
    }

    private void Update()
    {
        if (IsOwner)
        {
            _moveInput = _playerInput.Player.Move.ReadValue<Vector2>();
            _cursorLocation = _playerInput.Player.Aim.ReadValue<Vector2>();

        }

        if (timeUntilNextFire > 0)
        {
            timeUntilNextFire -= Time.deltaTime;
        }
    }

    private void FixedUpdate()
    {
        if (IsOwner && Application.isFocused)
        {
            HandleMovement();
            HandleTurretRotation();
        }

    }

    // Handles the movement, the spawned prefab must belong to the player and the application must be focused for the ship to move.
    private void HandleMovement()
    {
        // Update position and rotation based on input
        Vector2 moveDirection = transform.up * _moveInput.y * movementSpeed;
        _rb.velocity = moveDirection;

        float rotationAmount = _moveInput.x * -shipRotationSpeed * Time.fixedDeltaTime;
        float newRotation = _rb.rotation + rotationAmount;
        _rb.MoveRotation(newRotation);

        // Sync the information with server
        if (IsServer)
        {
            UpdateClientsPositionAndRotationClientRpc(_rb.position, newRotation);
        }
        else
        {
            SubmitMovementServerRpc(_rb.position, newRotation);
        }
    }

    // Aims the turret based on mouse position, application must be focused for it to work
    private void HandleTurretRotation()
    {
        Vector2 screenToWorldPosition = Camera.main.ScreenToWorldPoint(_cursorLocation);
        Vector2 targetDirection = new Vector2(screenToWorldPosition.x - turretPivotTransform.position.x, screenToWorldPosition.y - turretPivotTransform.position.y).normalized;
        Vector2 currentDirection = Vector2.Lerp(turretPivotTransform.up, targetDirection, Time.fixedDeltaTime * turretRotationSpeed);
        turretPivotTransform.up = currentDirection;

        if (IsServer)
        {
            UpdateClientsTurretRotationClientRpc(turretPivotTransform.up);
        }
        else
        {
            SubmitTurretRotationServerRpc(turretPivotTransform.up);
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        if (IsOwner)
        {
            AssignPlayerNumberServerRpc();
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (IsOwner)
        {
            AssignPlayerNumberServerRpc();
        }
    }

    public void DisplayDeathCanvas()
    {
        if (IsOwner)
        {
            Debug.Log("Dead");
            deathCanvas.SetActive(true);
        }
    }

    public void OnFire(InputAction.CallbackContext context)
    {
        if (IsOwner && context.performed && Application.isFocused)
        {
            // Make sure the shooting CD is finished and the ammo is greater than 0
            if (timeUntilNextFire <= 0 && _ammoComponent.currentAmmo.Value > 0)
            {
                // Sends request to the server to create a projectile
                FireProjectileServerRpc();
            }
        }
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        if (IsOwner && context.performed && Application.isFocused)
        {
            _moveInput = context.ReadValue<Vector2>();
        }
    }

    public void OnAim(InputAction.CallbackContext context)
    {
        if (IsOwner && context.performed && Application.isFocused)
        {
            _cursorLocation = context.ReadValue<Vector2>();
        }
    }
    [ServerRpc]
    public void SuperChargeServerRpc(float duration)
    {
        SuperCharge(duration);
        Invoke(nameof(EndSuperChargeServerRpc), duration);
    }

    [ServerRpc]
    public void EndSuperChargeServerRpc()
    {
        EndSuperCharge();
    }

    private void SuperCharge(float duration)
    {
        if (!_superCharged)
        {
            movementSpeed *= 2;
            _superCharged = true;
        }
    }

    private void EndSuperCharge()
    {
        movementSpeed = normalMovementSpeed;
        _superCharged = false;
    }


    // Uses the spawnmanager from the networkmanager singleton to instantiate a projectile. The projectile moves through projectile.cs and exists on the server
    private void FireProjectile()
    {
        NetworkObject projectileNetworkObject = NetworkManager.Singleton.SpawnManager.InstantiateAndSpawn(projectilePrefab);

        if (projectileNetworkObject != null)
        {
            GameObject projectile = projectileNetworkObject.gameObject;
            projectile.transform.position = gameObject.transform.position;
            projectile.transform.rotation = turretPivotTransform.transform.rotation;

            // Get the projectile's collider and ignore collision with shooter, prevents shooting self
            Collider2D projectileCollider = projectile.GetComponent<Collider2D>();
            if (projectileCollider != null && _collider != null)
            {
                Physics2D.IgnoreCollision(projectileCollider, _collider);
            }
        }
    }

    // Asks the server to assign the client names based on their networkObjectID
    [ServerRpc]
    private void AssignPlayerNumberServerRpc()
    {
        playerNumber = (int)NetworkObject.NetworkObjectId;
        playerName = "Player " + playerNumber;
        AssignPlayerNumberClientRpc(playerName, playerNumber);
    }

    [ClientRpc]
    private void AssignPlayerNumberClientRpc(string name, int number)
    {
        playerName = name;
        playerNumber = number;
        playerNumberText.text = playerName;
    }

    // Sends a request tot he server to fire a projectile, which is then broadcasted to all clients through updatefirecooldownclientrpc.
    [ServerRpc]
    private void FireProjectileServerRpc()
    {
        if (timeUntilNextFire <= 0 && _ammoComponent.currentAmmo.Value > 0)
        {
            FireProjectile();
            _ammoComponent.SpendAmmoServerRpc(1);
            timeUntilNextFire = fireCooldown;
            UpdateFireCooldownClientRpc(timeUntilNextFire);
        }
    }

    // Sends input and transform information to the server, calls on the server to update the clients new information and synchronize it across the network.. 
    [ServerRpc]
    private void SubmitMovementServerRpc(Vector2 position, float rotation)
    {
        _rb.position = position;
        _rb.MoveRotation(rotation);
        UpdateClientsPositionAndRotationClientRpc(position, rotation);
    }

    // Sends information about the current rotation to the server, and synchronizes the client's aim position with information given to the server.
    [ServerRpc]
    private void SubmitTurretRotationServerRpc(Vector2 turretUp)
    {
        turretPivotTransform.up = turretUp;
        UpdateClientsTurretRotationClientRpc(turretUp);
    }

    [ClientRpc]
    private void UpdateClientsPositionAndRotationClientRpc(Vector2 position, float rotation)
    {
        if (!IsOwner && Application.isFocused)
        {
            _rb.position = position;
            _rb.MoveRotation(rotation);
        }
    }

    [ClientRpc]
    private void UpdateClientsTurretRotationClientRpc(Vector2 turretUp)
    {
        if (!IsOwner && Application.isFocused)
        {
            turretPivotTransform.up = turretUp;
        }
    }

    [ClientRpc]
    private void UpdateFireCooldownClientRpc(float cooldown)
    {
        timeUntilNextFire = cooldown;
    }

    // Unregister callbacks for disconnected clients
    private void OnDestroy()
    {
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
    }
}

