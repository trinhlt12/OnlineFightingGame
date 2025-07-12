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

        // This is now a Tick-local property, not networked. It signals a jump request for the current tick.
        public bool JumpQueued { get; private set; }

        // Properties for states to access
        public  Rigidbody2D Rigidbody    => _rigidbody;
        public  Animator    Animator     => animator;
        public  bool        HasMoveInput => Mathf.Abs(CurrentMoveInput) > 0.01f;
        private bool        _lastKnownFacingDirection;
        public  bool        CurrentFacingRight => IsFacingRight;
        public  bool        CanJump            => JumpsUsed < MAX_JUMPS;
        public  bool        HasJumpInput       => JumpQueued;

        //CONSTANTS:
        private const int MAX_JUMPS = 2;

        public override void Spawned()
        {
            _rigidbody    = GetComponent<Rigidbody2D>();
            _stateMachine = GetComponent<NetworkedStateMachine>() ?? gameObject.AddComponent<NetworkedStateMachine>();

            if (animator == null)
            {
                animator = GetComponent<Animator>();
                if (animator == null)
                {
                    Debug.LogWarning($"[PlayerController] No Animator component found on {gameObject.name}. Animation playback will be disabled.");
                }
            }

            InitializeStateMachine();
            _lastKnownFacingDirection = IsFacingRight;
            UpdateCharacterDirection();

            if (HasInputAuthority)
            {
                Debug.Log($"[PlayerController] Local player spawned");
                if (FindObjectOfType<InputManager>() == null)
                {
                    new GameObject("InputManager").AddComponent<InputManager>();
                }
            }
        }

        private void InitializeStateMachine()
        {
            var idleState = new IdleState(this);
            var moveState = new MoveState(this);
            var jumpState = new JumpState(this);

            _stateMachine.RegisterState(idleState);
            _stateMachine.RegisterState(moveState);
            _stateMachine.RegisterState(jumpState);

            // Add transitions
            // *** FIX: Removed IsGrounded from these transitions. The FSM will handle landing via transitions from JumpState. ***
            _stateMachine.AddTransition(idleState, moveState, new FuncPredicate(() => HasMoveInput));
            _stateMachine.AddTransition(moveState, idleState, new FuncPredicate(() => !HasMoveInput));

            // Transitions to JumpState (from any other state)
            _stateMachine.AddTransition(idleState, jumpState, new FuncPredicate(() => HasJumpInput && CanJump));
            _stateMachine.AddTransition(moveState, jumpState, new FuncPredicate(() => HasJumpInput && CanJump));
            // This allows for the double jump
            _stateMachine.AddTransition(jumpState, jumpState, new FuncPredicate(() => HasJumpInput && CanJump));

            // Transitions from JumpState (when landing)
            _stateMachine.AddTransition(jumpState, idleState, new FuncPredicate(() => IsGrounded && !HasMoveInput));
            _stateMachine.AddTransition(jumpState, moveState, new FuncPredicate(() => IsGrounded && HasMoveInput));

            _stateMachine.InitializeStateMachine(idleState);
        }

        public override void Render()
        {
            if (IsFacingRight != _lastKnownFacingDirection)
            {
                _lastKnownFacingDirection = IsFacingRight;
                UpdateCharacterDirection();

                if (enableAnimationLogs) Debug.Log($"[PlayerController] {(HasStateAuthority ? "Server" : "Client")} detected direction change to {(IsFacingRight ? "RIGHT" : "LEFT")}");
            }
        }

        public override void FixedUpdateNetwork()
        {
            // Reset jump queue at the start of every tick on the Input Authority
            if (HasInputAuthority)
            {
                JumpQueued = false;
            }

            if (GetInput(out NetworkInputData input))
            {
                CurrentMoveInput = input.horizontal;
                if (input.WasPressedThisFrame(NetworkButtons.Jump))
                {
                    JumpQueued = true;
                }
            }

            // Server-authoritative logic
            if (HasStateAuthority)
            {
                CheckGround();

                if (IsGrounded && _rigidbody.velocity.y <= 0)
                {
                    JumpsUsed = 0;
                }
            }
        }

        public void ConsumeJumpInput()
        {
            JumpQueued = false;
        }

        public void Move(float horizontalInput)
        {
            if (!HasStateAuthority) return;

            var velocity = _rigidbody.velocity;
            velocity.x          = horizontalInput * moveSpeed;
            _rigidbody.velocity = velocity;

            if (horizontalInput != 0)
            {
                IsFacingRight = horizontalInput > 0;
            }
        }

        public void StopMovement()
        {
            if (!HasStateAuthority) return;

            var velocity = _rigidbody.velocity;
            velocity.x          = 0f;
            _rigidbody.velocity = velocity;
        }

        public void PerformJump()
        {
            if (!HasStateAuthority || !CanJump) return;

            // *** FIX: Reset vertical velocity before applying new jump force for a consistent "boost" feel. ***
            var velocity = _rigidbody.velocity;
            velocity.y = 0;
            _rigidbody.velocity = velocity;

            // Apply jump force
            _rigidbody.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);

            JumpsUsed++;
            ConsumeJumpInput();

            if (enableAnimationLogs) Debug.Log($"[PlayerController] Performed jump {JumpsUsed}/{MAX_JUMPS}");
        }

        public void HandleAirMovement(float horizontalInput)
        {
            if (!HasStateAuthority) return;

            var velocity = _rigidbody.velocity;
            velocity.x          = horizontalInput * airMoveSpeed;
            _rigidbody.velocity = velocity;

            if (horizontalInput != 0)
            {
                IsFacingRight = horizontalInput > 0;
            }
        }

        private void CheckGround()
        {
            if (groundCheckPoint == null) return;

            IsGrounded = Physics2D.OverlapCircle(
                groundCheckPoint.position,
                groundCheckRadius,
                groundLayer
            );
        }

        // ... (rest of the file remains the same)
        public void PlayAnimation(string animationName)
        {
            if (string.IsNullOrEmpty(animationName))
            {
                if (enableAnimationLogs) Debug.LogWarning($"[PlayerController] Attempted to play null/empty animation on {gameObject.name}");
                return;
            }

            if (animator != null)
            {
                animator.Play(animationName);
                if (enableAnimationLogs) Debug.Log($"[PlayerController] Playing animation: {animationName} on {gameObject.name}");
            }
            else
            {
                if (enableAnimationLogs) Debug.LogWarning($"[PlayerController] No Animator component found, cannot play animation: {animationName}");
            }
        }

        private void UpdateCharacterDirection()
        {
            var localScale = transform.localScale;
            if (IsFacingRight && localScale.x < 0)
            {
                localScale.x         = Mathf.Abs(localScale.x);
                transform.localScale = localScale;
            }
            else if (!IsFacingRight && localScale.x > 0)
            {
                localScale.x         = -Mathf.Abs(localScale.x);
                transform.localScale = localScale;
            }
        }

    }
}