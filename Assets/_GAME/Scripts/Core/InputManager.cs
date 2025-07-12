using UnityEngine;
using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using _GAME.Scripts.Core;

/// <summary>
/// Fixed input manager - resolves deadzone bug
/// </summary>
public class InputManager : MonoBehaviour, INetworkRunnerCallbacks
{
    [Header("Settings")] [SerializeField] private float deadZone = 0.1f;

    [Header("Debug")] [SerializeField] private bool enableDebugLogs = false;

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

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        // Store the previous frame's button states before updating
        _inputData.previousButtons = _inputData.buttons;

        // Reset the current frame's buttons
        _inputData.buttons = default;

        // Get raw input first
        float rawHorizontal = Input.GetAxisRaw("Horizontal");

        // Apply deadzone ONCE and store result
        float processedInput;
        if (Mathf.Abs(rawHorizontal) < deadZone)
        {
            processedInput = 0f;
            if (enableDebugLogs && rawHorizontal != 0) Debug.Log($"[FixedInputManager] Input {rawHorizontal} zeroed by deadzone {deadZone}");
        }
        else
        {
            processedInput = rawHorizontal;
            if (enableDebugLogs) Debug.Log($"[FixedInputManager] Input accepted: {processedInput}");
        }

        // Set the final input data
        _inputData.horizontal = processedInput;

        // Handle jump input - use fully qualified name
        var buttons = _inputData.buttons;
        buttons            = buttons.Set(_GAME.Scripts.Core.NetworkButtons.Jump, Input.GetKey(KeyCode.Space));
        _inputData.buttons = buttons;

        // Send the processed input
        input.Set(_inputData);

        if (enableDebugLogs && _inputData.horizontal != 0)
        {
            Debug.Log($"[FixedInputManager] Sending network input: {_inputData.horizontal}");
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