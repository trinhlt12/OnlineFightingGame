using System.Collections.Generic;
using UI;
using UnityEngine;

public class UIManager : Singleton<UIManager>
{
    [Header("Register your UI Canvas Prefabs here")] [SerializeField] private List<UICanvas> uiPrefabs;

    private readonly Dictionary<System.Type, UICanvas> _uiInstances = new();

    protected override void Awake()
    {
        base.Awake();
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

    public T Show<T>() where T : UICanvas
    {
        T panel = Get<T>();
        panel?.Show();
        return panel;
    }

    public void Hide<T>() where T : UICanvas
    {
        T panel = Get<T>();
        panel?.Hide();
    }

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
}