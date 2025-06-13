using Fusion;
using Fusion.Menu;
using Fusion.Photon.Realtime;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MultiClimb.Menu
{
    /// <summary>
    /// Handles menu connection logic for a multiplayer game using Fusion networking.
    /// Implements the IFusionMenuConnection interface.
    /// </summary>
    public class MenuConnection : IFusionMenuConnection
    {
        /// <summary>
        /// Initializes a new instance of the MenuConnection class.
        /// </summary>
        /// <param name="config">Configuration for the Fusion menu.</param>
        /// <param name="runnerPrefab">Prefab for the NetworkRunner.</param>
        public MenuConnection(IFusionMenuConfig config, NetworkRunner runnerPrefab)
        {
            _config = config;
            _runnerPrefab = runnerPrefab;
        }

        /// <summary>
        /// Gets the name of the current session.
        /// </summary>
        public string SessionName { get; private set; }

        /// <summary>
        /// Gets the maximum number of players allowed in the session.
        /// </summary>
        public int MaxPlayerCount { get; private set; }

        /// <summary>
        /// Gets the region of the session.
        /// </summary>
        public string Region { get; private set; }

        /// <summary>
        /// Gets the application version used for the session.
        /// </summary>
        public string AppVersion { get; private set; }

        /// <summary>
        /// Gets the list of usernames in the session.
        /// </summary>
        public List<string> Usernames { get; private set; }

        /// <summary>
        /// Indicates whether the connection is active.
        /// </summary>
        public bool IsConnected => _runner && _runner.IsRunning;

        /// <summary>
        /// Gets the current ping in milliseconds.
        /// </summary>
        public int Ping => (int)(IsConnected ? _runner.GetPlayerRtt(_runner.LocalPlayer) * 1000 : 0);

        private NetworkRunner _runnerPrefab;
        private NetworkRunner _runner;
        private IFusionMenuConfig _config;
        private bool _connectingSafeCheck;
        private CancellationTokenSource _cancellationTokenSource;
        private CancellationToken _cancellationToken;

        /// <summary>
        /// Connects to a session asynchronously.
        /// </summary>
        /// <param name="connectArgs">Arguments for connecting to the session.</param>
        /// <returns>A task that resolves to the connection result.</returns>
        public async Task<ConnectResult> ConnectAsync(IFusionMenuConnectArgs connectArgs)
        {
            // Safety check to prevent multiple connections
            if (_connectingSafeCheck) return new ConnectResult() { CustomResultHandling = true, Success = false, FailReason = ConnectFailReason.None };

            _connectingSafeCheck = true;
            if (_runner && _runner.IsRunning)
            {
                await _runner.Shutdown();
            }

            // Create and prepare Runner object
            _runner = CreateRunner();
            var sceneManager = _runner.gameObject.AddComponent<NetworkSceneManagerDefault>();
            sceneManager.IsSceneTakeOverEnabled = false;

            // Copy and update AppSettings
            var appSettings = CopyAppSettings(connectArgs);

            // Solve StartGameArgs
            var args = new StartGameArgs();
            args.CustomPhotonAppSettings = appSettings;
            args.GameMode = ResolveGameMode(connectArgs);
            args.SessionName = SessionName = connectArgs.Session;
            args.PlayerCount = MaxPlayerCount = connectArgs.MaxPlayerCount;

            // Scene info
            var sceneInfo = new NetworkSceneInfo();
            sceneInfo.AddSceneRef(sceneManager.GetSceneRef(connectArgs.Scene.ScenePath), LoadSceneMode.Additive);
            args.Scene = sceneInfo;

            // Cancellation Token
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();
            _cancellationToken = _cancellationTokenSource.Token;
            args.StartGameCancellationToken = _cancellationToken;

            var regionIndex = _config.AvailableRegions.IndexOf(connectArgs.Region);
            args.SessionNameGenerator = () => _config.CodeGenerator.EncodeRegion(_config.CodeGenerator.Create(), regionIndex);
            var startGameResult = default(StartGameResult);
            var connectResult = new ConnectResult();
            startGameResult = await _runner.StartGame(args);

            connectResult.Success = startGameResult.Ok;
            connectResult.FailReason = ResolveConnectFailReason(startGameResult.ShutdownReason);
            _connectingSafeCheck = false;

            if (connectResult.Success)
            {
                SessionName = _runner.SessionInfo.Name;
            }

            return connectResult;
        }

        /// <summary>
        /// Disconnects from the session asynchronously.
        /// </summary>
        /// <param name="reason">Reason for disconnecting.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task DisconnectAsync(int reason)
        {
            var peerMode = _runner.Config?.PeerMode;
            _cancellationTokenSource.Cancel();
            await _runner.Shutdown(shutdownReason: ResolveShutdownReason(reason));

            if (peerMode is NetworkProjectConfig.PeerModes.Multiple) return;

            for (int i = SceneManager.sceneCount - 1; i > 0; i--)
            {
                SceneManager.UnloadSceneAsync(SceneManager.GetSceneAt(i));
            }
        }

        /// <summary>
        /// Requests available online regions asynchronously.
        /// </summary>
        /// <param name="connectArgs">Arguments for the connection.</param>
        /// <returns>A task that resolves to a list of available regions.</returns>
        public Task<List<FusionMenuOnlineRegion>> RequestAvailableOnlineRegionsAsync(IFusionMenuConnectArgs connectArgs)
        {
            // Force best region
            return Task.FromResult(new List<FusionMenuOnlineRegion>() { new FusionMenuOnlineRegion() { Code = string.Empty, Ping = 0 } });
        }

        /// <summary>
        /// Sets the usernames for the current session.
        /// </summary>
        /// <param name="usernames">List of usernames.</param>
        public void SetSessionUsernames(List<string> usernames)
        {
            Usernames = usernames;
        }

        /// <summary>
        /// Resolves the game mode based on connection arguments.
        /// </summary>
        /// <param name="args">Connection arguments.</param>
        /// <returns>The resolved game mode.</returns>
        private GameMode ResolveGameMode(IFusionMenuConnectArgs args)
        {
            bool isSharedSession = args.Scene.SceneName.Contains("Shared");
            if (args.Creating)
            {
                // Create session
                return isSharedSession ? GameMode.Shared : GameMode.Host;
            }

            if (string.IsNullOrEmpty(args.Session))
            {
                // QuickJoin
                return isSharedSession ? GameMode.Shared : GameMode.AutoHostOrClient;
            }

            // Join session
            return isSharedSession ? GameMode.Shared : GameMode.Client;
        }

        /// <summary>
        /// Resolves the shutdown reason based on the given reason code.
        /// </summary>
        /// <param name="reason">Reason code.</param>
        /// <returns>The resolved shutdown reason.</returns>
        private ShutdownReason ResolveShutdownReason(int reason)
        {
            switch (reason)
            {
                case ConnectFailReason.UserRequest:
                    return ShutdownReason.Ok;
                case ConnectFailReason.ApplicationQuit:
                    return ShutdownReason.Ok;
                case ConnectFailReason.Disconnect:
                    return ShutdownReason.DisconnectedByPluginLogic;
                default:
                    return ShutdownReason.Error;
            }
        }

        /// <summary>
        /// Resolves the connection failure reason based on the shutdown reason.
        /// </summary>
        /// <param name="reason">Shutdown reason.</param>
        /// <returns>The resolved connection failure reason.</returns>
        private int ResolveConnectFailReason(ShutdownReason reason)
        {
            switch (reason)
            {
                case ShutdownReason.Ok:
                case ShutdownReason.OperationCanceled:
                    return ConnectFailReason.UserRequest;
                case ShutdownReason.DisconnectedByPluginLogic:
                case ShutdownReason.Error:
                    return ConnectFailReason.Disconnect;
                default:
                    return ConnectFailReason.None;
            }
        }

        /// <summary>
        /// Creates a new NetworkRunner instance.
        /// </summary>
        /// <returns>The created NetworkRunner instance.</returns>
        private NetworkRunner CreateRunner()
        {
            return _runnerPrefab ? UnityEngine.Object.Instantiate(_runnerPrefab) : new GameObject("NetworkRunner", typeof(NetworkRunner)).GetComponent<NetworkRunner>();
        }

        /// <summary>
        /// Copies application settings and updates them based on connection arguments.
        /// </summary>
        /// <param name="connectArgs">Connection arguments.</param>
        /// <returns>The copied and updated application settings.</returns>
        private FusionAppSettings CopyAppSettings(IFusionMenuConnectArgs connectArgs)
        {
            FusionAppSettings appSettings = new FusionAppSettings();
            PhotonAppSettings.Global.AppSettings.CopyTo(appSettings);
            appSettings.FixedRegion = Region = connectArgs.Region;
            appSettings.AppVersion = AppVersion = connectArgs.AppVersion;
            return appSettings;
        }
    }
}