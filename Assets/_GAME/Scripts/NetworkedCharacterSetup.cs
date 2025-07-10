using Fusion;
using UnityEngine;
using _GAME.Scripts.Data;

/// <summary>
/// This component should be attached to character prefabs to ensure proper network setup
/// It handles the connection between CharacterData and the actual networked character
/// </summary>
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkTransform))]
public class NetworkedCharacterSetup : NetworkBehaviour
{
    [Header("Character Configuration")]
    [SerializeField] private bool enableDebugLogs = true;

    // Store character data reference for network synchronization
    [Networked] public int CharacterDataID { get; set; }

    // Network position for explicit synchronization
    [Networked] public Vector3 NetworkPosition { get; set; }
    [Networked] public Quaternion NetworkRotation { get; set; }

    // Cache the character data locally for performance
    private CharacterData _characterData;
    private NetworkTransform _networkTransform;

    public CharacterData CharacterData => _characterData;

    public override void Spawned()
    {
        // Get NetworkTransform component for position synchronization
        _networkTransform = GetComponent<NetworkTransform>();

        if (enableDebugLogs)
            Debug.Log($"[NetworkedCharacterSetup] Character spawned - HasInputAuthority: {Object.HasInputAuthority}, InputAuthority: {Object.InputAuthority}, Position: {transform.position}");

        // Initialize character data if we have a valid ID
        if (CharacterDataID != 0)
        {
            LoadCharacterData();
        }

        // Ensure proper position synchronization
        if (HasStateAuthority)
        {
            // Server sets the network position based on actual spawn position
            NetworkPosition = transform.position;
            NetworkRotation = transform.rotation;

            if (enableDebugLogs)
                Debug.Log($"[NetworkedCharacterSetup] Server set network position: {NetworkPosition}");
        }
        else
        {
            // Client applies the network position received from server
            StartCoroutine(ApplyNetworkPositionWhenReady());
        }
    }

    /// <summary>
    /// Coroutine to apply network position on clients after network data is received
    /// </summary>
    private System.Collections.IEnumerator ApplyNetworkPositionWhenReady()
    {
        // Wait a frame to ensure network data is synchronized
        yield return new WaitForEndOfFrame();

        // Apply network position if it's different from current position
        if (Vector3.Distance(transform.position, NetworkPosition) > 0.1f)
        {
            transform.position = NetworkPosition;
            transform.rotation = NetworkRotation;

            if (enableDebugLogs)
                Debug.Log($"[NetworkedCharacterSetup] Client applied network position: {NetworkPosition}");
        }
    }

    /// <summary>
    /// Sets the character data ID and position for network synchronization
    /// Should be called immediately after spawning
    /// </summary>
    public void SetCharacterData(CharacterData characterData, Vector3 spawnPosition, Quaternion spawnRotation)
    {
        if (characterData == null)
        {
            Debug.LogError("[NetworkedCharacterSetup] Attempted to set null character data");
            return;
        }

        // Only the server should set this data
        if (HasStateAuthority)
        {
            CharacterDataID = characterData.CharacterID;
            _characterData = characterData;

            // Set position explicitly for network synchronization
            NetworkPosition = spawnPosition;
            NetworkRotation = spawnRotation;
            transform.position = spawnPosition;
            transform.rotation = spawnRotation;

            if (enableDebugLogs)
                Debug.Log($"[NetworkedCharacterSetup] Set character data: {characterData.CharacterName} (ID: {CharacterDataID}) at position {spawnPosition}");

            // Apply character data to the networked object
            ApplyCharacterData();
        }
    }

    /// <summary>
    /// Sets the character data ID for network synchronization
    /// Should be called immediately after spawning
    /// </summary>
    public void SetCharacterData(CharacterData characterData)
    {
        SetCharacterData(characterData, transform.position, transform.rotation);
    }

    /// <summary>
    /// Loads character data from the ID (useful for clients receiving the networked data)
    /// </summary>
    private void LoadCharacterData()
    {
        // Find the character data that matches our ID
        var allCharacterData = Resources.FindObjectsOfTypeAll<CharacterData>();

        foreach (var characterData in allCharacterData)
        {
            if (characterData.CharacterID == CharacterDataID)
            {
                _characterData = characterData;

                if (enableDebugLogs)
                    Debug.Log($"[NetworkedCharacterSetup] Loaded character data: {characterData.CharacterName} (ID: {CharacterDataID})");

                ApplyCharacterData();
                return;
            }
        }

        Debug.LogError($"[NetworkedCharacterSetup] Could not find character data with ID: {CharacterDataID}");
    }

    /// <summary>
    /// Applies character data to the networked object
    /// This is where you would set up character-specific properties
    /// </summary>
    private void ApplyCharacterData()
    {
        if (_characterData == null)
        {
            Debug.LogWarning("[NetworkedCharacterSetup] No character data to apply");
            return;
        }

        // Apply visual elements
        ApplyCharacterVisuals();

        // Apply character stats if needed
        ApplyCharacterStats();

        // Apply any other character-specific setup
        ApplyCharacterSpecialProperties();

        if (enableDebugLogs)
            Debug.Log($"[NetworkedCharacterSetup] Applied all character data for {_characterData.CharacterName}");
    }

    /// <summary>
    /// Applies visual elements from character data
    /// </summary>
    private void ApplyCharacterVisuals()
    {
        // Example: Update character renderer with character colors
        var renderers = GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
        {
            if (renderer.material != null)
            {
                // Apply primary color from character data
                renderer.material.color = _characterData.PrimaryColor;
            }
        }

        // Example: Update UI elements if character has name display
        var nameDisplay = GetComponentInChildren<TextMesh>();
        if (nameDisplay != null)
        {
            nameDisplay.text = _characterData.CharacterName;
        }

        if (enableDebugLogs)
            Debug.Log($"[NetworkedCharacterSetup] Applied visual elements for {_characterData.CharacterName}");
    }

    /// <summary>
    /// Applies character stats to the networked object
    /// </summary>
    private void ApplyCharacterStats()
    {
        if (_characterData.Stats == null)
        {
            Debug.LogWarning($"[NetworkedCharacterSetup] No stats data for {_characterData.CharacterName}");
            return;
        }

        // Example: Apply stats to character controller or other components
        var characterController = GetComponent<CharacterController>();
        if (characterController != null)
        {
            // You might want to modify movement speed based on character stats
            // This is just an example - adjust based on your game's needs
            var speedMultiplier = _characterData.Stats.Speed / 10f; // Normalize to 0-1 range
            // Apply speed multiplier to movement system
        }

        /*// Example: Apply stats to a health system
        var healthSystem = GetComponent<HealthSystem>();
        if (healthSystem != null)
        {
            // Set max health based on character defense stat
            // healthSystem.SetMaxHealth(_characterData.Stats.Defense * 10);
        }*/

        if (enableDebugLogs)
            Debug.Log($"[NetworkedCharacterSetup] Applied character stats for {_characterData.CharacterName}");
    }

    /// <summary>
    /// Applies special properties specific to the character
    /// </summary>
    private void ApplyCharacterSpecialProperties()
    {
        /*// Example: Set up special abilities based on character data
        var abilitySystem = GetComponent<CharacterAbilitySystem>();
        if (abilitySystem != null)
        {
            // Configure special ability based on character data
            // abilitySystem.SetSpecialAbility(_characterData.Stats.SpecialAbilityName);
        }*/

        // Example: Play character-specific sounds
        var audioSource = GetComponent<AudioSource>();
        if (audioSource != null && _characterData.VoiceLine != null)
        {
            audioSource.clip = _characterData.VoiceLine;
            // audioSource.Play(); // Optional: play spawn sound
        }

        if (enableDebugLogs)
            Debug.Log($"[NetworkedCharacterSetup] Applied special properties for {_characterData.CharacterName}");
    }

    /// <summary>
    /// Gets the character name for display purposes
    /// </summary>
    public string GetCharacterName()
    {
        return _characterData != null ? _characterData.CharacterName : "Unknown Character";
    }

    /// <summary>
    /// Gets the character stats for gameplay systems
    /// </summary>
    public CharacterStats GetCharacterStats()
    {
        return _characterData?.Stats;
    }

    /// <summary>
    /// Debug method to log current character state
    /// </summary>
    [ContextMenu("Debug Character State")]
    public void DebugCharacterState()
    {
        if (_characterData != null)
        {
            Debug.Log($"[NetworkedCharacterSetup] === Character State Debug ===");
            Debug.Log($"Character Name: {_characterData.CharacterName}");
            Debug.Log($"Character ID: {CharacterDataID}");
            Debug.Log($"Current Position: {transform.position}");
            Debug.Log($"Network Position: {NetworkPosition}");
            Debug.Log($"HasInputAuthority: {Object.HasInputAuthority}");
            Debug.Log($"InputAuthority: {Object.InputAuthority}");
            Debug.Log($"HasStateAuthority: {HasStateAuthority}");

            if (_characterData.Stats != null)
            {
                Debug.Log($"Stats - Speed: {_characterData.Stats.Speed}, Strength: {_characterData.Stats.Strength}");
            }
        }
        else
        {
            Debug.Log("[NetworkedCharacterSetup] No character data loaded");
        }
    }
}