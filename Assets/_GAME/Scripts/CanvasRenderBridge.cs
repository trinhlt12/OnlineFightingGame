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
            _isSubscribed = true;
            Debug.Log("[CanvasRenderBridge] Subscribed to state events");
        }

        // Initialize canvas if state is already ready
        if (_canvas != null && !_canvas.IsInitialized)
        {
            _canvas.InitializeFromBridge();
        }
    }

    private void OnStateReady()
    {
        if (_canvas != null && !_canvas.IsInitialized)
        {
            _canvas.InitializeFromBridge();
            Debug.Log("[CanvasRenderBridge] Canvas initialized from state ready event");
        }
    }

    private void OnSelectionChanged()
    {
        // Force UI update when selection changes
        if (_canvas != null && _canvas.IsInitialized)
        {
            _canvas.CheckForNetworkStateChanges();
        }
    }

    public override void Render()
    {
        // This is called every frame, but we only update when necessary
        // The event-based system should handle most updates
        if (CharacterSelectionState.Instance != null && _canvas != null && _canvas.IsInitialized)
        {
            // Optional: Add additional render-time checks here if needed
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (_isSubscribed && CharacterSelectionState.Instance != null)
        {
            CharacterSelectionState.Instance.OnStateSpawned -= OnStateReady;
            CharacterSelectionState.Instance.OnSelectionChanged -= OnSelectionChanged;
        }
    }
}