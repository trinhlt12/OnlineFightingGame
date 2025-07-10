namespace _GAME.Scripts
{
    using UnityEngine;

    public class AudioListenerCleaner : MonoBehaviour
    {
        private void Awake()
        {
            var listeners = FindObjectsOfType<AudioListener>();

            if (listeners.Length > 1)
            {
                Debug.LogWarning($"[AudioListenerCleaner] Found {listeners.Length} AudioListeners. Removing extras...");

                for (int i = 1; i < listeners.Length; i++)
                {
                    Destroy(listeners[i]);
                }
            }
        }
    }

}