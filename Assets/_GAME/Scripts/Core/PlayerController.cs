namespace _GAME.Scripts.Core
{
    using UnityEngine;
    using Fusion;
    using _GAME.Scripts.FSM;
    using _GAME.Scripts.FSM.ConcreteState;

    /// <summary>
    /// Central player controller - manages all player logic
    /// States will call methods from this controller
    /// Updated to work with automatic state animation system
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(NetworkTransform))]
    public class PlayerController : NetworkBehaviour
    {
        [Header("Movement")] [SerializeField] private float moveSpeed = 5f;

        [Header("Ground Check")] [SerializeField] private Transform groundCheckPoint;
        [SerializeField]                          private float     groundCheckRadius = 0.2f;
        [SerializeField]                          private LayerMask groundLayer;

        [Header("References")] [SerializeField] private Animator animator;

        [Header("Animation Settings")] [SerializeField] private bool  enableAnimationLogs = false;
        [Header("Jump Settings")] [SerializeField]      private float jumpForce           = 10f;
        [SerializeField]                                private float airMoveSpeed        = 3f; // Reduced speed in air

        // Components
        public Rigidbody2D           _rigidbody;
        private NetworkedStateMachine _stateMachine;

        // Network properties
        [Networked] public bool IsGrounded    { get; private set; }
        [Networked] public bool IsFacingRight { get; private set; } = true;

        [Networked] public float CurrentMoveInput { get; private set; }

        // Jump tracking for double jump system
        [Networked] public int  JumpsUsed        { get; private set; }
        [Networked] public bool JumpInputPressed { get; private set; }

        // Properties for states to access
        public  Rigidbody2D Rigidbody    => _rigidbody;
        public  Animator    Animator     => animator;
        public  bool        HasMoveInput => Mathf.Abs(CurrentMoveInput) > 0.01f;
        private bool        _lastKnownFacingDirection;
        public  bool        CurrentFacingRight => IsFacingRight;
        public  bool        CanJump            => JumpsUsed < MAX_JUMPS;
        public  bool        HasJumpInput       => JumpInputPressed;

        //CONSTANTS:
        private const int MAX_JUMPS = 2;

        public override void Spawned()
        {
            // Get components
            _rigidbody    = GetComponent<Rigidbody2D>();
            _stateMachine = GetComponent<NetworkedStateMachine>() ?? gameObject.AddComponent<NetworkedStateMachine>();

            // Ensure animator is assigned
            if (animator == null)
            {
                animator = GetComponent<Animator>();
                if (animator == null)
                {
                    Debug.LogWarning($"[PlayerController] No Animator component found on {gameObject.name}. Animation playback will be disabled.");
                }
            }

            // Initialize state machine
            InitializeStateMachine();
            _lastKnownFacingDirection = IsFacingRight;

            UpdateCharacterDirection();

            if (HasInputAuthority)
            {
                Debug.Log($"[PlayerController] Local player spawned");

                // Ensure InputManager exists
                if (FindObjectOfType<InputManager>() == null)
                {
                    new GameObject("InputManager").AddComponent<InputManager>();
                }
            }
        }

        private void InitializeStateMachine()
        {
            // Create states with automatic animation names
            var idleState = new IdleState(this); // Will auto-play "Idle" animation
            var moveState = new MoveState(this); // Will auto-play "Move" animation
            var jumpState = new JumpState(this); // Will auto-play "Jump" animation


            // Register states
            _stateMachine.RegisterState(idleState);
            _stateMachine.RegisterState(moveState);
            _stateMachine.RegisterState(jumpState);


            // Add transitions
            // Idle -> Move when has input
            _stateMachine.AddTransition(idleState, moveState,
                new FuncPredicate(() => HasMoveInput));
            _stateMachine.AddTransition(idleState, jumpState,
                new FuncPredicate(() => HasJumpInput && CanJump));

            // Move -> Idle when no input
            _stateMachine.AddTransition(moveState, idleState,
                new FuncPredicate(() => !HasMoveInput));
            _stateMachine.AddTransition(moveState, jumpState,
                new FuncPredicate(() => HasJumpInput && CanJump));

            // From Jump
            _stateMachine.AddTransition(jumpState, idleState,
                new FuncPredicate(() => IsGrounded && !HasMoveInput));
            _stateMachine.AddTransition(jumpState, moveState,
                new FuncPredicate(() => IsGrounded && HasMoveInput));

            // Initialize with idle state
            _stateMachine.InitializeStateMachine(idleState);
        }

        public override void Render()
        {
            // Check for facing direction changes and update visuals for all clients
            if (IsFacingRight != _lastKnownFacingDirection)
            {
                _lastKnownFacingDirection = IsFacingRight;
                UpdateCharacterDirection();

                if (enableAnimationLogs) Debug.Log($"[PlayerController] {(HasStateAuthority ? "Server" : "Client")} detected direction change to {(IsFacingRight ? "RIGHT" : "LEFT")}");
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (HasStateAuthority)
            {
                // Update ground check
                CheckGround();

                // Reset jumps when grounded
                if (IsGrounded && JumpsUsed > 0)
                {
                    JumpsUsed = 0;
                }

                // Get and store input
                if (Runner.TryGetInputForPlayer<NetworkInputData>(Object.InputAuthority, out var input))
                {
                    CurrentMoveInput = input.horizontal;
                    // Use fully qualified name to avoid conflict
                    JumpInputPressed = input.buttons.IsPressed(_GAME.Scripts.Core.NetworkButtons.Jump);
                }
            }
        }

        /// <summary>
        /// Move the character - called by MoveState
        /// </summary>
        public void Move(float horizontalInput)
        {
            if (!HasStateAuthority) return;

            // Apply movement
            var velocity = _rigidbody.velocity;
            velocity.x          = horizontalInput * moveSpeed;
            _rigidbody.velocity = velocity;

            // Update facing direction
            if (horizontalInput != 0)
            {
                IsFacingRight = horizontalInput > 0;

                /*
                UpdateCharacterDirection();
            */
            }
        }

        /// <summary>
        /// Stop movement - called by IdleState
        /// </summary>
        public void StopMovement()
        {
            if (!HasStateAuthority) return;

            var velocity = _rigidbody.velocity;
            velocity.x          = 0f;
            _rigidbody.velocity = velocity;
        }

        /// <summary>
        /// Perform jump - called by JumpState
        /// </summary>
        public void PerformJump()
        {
            if (!HasStateAuthority || !CanJump) return;

            // Apply jump force
            var velocity = _rigidbody.velocity;
            velocity.y          = jumpForce;
            _rigidbody.velocity = velocity;

            // Increment jump counter
            JumpsUsed++;

            if (enableAnimationLogs) Debug.Log($"[PlayerController] Performed jump {JumpsUsed}/{MAX_JUMPS}");
        }

        /// <summary>
        /// Handle movement while in air - called by JumpState
        /// </summary>
        public void HandleAirMovement(float horizontalInput)
        {
            if (!HasStateAuthority) return;

            // Apply air movement (reduced speed compared to ground)
            var velocity = _rigidbody.velocity;
            velocity.x          = horizontalInput * airMoveSpeed;
            _rigidbody.velocity = velocity;

            // Update facing direction
            if (horizontalInput != 0)
            {
                IsFacingRight = horizontalInput > 0;
            }
        }

        /// <summary>
        /// Check for additional jump input while in air - called by JumpState
        /// </summary>
        public void CheckForAdditionalJump()
        {
            if (!HasStateAuthority) return;

            // If jump input is pressed and we can still jump
            if (JumpInputPressed && CanJump)
            {
                PerformJump();
            }
        }

        /// <summary>
        /// Check if character is on ground
        /// </summary>
        private void CheckGround()
        {
            if (groundCheckPoint == null) return;

            IsGrounded = Physics2D.OverlapCircle(
                groundCheckPoint.position,
                groundCheckRadius,
                groundLayer
            );
        }

        /// <summary>
        /// Play animation - enhanced version for state system compatibility
        /// This method is called by states for animation playback
        /// </summary>
        public void PlayAnimation(string animationName)
        {
            if (string.IsNullOrEmpty(animationName))
            {
                if (enableAnimationLogs) Debug.LogWarning($"[PlayerController] Attempted to play null/empty animation on {gameObject.name}");
                return;
            }

            if (animator != null)
            {
                // Use Play for immediate state changes (no blending)
                animator.Play(animationName);

                if (enableAnimationLogs) Debug.Log($"[PlayerController] Playing animation: {animationName} on {gameObject.name}");
            }
            else
            {
                if (enableAnimationLogs) Debug.LogWarning($"[PlayerController] No Animator component found, cannot play animation: {animationName}");
            }
        }

        /// <summary>
        /// Play animation with crossfade for smooth transitions
        /// </summary>
        public void PlayAnimationWithCrossfade(string animationName, float crossfadeDuration = 0.1f)
        {
            if (string.IsNullOrEmpty(animationName))
            {
                if (enableAnimationLogs) Debug.LogWarning($"[PlayerController] Attempted to play null/empty animation with crossfade on {gameObject.name}");
                return;
            }

            if (animator != null)
            {
                var animationHash = Animator.StringToHash(animationName);
                animator.CrossFade(animationHash, crossfadeDuration);

                if (enableAnimationLogs) Debug.Log($"[PlayerController] Playing animation with crossfade: {animationName} (duration: {crossfadeDuration}) on {gameObject.name}");
            }
            else
            {
                if (enableAnimationLogs) Debug.LogWarning($"[PlayerController] No Animator component found, cannot play animation with crossfade: {animationName}");
            }
        }

        /// <summary>
        /// Updates character visual direction based on facing direction
        /// Call this method to flip character sprite when direction changes
        /// </summary>
        private void UpdateCharacterDirection()
        {
            // Get current scale
            var localScale = transform.localScale;

            // Flip the character by changing the X scale
            if (IsFacingRight && localScale.x < 0)
            {
                localScale.x         = Mathf.Abs(localScale.x);
                transform.localScale = localScale;

                if (enableAnimationLogs) Debug.Log($"[PlayerController] Flipped character to face RIGHT");
            }
            else if (!IsFacingRight && localScale.x > 0)
            {
                localScale.x         = -Mathf.Abs(localScale.x);
                transform.localScale = localScale;

                if (enableAnimationLogs) Debug.Log($"[PlayerController] Flipped character to face LEFT");
            }
        }

        /// <summary>
        /// Force update character direction (useful for initialization)
        /// </summary>
        public void ForceUpdateDirection()
        {
            UpdateCharacterDirection();
        }

        /// <summary>
        /// Check if a specific animation is currently playing
        /// </summary>
        public bool IsPlayingAnimation(string animationName, int layerIndex = 0)
        {
            if (animator == null || string.IsNullOrEmpty(animationName)) return false;

            var stateInfo = animator.GetCurrentAnimatorStateInfo(layerIndex);
            return stateInfo.IsName(animationName);
        }

        /// <summary>
        /// Get the normalized time of current animation
        /// </summary>
        public float GetCurrentAnimationTime(int layerIndex = 0)
        {
            if (animator == null) return 0f;

            return animator.GetCurrentAnimatorStateInfo(layerIndex).normalizedTime;
        }

        /// <summary>
        /// Force set animator parameter (useful for complex animation logic)
        /// </summary>
        public void SetAnimatorFloat(string parameterName, float value)
        {
            if (animator != null && !string.IsNullOrEmpty(parameterName))
            {
                animator.SetFloat(parameterName, value);
            }
        }

        public void SetAnimatorBool(string parameterName, bool value)
        {
            if (animator != null && !string.IsNullOrEmpty(parameterName))
            {
                animator.SetBool(parameterName, value);
            }
        }

        public void SetAnimatorTrigger(string parameterName)
        {
            if (animator != null && !string.IsNullOrEmpty(parameterName))
            {
                animator.SetTrigger(parameterName);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (Object == null || !Object.IsValid)
            {
                if (groundCheckPoint != null)
                {
                    Gizmos.color = Color.gray;
                    Gizmos.DrawWireSphere(groundCheckPoint.position, groundCheckRadius);
                }
                return;
            }
            // Draw ground check
            if (groundCheckPoint != null)
            {
                Gizmos.color = IsGrounded ? Color.green : Color.red;
                Gizmos.DrawWireSphere(groundCheckPoint.position, groundCheckRadius);
            }
        }
    }
}