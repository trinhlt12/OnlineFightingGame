// File: Assets/_GAME/Scripts/Core/MapManager.cs
using UnityEngine;
using Fusion;

public class MapManager : NetworkBehaviour
{
    [Header("Map Configuration")]
    [SerializeField] private MapData[] availableMaps;
    [SerializeField] private Transform mapRoot;
    [SerializeField] private bool enableDebugLogs = true;

    [Networked] public int CurrentMapIndex { get; set; } = -1;

    private GameObject _currentMapInstance;
    private static MapManager _instance;
    public static MapManager Instance => _instance;

    public MapData CurrentMapData => CurrentMapIndex >= 0 && CurrentMapIndex < availableMaps.Length
        ? availableMaps[CurrentMapIndex] : null;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // Ensure MapRoot exists
        if (mapRoot == null)
        {
            var mapRootGO = GameObject.Find("MapRoot");
            if (mapRootGO == null)
            {
                mapRootGO = new GameObject("MapRoot");
                mapRoot = mapRootGO.transform;
            }
            else
            {
                mapRoot = mapRootGO.transform;
            }
        }
    }

    public override void Spawned()
    {
        if (enableDebugLogs)
            Debug.Log($"[MapManager] Spawned - HasStateAuthority: {HasStateAuthority}");
    }

    /// <summary>
    /// Random select and spawn a map immediately (Server only)
    /// </summary>
    public void RandomSelectAndSpawnMap()
    {
        if (!HasStateAuthority)
        {
            Debug.LogWarning("[MapManager] RandomSelectAndSpawnMap called on client - ignoring");
            return;
        }

        if (availableMaps == null || availableMaps.Length == 0)
        {
            Debug.LogError("[MapManager] No available maps configured!");
            return;
        }

        // Random select map
        int randomMapIndex = Random.Range(0, availableMaps.Length);

        if (enableDebugLogs)
            Debug.Log($"[MapManager] Randomly selected map: {availableMaps[randomMapIndex].mapName} (Index: {randomMapIndex})");

        // Set networked property and spawn immediately on server
        CurrentMapIndex = randomMapIndex;
        SpawnMapLocal(randomMapIndex);

        // Send RPC to all clients to spawn immediately
        RPC_SpawnMapOnClients(randomMapIndex);
    }

    /// <summary>
    /// RPC to spawn map on all clients immediately
    /// </summary>
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SpawnMapOnClients(int mapIndex)
    {
        if (enableDebugLogs)
            Debug.Log($"[MapManager] RPC received - spawning map index: {mapIndex}");

        // Don't spawn again on server since it already spawned locally
        if (!HasStateAuthority)
        {
            SpawnMapLocal(mapIndex);
        }

        // Update CurrentMapIndex for consistency
        CurrentMapIndex = mapIndex;
    }

    /// <summary>
    /// Spawn specific map locally (immediate spawn)
    /// </summary>
    private void SpawnMapLocal(int mapIndex)
    {
        if (mapIndex < 0 || mapIndex >= availableMaps.Length)
        {
            Debug.LogError($"[MapManager] Invalid map index: {mapIndex}");
            return;
        }

        var mapData = availableMaps[mapIndex];
        if (mapData.mapPrefab == null)
        {
            Debug.LogError($"[MapManager] Map prefab is null for {mapData.mapName}");
            return;
        }

        // Cleanup old map first
        CleanupCurrentMap();

        // Spawn new map immediately
        _currentMapInstance = Instantiate(mapData.mapPrefab, mapRoot);
        _currentMapInstance.transform.localPosition = mapData.spawnOffset;

        if (enableDebugLogs)
            Debug.Log($"[MapManager] Successfully spawned map immediately: {mapData.mapName}");
    }

    /// <summary>
    /// Cleanup current map instance
    /// </summary>
    /// <summary>
    /// Cleanup current map instance
    /// </summary>
    private void CleanupCurrentMap()
    {
        if (_currentMapInstance != null)
        {
            if (enableDebugLogs)
                Debug.Log("[MapManager] Cleaning up previous map instance");

            // Check if we're in play mode or editor mode
            if (Application.isPlaying)
            {
                Destroy(_currentMapInstance);
            }
            else
            {
                // In editor mode, use DestroyImmediate
                DestroyImmediate(_currentMapInstance);
            }

            _currentMapInstance = null;
        }

        // Also cleanup any remaining children in MapRoot
        if (mapRoot != null)
        {
            // Convert to array to avoid modification during iteration
            var childrenToDestroy = new Transform[mapRoot.childCount];
            for (int i = 0; i < mapRoot.childCount; i++)
            {
                childrenToDestroy[i] = mapRoot.GetChild(i);
            }

            foreach (Transform child in childrenToDestroy)
            {
                if (child != null)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(child.gameObject);
                    }
                    else
                    {
                        DestroyImmediate(child.gameObject);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Fallback network sync (in case RPC fails)
    /// </summary>
    public override void FixedUpdateNetwork()
    {
        // Fallback: sync map on clients if somehow they missed the RPC
        if (!HasStateAuthority && CurrentMapIndex >= 0 && _currentMapInstance == null)
        {
            if (enableDebugLogs)
                Debug.Log("[MapManager] Fallback spawning via FixedUpdateNetwork");

            SpawnMapLocal(CurrentMapIndex);
        }
    }

    public MapData[] GetAvailableMaps() => availableMaps;

    /// <summary>
    /// Get current spawned map instance
    /// </summary>
    public GameObject GetCurrentMapInstance() => _currentMapInstance;

    /// <summary>
    /// Check if map is spawned and ready
    /// </summary>
    public bool IsMapReady() => _currentMapInstance != null && CurrentMapIndex >= 0;

    private void OnDestroy()
    {
        // Only cleanup if we're in play mode to avoid asset destruction warnings
        if (Application.isPlaying)
        {
            CleanupCurrentMap();
        }

        if (_instance == this)
            _instance = null;
    }
}