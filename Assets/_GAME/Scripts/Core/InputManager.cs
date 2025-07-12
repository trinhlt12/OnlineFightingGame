using UnityEngine;
using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using _GAME.Scripts.Core;
using _GAME.Scripts.Combat; // For AttackInputType

/// <summary>
/// Enhanced input manager - handles combat input via NetworkInputData
/// More efficient than RPC approach for frequent inputs
/// </summary>
public class InputManager : MonoBehaviour, INetworkRunnerCallbacks
{
    [Header("Settings")]
    [SerializeField] private float deadZone = 0.1f;

    [Header("Combat Input")]
    [SerializeField] private KeyCode attackKey = KeyCode.J;
    [SerializeField] private KeyCode specialKey = KeyCode.K;
    [SerializeField] private KeyCode dodgeKey = KeyCode.L;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    private NetworkInputData _inputData;

    private void Start()
    {
        var runner = FindObjectOfType<NetworkRunner>();
        if (runner != null)
        {
            runner.AddCallbacks(this);
            Debug.Log("[InputManager] Registered with NetworkRunner");
        }
        else
        {
            Debug.LogError("[InputManager] NetworkRunner not found!");
        }
    }

    private void Update()
    {
        // Note: Main input processing now happens in OnInput()
        // This Update() can be used for local UI input that doesn't need networking
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        // Store the previous frame's button states before updating
        _inputData.previousButtons = _inputData.buttons;

        // Reset the current frame's buttons
        _inputData.buttons = default;

        // Get raw input first
        float rawHorizontal = Input.GetAxisRaw("Horizontal");
        float rawVertical = Input.GetAxisRaw("Vertical");

        // Apply deadzone ONCE and store result
        float processedHorizontal;
        if (Mathf.Abs(rawHorizontal) < deadZone)
        {
            processedHorizontal = 0f;
            if (enableDebugLogs && rawHorizontal != 0)
                Debug.Log($"[InputManager] Horizontal input {rawHorizontal} zeroed by deadzone {deadZone}");
        }
        else
        {
            processedHorizontal = rawHorizontal;
            if (enableDebugLogs)
                Debug.Log($"[InputManager] Horizontal input accepted: {processedHorizontal}");
        }

        // Set the final input data
        _inputData.horizontal = processedHorizontal;

        // Handle button inputs
        var buttons = _inputData.buttons;

        // Movement/Jump buttons
        buttons = buttons.Set(_GAME.Scripts.Core.NetworkButtons.Jump, Input.GetKey(KeyCode.Space));

        // COMBAT SYSTEM - Handle attack inputs
        bool attackPressed = Input.GetKeyDown(attackKey);
        buttons = buttons.Set(_GAME.Scripts.Core.NetworkButtons.Attack, attackPressed);

        // Determine attack input type when attack is pressed
        if (attackPressed)
        {
            var attackType = _inputData.GetAttackInputType(processedHorizontal, rawVertical);
            _inputData.SetAttackInput(attackType, runner.Tick);

            if (enableDebugLogs)
                Debug.Log($"[InputManager] Attack input: {_inputData.attackInputType} at tick {runner.Tick}");
        }
        else
        {
            _inputData.attackInputType = _GAME.Scripts.Combat.AttackInputType.None;
            _inputData.attackInputConsumed = false;
        }

        // Future combat inputs
        buttons = buttons.Set(_GAME.Scripts.Core.NetworkButtons.Special, Input.GetKey(specialKey));
        buttons = buttons.Set(_GAME.Scripts.Core.NetworkButtons.Dodge, Input.GetKey(dodgeKey));

        _inputData.buttons = buttons;

        // Send the processed input
        input.Set(_inputData);

        if (enableDebugLogs && (_inputData.horizontal != 0 || attackPressed))
        {
            Debug.Log($"[InputManager] Sending network input - Move: {_inputData.horizontal}, Attack: {_inputData.attackInputType}");
        }
    }

    #region INetworkRunnerCallbacks - Required Empty Implementations

    public void OnObjectExitAOI(NetworkRunner                runner, NetworkObject  obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner               runner, NetworkObject  obj, PlayerRef player) { }
    public void OnPlayerJoined(NetworkRunner                 runner, PlayerRef      player)                     { }
    public void OnPlayerLeft(NetworkRunner                   runner, PlayerRef      player)                     { }
    public void OnInputMissing(NetworkRunner                 runner, PlayerRef      player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner                     runner, ShutdownReason shutdownReason) { }
    public void OnConnectedToServer(NetworkRunner            runner)                                                                                        { }
    public void OnDisconnectedFromServer(NetworkRunner       runner, NetDisconnectReason                      reason)                                       { }
    public void OnConnectRequest(NetworkRunner               runner, NetworkRunnerCallbackArgs.ConnectRequest request,       byte[]                 token)  { }
    public void OnConnectFailed(NetworkRunner                runner, NetAddress                               remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner        runner, SimulationMessagePtr                     message)                                              { }
    public void OnSessionListUpdated(NetworkRunner           runner, List<SessionInfo>                        sessionList)                                          { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object>               data)                                                 { }
    public void OnHostMigration(NetworkRunner                runner, HostMigrationToken                       hostMigrationToken)                                   { }
    public void OnReliableDataReceived(NetworkRunner         runner, PlayerRef                                player, ReliableKey key, ArraySegment<byte> data)     { }
    public void OnReliableDataProgress(NetworkRunner         runner, PlayerRef                                player, ReliableKey key, float              progress) { }
    public void OnSceneLoadDone(NetworkRunner                runner) { }
    public void OnSceneLoadStart(NetworkRunner               runner) { }

    #endregion
}