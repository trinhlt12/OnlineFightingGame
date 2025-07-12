namespace _GAME.Scripts.Core
{
    using UnityEngine;
    using Fusion;
    using _GAME.Scripts.FSM;
    using _GAME.Scripts.FSM.ConcreteState;
    using _GAME.Scripts.Combat;

    /// <summary>
    /// Central player controller - manages all player logic
    /// States will call methods from this controller
    /// Updated to work with automatic state animation system and combat system
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(NetworkTransform))]
    public class PlayerController : NetworkBehaviour
    {
        [Header("Combat")] [SerializeField] private bool enableCombatLogs = false;

        [Header("Movement")] [SerializeField] private float moveSpeed = 5f;

        [Header("Ground Check")] [SerializeField] private Transform groundCheckPoint;
        [SerializeField]                          private float     groundCheckRadius = 0.2f;
        [SerializeField]                          private LayerMask groundLayer;

        [Header("References")] [SerializeField] private Animator animator;

        [Header("Animation Settings")] [SerializeField] private bool  enableAnimationLogs = false;
        [Header("Jump Settings")] [SerializeField]      private float jumpForce           = 10f;
        [SerializeField]                                private float airMoveSpeed        = 3f; // Reduced speed in air

        // Components
        public  Rigidbody2D           _rigidbody;
        public  NetworkedStateMachine _stateMachine;
        private ComboController       _comboController; // Combat system integration

        // Network properties
        [Networked] public bool  IsGrounded       { get; private set; }
        [Networked] public bool  IsFacingRight    { get; private set; } = true;
        [Networked] public float CurrentMoveInput { get; private set; }

        // Combat System - Network properties for attack input
        [Networked] public bool AttackInputConsumed { get; private set; }

        // Jump tracking for double jump system
        [Networked] public int JumpsUsed { get; private set; }

        // This is now a Tick-local property, not networked. It signals a jump request for the current tick.
        public  bool             JumpQueued { get; private set; }
        private NetworkInputData _lastFrameInput;
        private NetworkInputData _currentFrameInput;
        private bool             _jumpInputConsumedThisFrame = false;

        public bool WasAttackPressedThisFrame
        {
            get
            {
                return _currentFrameInput.WasAttackPressedThisFrame()
                    && !AttackInputConsumed;
            }
        }

        public bool WasJumpPressedThisFrame
        {
            get
            {
                return _currentFrameInput.WasPressedThisFrame(NetworkButtons.Jump)
                    && !_jumpInputConsumedThisFrame;
            }
        }

        // Properties for states to access
        public  Rigidbody2D Rigidbody    => _rigidbody;
        public  Animator    Animator     => animator;
        public  bool        HasMoveInput => Mathf.Abs(CurrentMoveInput) > 0.01f;
        private bool        _lastKnownFacingDirection;
        public  bool        CurrentFacingRight => IsFacingRight;
        public  bool        CanJump            => JumpsUsed < MAX_JUMPS;
        public  bool        HasJumpInput       => JumpQueued;

        // COMBAT SYSTEM - Properties for accessing combat state
        public bool            IsInCombo       => _comboController != null && _comboController.IsInCombo;
        public bool            IsAttacking     => _comboController != null && _comboController.AttackPhase != AttackPhase.None;
        public ComboController ComboController => _comboController;

        //CONSTANTS:
        private const int MAX_JUMPS = 2;

        public override void Spawned()
        {
            _rigidbody       = GetComponent<Rigidbody2D>();
            _stateMachine    = GetComponent<NetworkedStateMachine>() ?? gameObject.AddComponent<NetworkedStateMachine>();
            _comboController = GetComponent<ComboController>(); // Get combat controller

            if (animator == null)
            {
                animator = GetComponent<Animator>();
                if (animator == null)
                {
                    Debug.LogWarning($"[PlayerController] No Animator component found on {gameObject.name}. Animation playback will be disabled.");
                }
            }

            // Validate ComboController
            if (_comboController == null)
            {
                Debug.LogWarning($"[PlayerController] No ComboController found on {gameObject.name}. Combat system will be disabled.");
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
            var idleState   = new IdleState(this);
            var moveState   = new MoveState(this);
            var jumpState   = new JumpState(this);
            var attackState = new AttackState(this); // NEW: Combat state

            _stateMachine.RegisterState(idleState);
            _stateMachine.RegisterState(moveState);
            _stateMachine.RegisterState(jumpState);
            _stateMachine.RegisterState(attackState); // NEW: Register attack state

            // EXISTING: Movement transitions
            _stateMachine.AddTransition(idleState, moveState, new FuncPredicate(() => HasMoveInput));
            _stateMachine.AddTransition(moveState, idleState, new FuncPredicate(() => !HasMoveInput));

            // EXISTING: Jump transitions
            _stateMachine.AddTransition(idleState, jumpState,
                new FuncPredicate(() => WasJumpPressedThisFrame && CanJump));
            _stateMachine.AddTransition(moveState, jumpState,
                new FuncPredicate(() => WasJumpPressedThisFrame && CanJump));

            // EXISTING: Landing transitions
            _stateMachine.AddTransition(jumpState, idleState, new FuncPredicate(() => IsGrounded && !HasMoveInput));
            _stateMachine.AddTransition(jumpState, moveState, new FuncPredicate(() => IsGrounded && HasMoveInput));

            // NEW: Attack transitions - from ground states to attack
            _stateMachine.AddTransition(idleState, attackState,
                new FuncPredicate(() => WasAttackPressedThisFrame && IsGrounded && CanStartAttack()));
            _stateMachine.AddTransition(moveState, attackState,
                new FuncPredicate(() => WasAttackPressedThisFrame && IsGrounded && CanStartAttack()));

            // NEW: Attack completion transitions - from attack back to movement
            _stateMachine.AddTransition(attackState, idleState,
                new FuncPredicate(() => IsAttackComplete() && !HasMoveInput));
            _stateMachine.AddTransition(attackState, moveState,
                new FuncPredicate(() => IsAttackComplete() && HasMoveInput));

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
            // Reset consumption flags at start of each tick
            _jumpInputConsumedThisFrame = false;
            AttackInputConsumed         = false;

            if (GetInput(out NetworkInputData input))
            {
                _lastFrameInput    = _currentFrameInput;
                _currentFrameInput = input;
                CurrentMoveInput   = input.horizontal;
            }

            if (HasStateAuthority)
            {
                CheckGround();

                if (IsGrounded && _rigidbody.velocity.y <= 0)
                {
                    JumpsUsed = 0;
                }
            }
        }

        public void ConsumeAttackInput()
        {
            AttackInputConsumed = true;
            if (enableCombatLogs) Debug.Log("[PlayerController] Attack input consumed");
        }

        public void ConsumeJumpInput()
        {
            _jumpInputConsumedThisFrame = true;
            if (enableAnimationLogs) Debug.Log("[PlayerController] Jump input consumed");
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

            var velocity = _rigidbody.velocity;
            velocity.y          = 0;
            _rigidbody.velocity = velocity;

            _rigidbody.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
            JumpsUsed++;

            // CONSUME the jump input immediately after use
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

        /// <summary>
        /// Get current attack input type from network input
        /// Used by combat states to determine which attack to perform
        /// </summary>
        public AttackInputType GetCurrentAttackInputType()
        {
            return _currentFrameInput.attackInputType;
        }

        /// <summary>
        /// Check if player has movement input (for combo system)
        /// </summary>
        public bool HasMovementInput()
        {
            return _currentFrameInput.HasMovementInput();
        }

        /// <summary>
        /// Check if player can start a new attack
        /// Used by state machine transitions
        /// </summary>
        public bool CanStartAttack()
        {
            if (_comboController == null) return false;

            var inputType = GetCurrentAttackInputType();
            return _comboController.CanPerformAttack(inputType);
        }

        /// <summary>
        /// Check if current attack sequence is complete
        /// Used by state machine transitions
        /// </summary>
        public bool IsAttackComplete()
        {
            return _comboController?.IsAttackComplete() ?? true;
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