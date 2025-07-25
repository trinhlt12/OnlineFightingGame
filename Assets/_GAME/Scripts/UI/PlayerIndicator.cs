// File: Assets/_GAME/Scripts/UI/PlayerIndicator.cs
using UnityEngine;
using Fusion;
using TMPro;

public class PlayerIndicator : NetworkBehaviour
{
    [Header("Indicator Settings")]
    [SerializeField] private GameObject indicatorRoot;
    [SerializeField] private TextMeshProUGUI indicatorText;
    [SerializeField] private RectTransform indicatorTransform;
    [SerializeField] private float heightOffset = 2.0f;
    [SerializeField] private bool enableDebugLogs = false;

    [Header("Visual Configuration")]
    [SerializeField] private string playerText = "YOU";
    [SerializeField] private Color playerColor = Color.cyan;
    [SerializeField] private bool useAnimation = true;
    [SerializeField] private float bobSpeed = 2.0f;
    [SerializeField] private float bobAmount = 0.2f;

    private Camera _mainCamera;
    private Canvas _worldCanvas;
    private Vector3 _originalPosition;
    private bool _isLocalPlayer = false;

    private void Awake()
    {
        // Setup indicator UI
        SetupIndicatorUI();
    }

    public override void Spawned()
    {
        // Check if this is the local player's character
        _isLocalPlayer = Object.HasInputAuthority;

        if (enableDebugLogs)
            Debug.Log($"[PlayerIndicator] Spawned - IsLocalPlayer: {_isLocalPlayer}, InputAuthority: {Object.InputAuthority}");

        // Show indicator only for local player
        SetIndicatorVisibility(_isLocalPlayer);

        // Get main camera reference
        _mainCamera = Camera.main;
        if (_mainCamera == null)
            _mainCamera = FindObjectOfType<Camera>();
    }

    private void SetupIndicatorUI()
    {
        // If no indicator root assigned, create one
        if (indicatorRoot == null)
        {
            CreateDefaultIndicator();
        }

        // Store original position for animation
        if (indicatorTransform != null)
        {
            _originalPosition = indicatorTransform.localPosition;
        }

        // Setup text if available
        if (indicatorText != null)
        {
            indicatorText.text = playerText;
            indicatorText.color = playerColor;
        }
    }

    private void CreateDefaultIndicator()
    {
        // Create World Space Canvas
        GameObject canvasGO = new GameObject("PlayerIndicatorCanvas");
        canvasGO.transform.SetParent(transform);

        _worldCanvas = canvasGO.AddComponent<Canvas>();
        _worldCanvas.renderMode = RenderMode.WorldSpace;
        _worldCanvas.worldCamera = _mainCamera;

        // Add Canvas Scaler for proper scaling
        var canvasScaler = canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasScaler.dynamicPixelsPerUnit = 100;

        // Position canvas above character
        canvasGO.transform.localPosition = Vector3.up * heightOffset;
        canvasGO.transform.localScale = Vector3.one * 0.01f; // Scale down for world space

        // Create indicator root
        GameObject rootGO = new GameObject("IndicatorRoot");
        rootGO.transform.SetParent(canvasGO.transform);

        indicatorRoot = rootGO;
        indicatorTransform = rootGO.AddComponent<RectTransform>();
        indicatorTransform.localPosition = Vector3.zero;

        // Create text component
        GameObject textGO = new GameObject("IndicatorText");
        textGO.transform.SetParent(rootGO.transform);

        indicatorText = textGO.AddComponent<TextMeshProUGUI>();
        indicatorText.text = playerText;
        indicatorText.color = playerColor;
        indicatorText.fontSize = 100; // Large font for world space
        indicatorText.alignment = TextAlignmentOptions.Center;

        var textRect = textGO.GetComponent<RectTransform>();
        textRect.sizeDelta = new Vector2(200, 100);
        textRect.localPosition = Vector3.zero;

        _originalPosition = indicatorTransform.localPosition;

        if (enableDebugLogs)
            Debug.Log("[PlayerIndicator] Created default indicator UI");
    }

    private void SetIndicatorVisibility(bool visible)
    {
        if (indicatorRoot != null)
        {
            indicatorRoot.SetActive(visible);

            if (enableDebugLogs)
                Debug.Log($"[PlayerIndicator] Set indicator visibility: {visible}");
        }
    }

    private void Update()
    {
        // Only update for local player
        if (!_isLocalPlayer || indicatorTransform == null) return;

        // Update camera reference if needed
        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
            if (_worldCanvas != null)
                _worldCanvas.worldCamera = _mainCamera;
        }

        // Face camera
        FaceCamera();

        // Animate indicator
        if (useAnimation)
        {
            AnimateIndicator();
        }
    }

    private void FaceCamera()
    {
        if (_mainCamera == null || _worldCanvas == null) return;

        // Make indicator always face the camera
        Vector3 directionToCamera = _mainCamera.transform.position - _worldCanvas.transform.position;
        _worldCanvas.transform.LookAt(_worldCanvas.transform.position + directionToCamera);
    }

    private void AnimateIndicator()
    {
        // Simple bob animation
        float bobOffset = Mathf.Sin(Time.time * bobSpeed) * bobAmount;
        Vector3 newPosition = _originalPosition + Vector3.up * bobOffset;
        indicatorTransform.localPosition = newPosition;
    }

    /// <summary>
    /// Update indicator when player authority changes
    /// </summary>
    public override void FixedUpdateNetwork()
    {
        // Check if authority changed
        bool currentIsLocal = Object.HasInputAuthority;
        if (currentIsLocal != _isLocalPlayer)
        {
            _isLocalPlayer = currentIsLocal;
            SetIndicatorVisibility(_isLocalPlayer);

            if (enableDebugLogs)
                Debug.Log($"[PlayerIndicator] Authority changed - IsLocalPlayer: {_isLocalPlayer}");
        }
    }

    /// <summary>
    /// Manually set indicator visibility (for testing)
    /// </summary>
    public void SetIndicatorVisible(bool visible)
    {
        SetIndicatorVisibility(visible);
    }

    /// <summary>
    /// Update indicator text and color
    /// </summary>
    public void UpdateIndicator(string text, Color color)
    {
        playerText = text;
        playerColor = color;

        if (indicatorText != null)
        {
            indicatorText.text = text;
            indicatorText.color = color;
        }
    }

    private void OnDestroy()
    {
        if (_worldCanvas != null && Application.isPlaying)
        {
            Destroy(_worldCanvas.gameObject);
        }
    }
}