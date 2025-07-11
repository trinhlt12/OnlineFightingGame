using UnityEngine;
using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using _GAME.Scripts.Core;

/// <summary>
/// Simple input manager - collects and sends input through network
/// </summary>
public class InputManager : MonoBehaviour, INetworkRunnerCallbacks
{
    [Header("Settings")] [SerializeField] private float deadZone = 0.1f;

    private NetworkInputData _inputData;

    private void Update()
    {
        // Collect horizontal input
        _inputData.horizontal = Input.GetAxisRaw("Horizontal");

        // Apply deadzone
        if (Mathf.Abs(_inputData.horizontal) < deadZone) _inputData.horizontal = 0f;
    }

    #region INetworkRunnerCallbacks

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        input.Set(_inputData);
    }

    // Required empty implementations
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

    public void OnPlayerJoined(NetworkRunner      runner, PlayerRef      player)                     { }
    public void OnPlayerLeft(NetworkRunner        runner, PlayerRef      player)                     { }
    public void OnInputMissing(NetworkRunner      runner, PlayerRef      player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner          runner, ShutdownReason shutdownReason) { }
    public void OnConnectedToServer(NetworkRunner runner) { }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }

    public void OnConnectRequest(NetworkRunner               runner, NetworkRunnerCallbackArgs.ConnectRequest request,       byte[]                 token)  { }
    public void OnConnectFailed(NetworkRunner                runner, NetAddress                               remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner        runner, SimulationMessagePtr                     message)            { }
    public void OnSessionListUpdated(NetworkRunner           runner, List<SessionInfo>                        sessionList)        { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object>               data)               { }
    public void OnHostMigration(NetworkRunner                runner, HostMigrationToken                       hostMigrationToken) { }

    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }

    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }

    public void OnSceneLoadDone(NetworkRunner  runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }

    #endregion
}