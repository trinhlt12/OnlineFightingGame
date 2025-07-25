namespace _GAME.Scripts.Core
{
    using System.Collections.Generic;
    using UnityEngine;
    using Fusion;
    using _GAME.Scripts.FSM;
    using _GAME.Scripts.FSM.ConcreteState;
    using _GAME.Scripts.Combat;

    /// <summary>
    /// REFACTORED: Central player controller with clean input handling
    /// SINGLE RESPONSIBILITY: Input forwarding, movement, and state coordination
    /// Combat logic delegated to ComboController, FSM managed by NetworkedStateMachine
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(NetworkTransform))]
    public class PlayerController : NetworkBehaviour
    {
        [Header("Movement")] [SerializeField] private float moveSpeed    = 5f;
        [SerializeField]                      private float airMoveSpeed = 3f;

        [Header("Jump Settings")] [SerializeField] private float jumpForce = 10f;
        private const                                      int   MAX_JUMPS = 2;

        [Header("Ground Check")] [SerializeField] private Transform groundCheckPoint;
        [SerializeField]                          private float     groundCheckRadius = 0.2f;
        [SerializeField]                          private LayerMask groundLayer;

        [Header("References")] [SerializeField] private Animator animator;

        [Header("Debug")] [SerializeField] private bool enableAnimationLogs = false;
        [SerializeField]                   private bool enableCombatLogs    = false;

        // ==================== COMPONENTS ====================
        public  Rigidbody2D           _rigidbody;
        public  CapsuleCollider2D     _collider;
        public  NetworkedStateMachine _stateMachine;
        private ComboController       _comboController;

        // ==================== NETWORK PROPERTIES ====================
        [Networked] public bool  IsGrounded       { get; private set; }
        [Networked] public bool  IsFacingRight    { get; private set; } = true;
        [Networked] public float CurrentMoveInput { get; private set; }
        [Networked] public int   JumpsUsed        { get; private set; }

        // ==================== INPUT TRACKING ====================
        private NetworkInputData _currentFrameInput;
        private bool             _jumpInputConsumedThisFrame   = false;
        private bool             _attackInputConsumedThisFrame = false;

        // ==================== PROPERTIES ====================
        public                                             Rigidbody2D Rigidbody          => _rigidbody;
        public                                             Animator    Animator           => animator;
        public                                             bool        HasMoveInput       => _inputEnabled && Mathf.Abs(CurrentMoveInput) > 0.1f;
        public                                             bool        CurrentFacingRight => IsFacingRight;
        public                                             bool        CanJump            => JumpsUsed < MAX_JUMPS;
        [Header("Dash Settings")] [SerializeField] private float       dashDistance     = 5f;
        [SerializeField]                           private float       dashCooldown     = 2f;
        private const                                      int         MAX_DASH_CHARGES = 3;

        // Network Properties for Dash (add to existing network properties section)
        [Networked] public int    DashCharges       { get; private set; } = MAX_DASH_CHARGES;
        [Networked] public float  DashCooldownTimer { get; private set; }
        [Networked] public string PreviousStateName { get; private set; } = "";

        // Input Properties for Dash (add to existing input properties section)
        public bool WasDashPressedThisFrame
        {
            get
            {
                if (!_inputEnabled) return false;
                bool pressed = _currentFrameInput.buttons.IsPressed(NetworkButtons.Dodge) && !_currentFrameInput.previousButtons.IsPressed(NetworkButtons.Dodge);
                return pressed && !_dashInputConsumedThisFrame;
            }
        }

        private bool _dashInputConsumedThisFrame = false;

        // Dash Properties (add to existing properties section)
        public bool CanDash => DashCharges > 0 && DashCooldownTimer <= 0;

        // INPUT PROPERTIES - Single source of truth
        public bool WasJumpPressedThisFrame
        {
            get
            {
                return _inputEnabled
                    && _currentFrameInput.WasPressedThisFrame(NetworkButtons.Jump)
                    && !_jumpInputConsumedThisFrame;
            }
        }

        public bool WasAttackPressedThisFrame
        {
            get
            {
                return _inputEnabled
                    && _currentFrameInput.WasAttackPressedThisFrame()
                    && !_attackInputConsumedThisFrame;
            }
        }

        // COMBAT PROPERTIES - Delegate to ComboController
        public bool            IsInCombo       => _comboController != null && _comboController.IsInCombo;
        public bool            IsAttacking     => _comboController != null && _comboController.IsAttacking;
        public ComboController ComboController => _comboController;

        // ==================== LIFECYCLE ====================

        public override void Spawned()
        {
            InitializeComponents();
            InitializeStateMachine();

            if (HasInputAuthority)
            {
                Debug.Log($"[PlayerController] Local player spawned");
                EnsureInputManager();
            }
        }

        private void InitializeComponents()
        {
            _rigidbody       = GetComponent<Rigidbody2D>();
            _collider        = GetComponent<CapsuleCollider2D>();
            _stateMachine    = GetComponent<NetworkedStateMachine>() ?? gameObject.AddComponent<NetworkedStateMachine>();
            _comboController = GetComponent<ComboController>();

            if (animator == null)
            {
                animator = GetComponent<Animator>();
                if (animator == null)
                {
                    Debug.LogWarning($"[PlayerController] No Animator component found on {gameObject.name}");
                }
            }

            if (_comboController == null)
            {
                Debug.LogWarning($"[PlayerController] No ComboController found on {gameObject.name}. Combat will be disabled.");
            }
        }

        private HitState _hitState;

        private void InitializeStateMachine()
        {
            var idleState   = new IdleState(this);
            var moveState   = new MoveState(this);
            var jumpState   = new JumpState(this);
            var attackState = new AttackState(this);
            var hitState    = new HitState(this);
            var dashState   = new DashState(this);
            var dieState    = new DieState(this);

            _hitState = hitState;

            // Register all states
            _stateMachine.RegisterState(idleState);
            _stateMachine.RegisterState(moveState);
            _stateMachine.RegisterState(jumpState);
            _stateMachine.RegisterState(attackState);
            _stateMachine.RegisterState(hitState);
            _stateMachine.RegisterState(dashState);
            _stateMachine.RegisterState(dieState);

            _stateMachine.AddAnyTransition(dieState,
                new FuncPredicate(() => DieState.ShouldEnterDieState(this)));
            _stateMachine.AddTransition(dieState, idleState,
                new FuncPredicate(() => ShouldExitDieState()));

            _stateMachine.AddTransition(idleState, dashState,
                new FuncPredicate(() => WasDashPressedThisFrame && CanDash));
            _stateMachine.AddTransition(moveState, dashState,
                new FuncPredicate(() => WasDashPressedThisFrame && CanDash));
            _stateMachine.AddTransition(jumpState, dashState,
                new FuncPredicate(() => WasDashPressedThisFrame && CanDash));

            // Dash completion transitions
            _stateMachine.AddTransition(dashState, idleState,
                new FuncPredicate(() => dashState.IsDashComplete() && IsGrounded && !HasMoveInput));
            _stateMachine.AddTransition(dashState, moveState,
                new FuncPredicate(() => dashState.IsDashComplete() && IsGrounded && HasMoveInput));
            _stateMachine.AddTransition(dashState, jumpState,
                new FuncPredicate(() => dashState.IsDashComplete() && !IsGrounded));

            /*_stateMachine.AddAnyTransition(hitState,
                new FuncPredicate(() => ShouldEnterHitState()));*/
            _stateMachine.AddTransition(hitState, idleState,
                new FuncPredicate(() => hitState.CanExitHitState() && IsGrounded && !HasMoveInput));
            _stateMachine.AddTransition(hitState, moveState,
                new FuncPredicate(() => hitState.CanExitHitState() && IsGrounded && HasMoveInput));
            _stateMachine.AddTransition(hitState, jumpState,
                new FuncPredicate(() => hitState.CanExitHitState() && !IsGrounded));

            // Movement transitions
            _stateMachine.AddTransition(idleState, moveState,
                new FuncPredicate(() => HasMoveInput && !IsAttacking));
            _stateMachine.AddTransition(moveState, idleState,
                new FuncPredicate(() => !HasMoveInput && !IsAttacking));

            // Jump transitions
            _stateMachine.AddTransition(idleState, jumpState,
                new FuncPredicate(() => WasJumpPressedThisFrame && CanJump && !IsAttacking));
            _stateMachine.AddTransition(moveState, jumpState,
                new FuncPredicate(() => WasJumpPressedThisFrame && CanJump && !IsAttacking));

            // Landing transitions
            _stateMachine.AddTransition(jumpState, idleState,
                new FuncPredicate(() => IsGrounded && !HasMoveInput && !IsAttacking));
            _stateMachine.AddTransition(jumpState, moveState,
                new FuncPredicate(() => IsGrounded && HasMoveInput && !IsAttacking));

            // Attack transitions - from ground states
            _stateMachine.AddTransition(idleState, attackState,
                new FuncPredicate(() => WasAttackPressedThisFrame && IsGrounded && CanStartAttack()));
            _stateMachine.AddTransition(moveState, attackState,
                new FuncPredicate(() => WasAttackPressedThisFrame && IsGrounded && CanStartAttack()));

            // Attack completion transitions
            _stateMachine.AddTransition(attackState, idleState,
                new FuncPredicate(() => IsAttackComplete() && !HasMoveInput));
            _stateMachine.AddTransition(attackState, moveState,
                new FuncPredicate(() => IsAttackComplete() && HasMoveInput));

            // Combo continuation (stay in attack state)
            // No explicit transition needed - AttackState handles this internally

            _stateMachine.InitializeStateMachine(idleState);
        }

        private bool IsGameplayFrozen()
        {
            var gameManager = FindObjectOfType<GameManager>();
            return gameManager != null && gameManager.IsGameplayFrozen();
        }

        /// <summary>
        /// Check if should exit DieState - triggers on round reset
        /// </summary>
        private bool ShouldExitDieState()
        {
            // Only authority can trigger state changes
            if (!HasStateAuthority) return false;

            // Only exit if currently in DieState
            if (!(_stateMachine.CurrentState is DieState)) return false;

            var gameManager = FindObjectOfType<GameManager>();
            if (gameManager == null) return false;

            // Exit DieState when:
            // 1. Round is restarting (Countdown state)
            // 2. Or player health is restored (for round reset)
            bool shouldExit = gameManager.GetCurrentState() == GameManager.GameState.Countdown || gameManager.GetCurrentState() == GameManager.GameState.WaitingToStart;

            return shouldExit;
        }

        public HitState GetHitState()
        {
            return _hitState;
        }

        private bool _inputEnabled = true;

        public void SetInputEnabled(bool enabled)
        {
            _inputEnabled = enabled;
        }

        public bool ShouldEnterHitState()
        {
            var damageReceiver = GetComponent<DamageReceiver>();
            return damageReceiver != null && _stateMachine.CurrentState is HitState == false;
        }

        private void EnsureInputManager()
        {
            if (FindObjectOfType<InputManager>() == null)
            {
                new GameObject("InputManager").AddComponent<InputManager>();
            }
        }
        // ==================== DASH METHODS ====================

        /// <summary>
        /// Perform dash movement
        /// </summary>
        public void PerformDash()
        {
            if (!HasStateAuthority || !CanDash) return;

            // Calculate dash direction
            float direction = IsFacingRight ? 1f : -1f;

            // Apply dash movement
            var velocity = _rigidbody.velocity;
            velocity.x          = direction * dashDistance / 0.2f; // Dash distance over dash duration
            _rigidbody.velocity = velocity;

            // Consume dash charge
            DashCharges--;
            _dashInputConsumedThisFrame = true;

            // Start cooldown if no charges left
            if (DashCharges <= 0)
            {
                DashCooldownTimer = dashCooldown;
            }
        }

        /// <summary>
        /// Check if previous state was jump state
        /// </summary>
        public bool WasPreviousStateJump()
        {
            return PreviousStateName == "JumpState";
        }

        // ==================== DASH COOLDOWN UPDATE ====================
        // Add this to existing UpdatePhysics() method or FixedUpdateNetwork()

        private void UpdateDashCooldown()
        {
            if (!HasStateAuthority) return;

            if (DashCooldownTimer > 0)
            {
                DashCooldownTimer -= Runner.DeltaTime;

                if (DashCooldownTimer <= 0)
                {
                    DashCooldownTimer = 0;
                    DashCharges       = MAX_DASH_CHARGES; // Reset all charges
                }
            }
        }

        // ==================== INPUT CONSUMPTION RESET ====================
        // Add _dashInputConsumedThisFrame = false; to existing ResetInputConsumption() method

        // ==================== PREVIOUS STATE TRACKING ====================
        // Add this method to be called when changing states
        public void SetPreviousState(string stateName)
        {
            if (HasStateAuthority)
            {
                PreviousStateName = stateName;
            }
        }

        // ==================== NETWORK UPDATE ====================

        public override void FixedUpdateNetwork()
        {
            if (IsGameplayFrozen()) return;

            // Reset input consumption flags at start of each tick
            ResetInputConsumption();

            // Process input
            if (GetInput(out NetworkInputData input))
            {
                _currentFrameInput = input;
                CurrentMoveInput   = input.horizontal;
            }

            // Update physics (only on state authority)
            if (HasStateAuthority)
            {
                UpdatePhysics();
            }
        }

        private void ResetInputConsumption()
        {
            _dashInputConsumedThisFrame   = false;
            _jumpInputConsumedThisFrame   = false;
            _attackInputConsumedThisFrame = false;
        }

        private void UpdatePhysics()
        {
            CheckGround();
            UpdateJumpReset();
            UpdateDashCooldown();
        }

        private void UpdateVisualDirection()
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

        private void UpdateJumpReset()
        {
            if (IsGrounded && _rigidbody.velocity.y <= 0)
            {
                JumpsUsed = 0;
            }
        }

        // ==================== MOVEMENT SYSTEM ====================

        public void Move(float horizontalInput)
        {
            if (!HasStateAuthority) return;

            var GameManager = FindObjectOfType<GameManager>();
            if (GameManager != null && GameManager.IsGameplayFrozen()) return;

            var velocity = _rigidbody.velocity;
            velocity.x          = horizontalInput * moveSpeed;
            _rigidbody.velocity = velocity;

            // Update facing direction
            if (horizontalInput != 0)
            {
                IsFacingRight = horizontalInput > 0;
            }
        }

        public override void Render()
        {
            if (!HasStateAuthority && Object.HasInputAuthority)
            {
                // Apply input immediately on input authority client
                if (GetInput(out NetworkInputData input))
                {
                    // Visual-only movement prediction
                    UpdateVisualMovement(input.horizontal);
                }
            }

            // Existing visual updates
            UpdateVisualDirection();
        }

        private void UpdateVisualMovement(float input)
        {
            // Light prediction - chỉ cho visual
            if (Mathf.Abs(input) > 0.1f)
            {
                Vector3 pos = transform.position;
                pos.x              += input * moveSpeed * Time.deltaTime;
                transform.position =  pos;
            }
        }

        public void StopMovement()
        {
            if (!HasStateAuthority) return;

            var velocity = _rigidbody.velocity;
            velocity.x          = 0f;
            _rigidbody.velocity = velocity;
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

        // ==================== JUMP SYSTEM ====================

        public void PerformJump()
        {
            if (!HasStateAuthority || !CanJump) return;

            var velocity = _rigidbody.velocity;
            velocity.y          = 0;
            _rigidbody.velocity = velocity;

            _rigidbody.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
            JumpsUsed++;

            IsGrounded = false;

            ConsumeJumpInput();

            if (enableAnimationLogs) Debug.Log($"[PlayerController] Performed jump {JumpsUsed}/{MAX_JUMPS}");
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

        // ==================== INPUT CONSUMPTION ====================

        public void ConsumeJumpInput()
        {
            _jumpInputConsumedThisFrame = true;
            if (enableAnimationLogs) Debug.Log("[PlayerController] Jump input consumed");
        }

        public void ConsumeAttackInput()
        {
            _attackInputConsumedThisFrame = true;
            if (enableCombatLogs) Debug.Log("[PlayerController] Attack input consumed");
        }

        // ==================== COMBAT INTEGRATION ====================

        public AttackInputType GetCurrentAttackInputType()
        {
            return _currentFrameInput.attackInputType;
        }

        public bool HasMovementInput()
        {
            return _currentFrameInput.HasMovementInput();
        }

        public bool CanStartAttack()
        {
            if (_comboController == null) return false;

            var inputType = GetCurrentAttackInputType();
            return _comboController.CanPerformAttack(inputType);
        }

        public bool IsAttackComplete()
        {
            return _comboController?.IsAttackComplete() ?? true;
        }

        public void SetDashCollision(bool isDashing)
        {
            if (!HasStateAuthority) return;
            var myCollider = this._collider;
            if (myCollider == null) return;

            var allPlayerController = GetAllPlayersInSession();
            foreach (var otherPlayer in allPlayerController)
            {
                if (otherPlayer == this) continue;
                var otherCollider = otherPlayer._collider;
                if (otherCollider == null) continue;
                Physics2D.IgnoreCollision(myCollider, otherCollider, isDashing);
            }
        }

        private List<PlayerController> GetAllPlayersInSession()
        {
            var players = new List<PlayerController>();
            if (Runner == null) return players;

            foreach (var player in FindObjectsOfType<PlayerController>())
            {
                if (player.Object != null && player.Object.IsValid)
                {
                    players.Add(player);
                }
            }
            return players;
        }

        // ==================== ANIMATION SYSTEM ====================

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

        // ==================== DEBUG ====================

        [ContextMenu("Debug Player State")]
        public void DebugPlayerState()
        {
            Debug.Log($"[PlayerController] === Player State Debug ===");
            Debug.Log($"Position: {transform.position}");
            Debug.Log($"IsGrounded: {IsGrounded}");
            Debug.Log($"IsFacingRight: {IsFacingRight}");
            Debug.Log($"HasMoveInput: {HasMoveInput}");
            Debug.Log($"JumpsUsed: {JumpsUsed}/{MAX_JUMPS}");
            Debug.Log($"IsAttacking: {IsAttacking}");
            Debug.Log($"IsInCombo: {IsInCombo}");
            Debug.Log($"HasStateAuthority: {HasStateAuthority}");
            Debug.Log($"HasInputAuthority: {HasInputAuthority}");

            if (_comboController != null)
            {
                _comboController.LogComboState();
            }
        }
    }
}