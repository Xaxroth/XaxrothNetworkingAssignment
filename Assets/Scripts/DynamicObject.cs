using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class DynamicObject : NetworkBehaviour
{
    [SerializeField] private float speed = 0.05f;

    [SerializeField] private Vector2 Position = new Vector2(-10, 10);

    [SerializeField] private NetworkVariable<float> VerticalDirection = new NetworkVariable<float>();
    [SerializeField] private NetworkVariable<float> HorizontalDirection = new NetworkVariable<float>();

    private float PreviousVerticalPosition;
    private float PreviousHorizontalPosition;

    void Start()
    {
        transform.position = new Vector2(Random.Range(Position.x, Position.y), 0);

        VerticalDirection.Value = 0f;
        HorizontalDirection.Value = 0f;

        VerticalDirection.OnValueChanged += OnVerticalDirectionChanged;
        HorizontalDirection.OnValueChanged += OnHorizontalDirectionChanged;
    }

    private void OnDestroy()
    {
        VerticalDirection.OnValueChanged -= OnVerticalDirectionChanged;
        HorizontalDirection.OnValueChanged -= OnHorizontalDirectionChanged;
    }

    void Update()
    {
        if (IsOwner)
        {
            float verticalInput = Input.GetAxis("Vertical");
            float horizontalInput = Input.GetAxis("Horizontal");

            VerticalDirection.Value = verticalInput * speed;
            HorizontalDirection.Value = horizontalInput * speed;
        }
    }

    private void OnVerticalDirectionChanged(float oldValue, float newValue)
    {
        VerticalDirectionServerRpc(newValue);
    }

    private void OnHorizontalDirectionChanged(float oldValue, float newValue)
    {
        HorizontalDirectionServerRpc(newValue);
    }

    [ServerRpc(RequireOwnership = false)]
    private void VerticalDirectionServerRpc(float newValue)
    {
        VerticalDirection.Value = newValue;
    }

    [ServerRpc(RequireOwnership = false)]
    private void HorizontalDirectionServerRpc(float newValue)
    {
        HorizontalDirection.Value = newValue;
    }
}