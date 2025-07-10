using System.Collections.Generic;
using UI;
using UnityEngine;

public class UIManager : Singleton<UIManager>
{
    [Header("Register your UI Canvas Prefabs here")]
    [SerializeField] private List<UICanvas> uiPrefabs;

    private readonly Dictionary<System.Type, UICanvas> _uiInstances = new();

    protected override void Awake()
    {
        base.Awake();

        // Initialize prefabs that are assigned in inspector
        foreach (var prefab in uiPrefabs)
        {
            if (prefab == null) continue;

            var type = prefab.GetType();
            if (_uiInstances.ContainsKey(type)) continue;

            UICanvas instance = Instantiate(prefab, transform);
            instance.Hide(); // start hidden
            _uiInstances[type] = instance;
        }
    }

    /// <summary>
    /// Shows an existing UI canvas of type T
    /// </summary>
    public T Show<T>() where T : UICanvas
    {
        T panel = Get<T>();
        panel?.Show();
        return panel;
    }

    /// <summary>
    /// Hides an existing UI canvas of type T
    /// </summary>
    public void Hide<T>() where T : UICanvas
    {
        T panel = Get<T>();
        panel?.Hide();
    }

    /// <summary>
    /// Gets an existing UI canvas of type T
    /// </summary>
    public T Get<T>() where T : UICanvas
    {
        var type = typeof(T);
        if (_uiInstances.TryGetValue(type, out UICanvas canvas))
        {
            return canvas as T;
        }

        Debug.LogWarning($"[UIManager] No panel of type {type} found.");
        return null;
    }

    /// <summary>
    /// Loads a UI canvas from Resources and shows it immediately
    /// This is useful for UI that should be loaded dynamically
    /// </summary>
    public T LoadAndShow<T>(string resourcePath) where T : UICanvas
    {
        var type = typeof(T);

        // Check if we already have this UI loaded
        if (_uiInstances.TryGetValue(type, out UICanvas existingCanvas))
        {
            Debug.Log($"[UIManager] UI of type {type} already exists, showing existing instance");
            existingCanvas.Show();
            return existingCanvas as T;
        }

        // Try to load from Resources
        Debug.Log($"[UIManager] Loading UI from Resources: {resourcePath}");
        var prefab = Resources.Load<GameObject>(resourcePath);

        if (prefab == null)
        {
            Debug.LogError($"[UIManager] Failed to load UI prefab from Resources at path: {resourcePath}");
            return null;
        }

        // Check if the prefab has the correct component
        var canvasComponent = prefab.GetComponent<T>();
        if (canvasComponent == null)
        {
            Debug.LogError($"[UIManager] Prefab at {resourcePath} does not have component of type {type}");
            return null;
        }

        // Instantiate and setup
        var instance = Instantiate(prefab, transform);
        var canvasInstance = instance.GetComponent<T>();

        if (canvasInstance != null)
        {
            _uiInstances[type] = canvasInstance;
            canvasInstance.Show();
            Debug.Log($"[UIManager] Successfully loaded and showed UI: {type}");
            return canvasInstance;
        }
        else
        {
            Debug.LogError($"[UIManager] Failed to get component {type} from instantiated prefab");
            Destroy(instance);
            return null;
        }
    }

    /// <summary>
    /// Loads a UI canvas from Resources without showing it
    /// </summary>
    public T LoadFromResources<T>(string resourcePath) where T : UICanvas
    {
        var type = typeof(T);

        // Check if we already have this UI loaded
        if (_uiInstances.TryGetValue(type, out UICanvas existingCanvas))
        {
            Debug.Log($"[UIManager] UI of type {type} already exists");
            return existingCanvas as T;
        }

        // Try to load from Resources
        var prefab = Resources.Load<GameObject>(resourcePath);

        if (prefab == null)
        {
            Debug.LogError($"[UIManager] Failed to load UI prefab from Resources at path: {resourcePath}");
            return null;
        }

        var canvasComponent = prefab.GetComponent<T>();
        if (canvasComponent == null)
        {
            Debug.LogError($"[UIManager] Prefab at {resourcePath} does not have component of type {type}");
            return null;
        }

        // Instantiate but keep hidden
        var instance = Instantiate(prefab, transform);
        var canvasInstance = instance.GetComponent<T>();

        if (canvasInstance != null)
        {
            _uiInstances[type] = canvasInstance;
            canvasInstance.Hide(); // Keep hidden initially
            Debug.Log($"[UIManager] Successfully loaded UI: {type}");
            return canvasInstance;
        }
        else
        {
            Debug.LogError($"[UIManager] Failed to get component {type} from instantiated prefab");
            Destroy(instance);
            return null;
        }
    }

    /// <summary>
    /// Removes a UI canvas from the manager and destroys it
    /// </summary>
    public void Unload<T>() where T : UICanvas
    {
        var type = typeof(T);
        if (_uiInstances.TryGetValue(type, out UICanvas canvas))
        {
            _uiInstances.Remove(type);
            if (canvas != null)
            {
                Destroy(canvas.gameObject);
            }
            Debug.Log($"[UIManager] Unloaded UI: {type}");
        }
    }

    /// <summary>
    /// Checks if a UI canvas of type T is currently loaded
    /// </summary>
    public bool IsLoaded<T>() where T : UICanvas
    {
        var type = typeof(T);
        return _uiInstances.ContainsKey(type) && _uiInstances[type] != null;
    }

    /// <summary>
    /// Registers an existing UI canvas with the manager
    /// Useful for UI that exists in scene but needs to be managed by UIManager
    /// </summary>
    public void RegisterExistingUI<T>(T canvas) where T : UICanvas
    {
        var type = typeof(T);

        if (_uiInstances.ContainsKey(type))
        {
            Debug.LogWarning($"[UIManager] UI of type {type} is already registered. Replacing with new instance.");
        }

        _uiInstances[type] = canvas;
        Debug.Log($"[UIManager] Registered existing UI: {type}");
    }

    /// <summary>
    /// Unregisters a UI canvas from the manager without destroying it
    /// </summary>
    public void UnregisterUI<T>() where T : UICanvas
    {
        var type = typeof(T);
        if (_uiInstances.ContainsKey(type))
        {
            _uiInstances.Remove(type);
            Debug.Log($"[UIManager] Unregistered UI: {type}");
        }
    }
}