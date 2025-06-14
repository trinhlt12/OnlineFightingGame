using UnityEngine;

public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T _instance;
    private static bool _shuttingDown = false;
    private static readonly object _lock = new object();

    public static T Instance
    {
        get
        {
            if (_shuttingDown)
            {
                Debug.LogWarning($"[Singleton] Instance '{typeof(T)}' already destroyed. Returning null.");
                return null;
            }

            lock (_lock)
            {
                if (_instance == null)
                {
                    _instance = (T)FindObjectOfType(typeof(T));

                    if (_instance == null)
                    {
                        var singletonObject = new GameObject();
                        _instance = singletonObject.AddComponent<T>();
                        singletonObject.name = $"{typeof(T)} (Singleton)";
                    }

                    DontDestroyOnLoad(_instance.gameObject);
                }

                return _instance;
            }
        }
    }

    protected virtual void Awake()
    {
        if (_instance == null)
        {
            _instance = this as T;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    protected virtual void OnApplicationQuit()
    {
        _shuttingDown = true;
    }

    protected virtual void OnDestroy()
    {
        if (_instance == this)
        {
            _shuttingDown = true;
        }
    }
}