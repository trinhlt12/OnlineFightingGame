using System.Collections;
using _GAME.Scripts.CharacterSelection;
using Fusion;
using UI;
using UnityEngine;

[RequireComponent(typeof(CharacterSelectionCanvas))]
public class CanvasRenderBridge : NetworkBehaviour
{
    private CharacterSelectionCanvas _canvas;
    private bool _isSubscribed = false;

    public override void Spawned()
    {
        _canvas = GetComponent<CharacterSelectionCanvas>();
        StartCoroutine(SetupBridge());
    }

    private IEnumerator SetupBridge()
    {
        // Wait for CharacterSelectionState to be ready
        yield return new WaitUntil(() => CharacterSelectionState.Instance != null);

        // Subscribe to state events if not already subscribed
        if (!_isSubscribed)
        {
            CharacterSelectionState.Instance.OnStateSpawned += OnStateReady;
            CharacterSelectionState.Instance.OnSelectionChanged += OnSelectionChanged;
            CharacterSelectionState.Instance.OnReadyStateChanged += OnReadyStateChanged;
            _isSubscribed = true;
            Debug.Log($"[CanvasRenderBridge] Subscribed to state events on {(HasStateAuthority ? "Host" : "Client")}");
        }

        // Initialize canvas if state is already ready
        if (_canvas != null && !_canvas.IsInitialized)
        {
            _canvas.InitializeFromBridge();
        }
    }

    private void OnStateReady()
    {
        Debug.Log($"[CanvasRenderBridge] OnStateReady called on {(HasStateAuthority ? "Host" : "Client")}");
        if (_canvas != null && !_canvas.IsInitialized)
        {
            _canvas.InitializeFromBridge();
            Debug.Log("[CanvasRenderBridge] Canvas initialized from state ready event");
        }
    }

    private void OnSelectionChanged()
    {
        Debug.Log($"[CanvasRenderBridge] OnSelectionChanged called on {(HasStateAuthority ? "Host" : "Client")}");

        // Force UI update when selection changes
        if (_canvas != null && _canvas.IsInitialized)
        {
            _canvas.CheckForNetworkStateChanges();
        }
    }

    private void OnReadyStateChanged()
    {
        Debug.Log($"[CanvasRenderBridge] OnReadyStateChanged called on {(HasStateAuthority ? "Host" : "Client")}");

        // Force UI update when ready state changes
        if (_canvas != null && _canvas.IsInitialized)
        {
            _canvas.CheckForNetworkStateChanges();
        }
    }

    public override void Render()
    {
        // Only update when necessary, not every frame
        // The event-based system should handle updates
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (_isSubscribed && CharacterSelectionState.Instance != null)
        {
            CharacterSelectionState.Instance.OnStateSpawned -= OnStateReady;
            CharacterSelectionState.Instance.OnSelectionChanged -= OnSelectionChanged;
            CharacterSelectionState.Instance.OnReadyStateChanged -= OnReadyStateChanged;
        }
    }

    // Debug method to force refresh
    public void ForceRefresh()
    {
        Debug.Log($"[CanvasRenderBridge] ForceRefresh called on {(HasStateAuthority ? "Host" : "Client")}");
        if (_canvas != null && _canvas.IsInitialized)
        {
            _canvas.CheckForNetworkStateChanges();
        }
    }
}