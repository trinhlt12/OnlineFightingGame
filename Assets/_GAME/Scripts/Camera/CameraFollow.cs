using UnityEngine;
using Fusion;
using _GAME.Scripts.Core;

namespace _GAME.Scripts.Camera
{
    /// <summary>
    /// SINGLE RESPONSIBILITY: Camera following system for local player in networked fighting game
    /// Follows SOLID principles and optimized for network performance
    /// Only follows the player with InputAuthority (local player)
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [Header("Follow Settings")]
        [SerializeField] private float followSpeed = 5f;
        [SerializeField] private float lookAheadDistance = 2f;
        [SerializeField] private float lookAheadSpeed = 3f;

        [Header("Camera Bounds")]
        [SerializeField] private bool useBounds = true;
        [SerializeField] private Vector2 minBounds = new Vector2(-10f, -5f);
        [SerializeField] private Vector2 maxBounds = new Vector2(10f, 5f);

        [Header("Smoothing")]
        [SerializeField] private bool enableVerticalFollow = true;
        [SerializeField] private float verticalOffset = 0f;
        [SerializeField] private float verticalFollowSpeed = 3f;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;

        // ==================== CACHED COMPONENTS ====================
        private UnityEngine.Camera _camera;
        private Transform _cameraTransform;

        // ==================== TARGET TRACKING ====================
        private PlayerController _localPlayer;
        private Transform _targetTransform;
        private bool _hasValidTarget = false;

        // ==================== SMOOTHING VARIABLES ====================
        private Vector3 _targetPosition;
        private Vector3 _velocity = Vector3.zero;
        private float _lookAheadX = 0f;

        // ==================== PROPERTIES ====================
        public bool HasTarget => _hasValidTarget && _targetTransform != null;
        public PlayerController LocalPlayer => _localPlayer;

        // ==================== INITIALIZATION ====================

        private void Awake()
        {
            // Cache camera components
            _camera = GetComponent<UnityEngine.Camera>();
            _cameraTransform = transform;

            if (_camera == null)
            {
                Debug.LogError("[CameraFollow] No Camera component found on this GameObject!");
                enabled = false;
                return;
            }

            if (enableDebugLogs)
                Debug.Log("[CameraFollow] Camera follow system initialized");
        }

        private void Start()
        {
            // Find local player after all network objects are spawned
            FindLocalPlayer();
        }

        // ==================== TARGET MANAGEMENT ====================

        /// <summary>
        /// Finds the local player (player with InputAuthority)
        /// Called automatically on Start and can be called manually if needed
        /// </summary>
        public void FindLocalPlayer()
        {
            // Clear previous target
            ClearTarget();

            // Find all PlayerController instances in the scene
            PlayerController[] players = FindObjectsOfType<PlayerController>();

            if (enableDebugLogs)
                Debug.Log($"[CameraFollow] Found {players.Length} players in scene");

            foreach (PlayerController player in players)
            {
                // Check if this player has input authority (is the local player)
                if (player.Object != null && player.Object.HasInputAuthority)
                {
                    SetTarget(player);

                    if (enableDebugLogs)
                        Debug.Log($"[CameraFollow] Found local player: {player.name}");

                    return;
                }
            }

            if (enableDebugLogs)
                Debug.LogWarning("[CameraFollow] No local player found with InputAuthority");
        }

        /// <summary>
        /// Sets the target player to follow
        /// </summary>
        /// <param name="player">Player controller to follow</param>
        public void SetTarget(PlayerController player)
        {
            if (player == null)
            {
                Debug.LogWarning("[CameraFollow] Attempted to set null target");
                return;
            }

            _localPlayer = player;
            _targetTransform = player.transform;
            _hasValidTarget = true;

            // Initialize camera position to target position
            if (_targetTransform != null)
            {
                Vector3 initialPosition = _targetTransform.position;
                initialPosition.z = _cameraTransform.position.z; // Maintain camera's Z position
                initialPosition.y += verticalOffset;

                _cameraTransform.position = initialPosition;
                _targetPosition = initialPosition;
            }

            if (enableDebugLogs)
                Debug.Log($"[CameraFollow] Target set to: {player.name}");
        }

        /// <summary>
        /// Clears the current target
        /// </summary>
        public void ClearTarget()
        {
            _localPlayer = null;
            _targetTransform = null;
            _hasValidTarget = false;

            if (enableDebugLogs)
                Debug.Log("[CameraFollow] Target cleared");
        }

        // ==================== UPDATE LOGIC ====================

        private void LateUpdate()
        {
            // If no target, try to find local player again
            if (!HasTarget)
            {
                FindLocalPlayer();
                return;
            }

            // Validate target still exists and has authority
            if (!ValidateTarget())
            {
                ClearTarget();
                return;
            }

            // Update camera position
            UpdateCameraPosition();
        }

        /// <summary>
        /// Validates that the current target is still valid
        /// </summary>
        /// <returns>True if target is valid, false otherwise</returns>
        private bool ValidateTarget()
        {
            if (_localPlayer == null || _targetTransform == null)
                return false;

            // Check if the player still has input authority
            if (_localPlayer.Object == null || !_localPlayer.Object.HasInputAuthority)
                return false;

            return true;
        }

        /// <summary>
        /// Updates camera position to follow the target
        /// </summary>
        private void UpdateCameraPosition()
        {
            Vector3 targetPos = _targetTransform.position;

            // Calculate look-ahead based on player movement
            CalculateLookAhead(targetPos);

            // Apply look-ahead to X position
            targetPos.x += _lookAheadX;

            // Apply vertical offset and follow settings
            if (enableVerticalFollow)
            {
                targetPos.y += verticalOffset;
            }
            else
            {
                targetPos.y = _cameraTransform.position.y;
            }

            // Maintain camera's Z position
            targetPos.z = _cameraTransform.position.z;

            // Apply bounds if enabled
            if (useBounds)
            {
                targetPos = ApplyBounds(targetPos);
            }

            // Smooth movement
            _targetPosition = targetPos;

            // Use different smoothing for vertical and horizontal movement
            Vector3 currentPos = _cameraTransform.position;
            Vector3 newPos = currentPos;

            // Horizontal smoothing
            newPos.x = Mathf.SmoothDamp(currentPos.x, _targetPosition.x, ref _velocity.x, 1f / followSpeed);

            // Vertical smoothing (if enabled)
            if (enableVerticalFollow)
            {
                newPos.y = Mathf.SmoothDamp(currentPos.y, _targetPosition.y, ref _velocity.y, 1f / verticalFollowSpeed);
            }

            _cameraTransform.position = newPos;
        }

        /// <summary>
        /// Calculates look-ahead distance based on player movement
        /// </summary>
        /// <param name="targetPosition">Current target position</param>
        private void CalculateLookAhead(Vector3 targetPosition)
        {
            if (_localPlayer == null) return;

            // Get player's movement direction
            float moveInput = _localPlayer.CurrentMoveInput;
            float targetLookAhead = moveInput * lookAheadDistance;

            // Smooth the look-ahead transition
            _lookAheadX = Mathf.Lerp(_lookAheadX, targetLookAhead, lookAheadSpeed * Time.deltaTime);
        }

        /// <summary>
        /// Applies camera bounds to the target position
        /// </summary>
        /// <param name="position">Desired camera position</param>
        /// <returns>Clamped position within bounds</returns>
        private Vector3 ApplyBounds(Vector3 position)
        {
            position.x = Mathf.Clamp(position.x, minBounds.x, maxBounds.x);
            position.y = Mathf.Clamp(position.y, minBounds.y, maxBounds.y);
            return position;
        }

        // ==================== PUBLIC API ====================

        /// <summary>
        /// Updates camera bounds at runtime
        /// </summary>
        /// <param name="min">Minimum bounds</param>
        /// <param name="max">Maximum bounds</param>
        public void SetBounds(Vector2 min, Vector2 max)
        {
            minBounds = min;
            maxBounds = max;

            if (enableDebugLogs)
                Debug.Log($"[CameraFollow] Camera bounds updated: Min({min}), Max({max})");
        }

        /// <summary>
        /// Updates follow speed at runtime
        /// </summary>
        /// <param name="speed">New follow speed</param>
        public void SetFollowSpeed(float speed)
        {
            followSpeed = Mathf.Max(0.1f, speed);

            if (enableDebugLogs)
                Debug.Log($"[CameraFollow] Follow speed updated: {followSpeed}");
        }

        /// <summary>
        /// Immediately snaps camera to target position (no smoothing)
        /// </summary>
        public void SnapToTarget()
        {
            if (!HasTarget) return;

            Vector3 snapPosition = _targetTransform.position;
            snapPosition.y += verticalOffset;
            snapPosition.z = _cameraTransform.position.z;

            if (useBounds)
                snapPosition = ApplyBounds(snapPosition);

            _cameraTransform.position = snapPosition;
            _velocity = Vector3.zero;
            _lookAheadX = 0f;

            if (enableDebugLogs)
                Debug.Log("[CameraFollow] Snapped to target position");
        }

        // ==================== DEBUG ====================

        private void OnDrawGizmosSelected()
        {
            if (!useBounds) return;

            // Draw camera bounds
            Gizmos.color = Color.yellow;
            Vector3 center = new Vector3((minBounds.x + maxBounds.x) * 0.5f, (minBounds.y + maxBounds.y) * 0.5f, 0f);
            Vector3 size = new Vector3(maxBounds.x - minBounds.x, maxBounds.y - minBounds.y, 0f);
            Gizmos.DrawWireCube(center, size);

            // Draw target indicator
            if (HasTarget)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(_targetTransform.position, 0.5f);
            }
        }
    }
}