namespace _GAME.Scripts.Core
{
    using UnityEngine;
    using Fusion;
    using _GAME.Scripts.FSM;
    using _GAME.Scripts.FSM.ConcreteState;

    /// <summary>
    /// Central player controller - manages all player logic
    /// States will call methods from this controller
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

        // Components
        private Rigidbody2D           _rigidbody;
        private NetworkedStateMachine _stateMachine;

        // Network properties
        [Networked] public bool  IsGrounded       { get; private set; }
        [Networked] public bool  IsFacingRight    { get; private set; } = true;
        [Networked] public float CurrentMoveInput { get; private set; }

        // Properties for states to access
        public Rigidbody2D Rigidbody    => _rigidbody;
        public Animator    Animator     => animator;
        public bool        HasMoveInput => Mathf.Abs(CurrentMoveInput) > 0.01f;

        public override void Spawned()
        {
            // Get components
            _rigidbody    = GetComponent<Rigidbody2D>();
            _stateMachine = GetComponent<NetworkedStateMachine>() ?? gameObject.AddComponent<NetworkedStateMachine>();

            // Initialize state machine
            InitializeStateMachine();

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
            // Create states
            var idleState = new IdleState(this);
            var moveState = new MoveState(this);

            // Register states
            _stateMachine.RegisterState(idleState);
            _stateMachine.RegisterState(moveState);

            // Add transitions
            // Idle -> Move when has input
            _stateMachine.AddTransition(idleState, moveState,
                new FuncPredicate(() => HasMoveInput));

            // Move -> Idle when no input
            _stateMachine.AddTransition(moveState, idleState,
                new FuncPredicate(() => !HasMoveInput));

            // Initialize with idle state
            _stateMachine.InitializeStateMachine(idleState);
        }

        public override void FixedUpdateNetwork()
        {
            if (HasStateAuthority)
            {
                // Update ground check
                CheckGround();

                // Get and store input
                if (Runner.TryGetInputForPlayer<NetworkInputData>(Object.InputAuthority, out var input))
                {
                    CurrentMoveInput = input.horizontal;
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
        /// Play animation - called by states
        /// </summary>
        public void PlayAnimation(string animationName)
        {
            if (animator != null)
            {
                animator.Play(animationName);
            }
        }

        private void OnDrawGizmosSelected()
        {
            // Draw ground check
            if (groundCheckPoint != null)
            {
                Gizmos.color = IsGrounded ? Color.green : Color.red;
                Gizmos.DrawWireSphere(groundCheckPoint.position, groundCheckRadius);
            }
        }
    }
}