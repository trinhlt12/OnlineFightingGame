using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;
using _GAME.Scripts.FSM;

/// <summary>
/// Base networked state machine that synchronizes states across all clients
/// This is the foundation - concrete states will be implemented later
/// </summary>
public class NetworkedStateMachine : NetworkBehaviour
{
    [Header("Debug")] [SerializeField] private bool enableDebugLogs = true;

    // Networked state ID - this will sync across all clients
    [Networked] public int CurrentStateID { get; set; }

    // Cache for change detection
    private int _lastKnownStateID = -1;

    // Local state management
    private StateNode                   currentState;
    private Dictionary<Type, StateNode> nodes          = new();
    private Dictionary<int, IState>     stateIdToState = new();
    private Dictionary<Type, int>       stateTypeToId  = new();
    private HashSet<ITransition>        anyTransitions = new();

    private int nextStateId = 1;

    // Public properties
    public IState CurrentState  => currentState?.State;
    public bool   IsInitialized { get; private set; }

    // Events
    public event Action<IState, IState> OnStateChanged; // (previousState, newState)

    public override void Spawned()
    {
        base.Spawned();

        // Initialize cache
        _lastKnownStateID = CurrentStateID;

        if (enableDebugLogs) Debug.Log($"[NetworkedStateMachine] Spawned - HasStateAuthority: {HasStateAuthority}");
    }

    public override void FixedUpdateNetwork()
    {
        // Only state authority can evaluate transitions
        if (HasStateAuthority && IsInitialized)
        {
            var transition = GetTransition();
            if (transition != null)
            {
                ChangeState(transition.To);
            }

            // Update current state's FixedUpdate
            currentState?.State?.StateFixedUpdate();
        }
    }

    public override void Render()
    {
        // Check for state changes using cached ID comparison
        if (CurrentStateID != _lastKnownStateID)
        {
            _lastKnownStateID = CurrentStateID;

            // Only apply networked state if we're not the state authority
            // (State authority applies changes locally when making them)
            if (!HasStateAuthority)
            {
                ApplyNetworkedState();
            }
        }

        // All clients update visual state
        if (IsInitialized)
        {
            currentState?.State?.StateUpdate();
        }
    }

    /// <summary>
    /// Initialize the state machine (call this after registering all states)
    /// </summary>
    public void InitializeStateMachine(IState initialState)
    {
        if (IsInitialized)
        {
            Debug.LogWarning("[NetworkedStateMachine] Already initialized!");
            return;
        }

        if (initialState == null)
        {
            Debug.LogError("[NetworkedStateMachine] Initial state cannot be null!");
            return;
        }

        // Ensure the initial state is registered
        GetOrAddNode(initialState);

        if (HasStateAuthority)
        {
            SetState(initialState);
        }

        IsInitialized = true;

        if (enableDebugLogs) Debug.Log($"[NetworkedStateMachine] Initialized with state: {initialState.GetType().Name}");
    }

    /// <summary>
    /// Set initial state (only for state authority)
    /// </summary>
    public void SetState(IState state)
    {
        if (!HasStateAuthority)
        {
            Debug.LogWarning("[NetworkedStateMachine] SetState called on non-authority client");
            return;
        }

        var stateType = state.GetType();
        if (!stateTypeToId.ContainsKey(stateType))
        {
            Debug.LogError($"[NetworkedStateMachine] State {stateType} not registered!");
            return;
        }

        var stateId = stateTypeToId[stateType];

        // Update networked property and cache
        CurrentStateID    = stateId;
        _lastKnownStateID = stateId;

        ApplyStateLocally(state);

        if (enableDebugLogs) Debug.Log($"[NetworkedStateMachine] Set state: {state.GetType().Name} (ID: {stateId})");
    }

    /// <summary>
    /// Change state (only for state authority)
    /// </summary>
    public void ChangeState(IState state)
    {
        if (!HasStateAuthority)
        {
            Debug.LogWarning("[NetworkedStateMachine] ChangeState called on non-authority client");
            return;
        }

        if (state == currentState?.State) return;

        var stateType = state.GetType();
        if (!stateTypeToId.ContainsKey(stateType))
        {
            Debug.LogError($"[NetworkedStateMachine] State {stateType} not registered!");
            return;
        }

        var previousState = currentState?.State;
        var stateId       = stateTypeToId[stateType];

        // Update networked property and cache
        CurrentStateID    = stateId;
        _lastKnownStateID = stateId;

        // Apply state change locally
        ApplyStateTransition(previousState, state);

        if (enableDebugLogs) Debug.Log($"[NetworkedStateMachine] Changed state: {previousState?.GetType().Name} -> {state.GetType().Name}");
    }

    /// <summary>
    /// Register a state with the state machine
    /// Call this before initializing the state machine
    /// </summary>
    public void RegisterState(IState state)
    {
        GetOrAddNode(state);
    }

    /// <summary>
    /// Add transition between states
    /// </summary>
    public void AddTransition(IState from, IState to, IPredicate condition)
    {
        GetOrAddNode(from).AddTransition(GetOrAddNode(to).State, condition);
    }

    /// <summary>
    /// Add transition from any state
    /// </summary>
    public void AddAnyTransition(IState to, IPredicate condition)
    {
        anyTransitions.Add(new Transition(GetOrAddNode(to).State, condition));
    }

    /// <summary>
    /// Get next valid transition
    /// </summary>
    private ITransition GetTransition()
    {
        // Check any transitions first (higher priority)
        foreach (var transition in anyTransitions)
        {
            if (transition.Condition.Evaluate()) return transition;
        }

        // Check current state transitions
        if (currentState != null)
        {
            foreach (var transition in currentState.Transitions)
            {
                if (transition.Condition.Evaluate()) return transition;
            }
        }

        return null;
    }

    /// <summary>
    /// Apply state received from network
    /// </summary>
    private void ApplyNetworkedState()
    {
        if (!stateIdToState.ContainsKey(CurrentStateID))
        {
            Debug.LogError($"[NetworkedStateMachine] Unknown state ID: {CurrentStateID}");
            return;
        }

        var newState      = stateIdToState[CurrentStateID];
        var previousState = currentState?.State;

        ApplyStateTransition(previousState, newState);

        if (enableDebugLogs) Debug.Log($"[NetworkedStateMachine] Applied networked state: {newState.GetType().Name} (ID: {CurrentStateID})");
    }

    /// <summary>
    /// Apply state transition locally
    /// </summary>
    private void ApplyStateTransition(IState previousState, IState newState)
    {
        previousState?.ExitState();
        newState?.EnterState();
        currentState = nodes[newState.GetType()];

        // Trigger event
        OnStateChanged?.Invoke(previousState, newState);
    }

    /// <summary>
    /// Apply state locally without transition logic (for initial state)
    /// </summary>
    private void ApplyStateLocally(IState state)
    {
        currentState = nodes[state.GetType()];
        currentState.State?.EnterState();

        // Trigger event
        OnStateChanged?.Invoke(null, state);
    }

    /// <summary>
    /// Register state and assign ID for networking
    /// </summary>
    private StateNode GetOrAddNode(IState state)
    {
        var stateType = state.GetType();
        var node      = nodes.GetValueOrDefault(stateType);

        if (node == null)
        {
            // Assign unique ID for networking
            var stateId = nextStateId++;

            node = new StateNode(state);
            nodes.Add(stateType, node);
            stateIdToState[stateId]  = state;
            stateTypeToId[stateType] = stateId;

            if (enableDebugLogs) Debug.Log($"[NetworkedStateMachine] Registered state: {stateType.Name} (ID: {stateId})");
        }

        return node;
    }

    /// <summary>
    /// Get all registered states (for debugging)
    /// </summary>
    public IReadOnlyDictionary<Type, int> GetRegisteredStates()
    {
        return stateTypeToId;
    }

    /// <summary>
    /// Force state change for debugging (only works in authority)
    /// </summary>
    public void ForceChangeState<T>() where T : IState
    {
        if (!HasStateAuthority)
        {
            Debug.LogWarning("[NetworkedStateMachine] ForceChangeState can only be called by state authority");
            return;
        }

        var stateType = typeof(T);
        if (stateIdToState.Values.FirstOrDefault(s => s.GetType() == stateType) is IState state)
        {
            ChangeState(state);
        }
        else
        {
            Debug.LogError($"[NetworkedStateMachine] State {stateType.Name} not found");
        }
    }

    /// <summary>
    /// Debug method to log current state information
    /// </summary>
    [ContextMenu("Debug Current State")]
    public void DebugCurrentState()
    {
        Debug.Log($"[NetworkedStateMachine] === State Debug Info ===");
        Debug.Log($"Current State: {(currentState?.State?.GetType().Name ?? "None")}");
        Debug.Log($"Current State ID: {CurrentStateID}");
        Debug.Log($"HasStateAuthority: {HasStateAuthority}");
        Debug.Log($"IsInitialized: {IsInitialized}");
        Debug.Log($"Registered States: {nodes.Count}");

        foreach (var kvp in stateTypeToId)
        {
            Debug.Log($"  - {kvp.Key.Name}: ID {kvp.Value}");
        }
    }
}

/// <summary>
/// StateNode class for managing state and transitions
/// </summary>
public class StateNode
{
    public IState               State       { get; set; }
    public HashSet<ITransition> Transitions { get; }

    public StateNode(IState state)
    {
        State       = state;
        Transitions = new HashSet<ITransition>();
    }

    public void AddTransition(IState to, IPredicate condition)
    {
        Transitions.Add(new Transition(to, condition));
    }
}