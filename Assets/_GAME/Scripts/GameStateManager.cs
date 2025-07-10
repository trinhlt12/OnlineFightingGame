using UnityEngine;
using UI;
using System.Collections;

public class GameStateManager : Singleton<GameStateManager>
{
    [Header("UI State Management")]
    [SerializeField] private bool enableDebugLogs = true;

    // Events for state transitions
    public System.Action OnCharacterSelectionStarted;
    public System.Action OnMapSelectionStarted;
    public System.Action OnGameStarted;

    private UICanvas _currentActiveCanvas;

    public enum GameState
    {
        Menu,
        CharacterSelection,
        MapSelection,
        InGame
    }

    private GameState _currentState = GameState.Menu;
    public GameState CurrentState => _currentState;

    protected override void Awake()
    {
        base.Awake();

        if (enableDebugLogs)
            Debug.Log("[GameStateManager] Initialized");
    }

    /// <summary>
    /// Transitions from Character Selection to Map Selection
    /// This method is called when the host starts the game
    /// </summary>
    public void TransitionToMapSelection()
    {
        if (enableDebugLogs)
            Debug.Log("[GameStateManager] Starting transition to Map Selection");

        StartCoroutine(TransitionToMapSelectionCoroutine());
    }

    private IEnumerator TransitionToMapSelectionCoroutine()
    {
        // Step 1: Hide Character Selection Canvas
        if (enableDebugLogs)
            Debug.Log("[GameStateManager] Step 1: Hiding Character Selection Canvas");

        UIManager.Instance.Hide<CharacterSelectionCanvas>();

        // Optional: Add a small delay for smooth transition
        yield return new WaitForSeconds(0.1f);

        // Step 2: Load and Show Map Selection Canvas from Resources
        if (enableDebugLogs)
            Debug.Log("[GameStateManager] Step 2: Loading Map Selection Canvas from Resources");

        var mapSelectionCanvas = UIManager.Instance.LoadAndShow<MapSelectionCanvas>("UI/MapSelectionCanvas");

        if (mapSelectionCanvas != null)
        {
            _currentActiveCanvas = mapSelectionCanvas;
            _currentState = GameState.MapSelection;

            if (enableDebugLogs)
                Debug.Log("[GameStateManager] Successfully transitioned to Map Selection");

            // Notify listeners
            OnMapSelectionStarted?.Invoke();
        }
        else
        {
            Debug.LogError("[GameStateManager] Failed to load MapSelectionCanvas from Resources!");
        }
    }

    /// <summary>
    /// Transitions from Menu to Character Selection
    /// This is typically called when players join a game session
    /// </summary>
    public void TransitionToCharacterSelection()
    {
        if (enableDebugLogs)
            Debug.Log("[GameStateManager] Transitioning to Character Selection");

        // Hide any current canvas
        if (_currentActiveCanvas != null)
        {
            _currentActiveCanvas.Hide();
        }

        // Show Character Selection Canvas
        var characterCanvas = UIManager.Instance.Show<CharacterSelectionCanvas>();
        if (characterCanvas != null)
        {
            _currentActiveCanvas = characterCanvas;
            _currentState = GameState.CharacterSelection;
            OnCharacterSelectionStarted?.Invoke();
        }
    }

    /// <summary>
    /// Transitions from Map Selection to actual game
    /// This will be implemented later when map selection is complete
    /// </summary>
    public void TransitionToGame()
    {
        if (enableDebugLogs)
            Debug.Log("[GameStateManager] Transitioning to Game");

        // Hide Map Selection Canvas
        UIManager.Instance.Hide<MapSelectionCanvas>();

        _currentState = GameState.InGame;
        OnGameStarted?.Invoke();

        // TODO: Load game scene or initialize game state
        // SceneManager.LoadScene("GameScene");
    }

    /// <summary>
    /// Force transition to a specific state (useful for debugging)
    /// </summary>
    public void ForceTransitionTo(GameState targetState)
    {
        if (enableDebugLogs)
            Debug.Log($"[GameStateManager] Force transitioning to {targetState}");

        switch (targetState)
        {
            case GameState.CharacterSelection:
                TransitionToCharacterSelection();
                break;
            case GameState.MapSelection:
                TransitionToMapSelection();
                break;
            case GameState.InGame:
                TransitionToGame();
                break;
        }
    }

    /// <summary>
    /// Get the current active canvas
    /// </summary>
    public T GetCurrentCanvas<T>() where T : UICanvas
    {
        return _currentActiveCanvas as T;
    }

    /// <summary>
    /// Check if we're in a specific state
    /// </summary>
    public bool IsInState(GameState state)
    {
        return _currentState == state;
    }
}