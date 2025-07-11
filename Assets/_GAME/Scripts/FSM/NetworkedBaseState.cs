namespace _GAME.Scripts.FSM
{
    using Fusion;
    using UnityEngine;

    /// <summary>
    /// Enhanced BaseState that supports networking through Photon Fusion
    /// This replaces the original BaseState to work with NetworkBehaviour entities
    /// </summary>
    public abstract class NetworkedBaseState<T> : IState where T : NetworkBehaviour
    {
        protected readonly T        entity;
        protected readonly Animator animator;

        // Common animation settings
        protected const float crossFadeDuration = 0.1f;

        // Debug settings
        protected virtual bool EnableStateLogs => true;

        protected NetworkedBaseState(T entity, Animator animator = null)
        {
            this.entity   = entity;
            this.animator = animator;
        }

        public virtual void EnterState()
        {
            if (EnableStateLogs) Debug.Log($"[{GetType().Name}] EnterState - Authority: {(entity.HasStateAuthority ? "Server" : "Client")}");
        }

        public virtual void StateUpdate()
        {
            // Called every frame on all clients for visual updates
        }

        public virtual void StateFixedUpdate()
        {
            // Called every network tick, only on state authority for game logic
        }

        public virtual void ExitState()
        {
            if (EnableStateLogs) Debug.Log($"[{GetType().Name}] ExitState - Authority: {(entity.HasStateAuthority ? "Server" : "Client")}");
        }

        // Helper methods for common networking operations
        protected bool          HasStateAuthority => entity.HasStateAuthority;
        protected bool          HasInputAuthority => entity.HasInputAuthority;
        protected NetworkRunner Runner            => entity.Runner;

        // Input helper methods - uncomment when you have NetworkInputData defined
        /*
        /// <summary>
        /// Get network input data (simplified version)
        /// </summary>
        protected NetworkInputData GetNetworkInput()
        {
            if (HasInputAuthority && entity.Runner != null)
            {
                if (entity.Runner.TryGetInputForPlayer<NetworkInputData>(entity.Object.InputAuthority, out var input))
                {
                    return input;
                }
            }

            return default;
        }

        /// <summary>
        /// Check if input button is pressed
        /// </summary>
        protected bool IsInputPressed(InputButtons button)
        {
            var input = GetNetworkInput();
            return (input.Buttons & button) == button;
        }

        /// <summary>
        /// Get movement input
        /// </summary>
        protected Vector2 GetMovementInput()
        {
            var input = GetNetworkInput();
            return input.MovementInput;
        }
        */

        /// <summary>
        /// Play animation if animator is available
        /// </summary>
        protected void PlayAnimation(int animationHash, float crossFade = crossFadeDuration)
        {
            if (animator != null)
            {
                animator.CrossFade(animationHash, crossFade);
            }
        }

        /// <summary>
        /// Play animation by name
        /// </summary>
        protected void PlayAnimation(string animationName, float crossFade = crossFadeDuration)
        {
            if (animator != null)
            {
                var hash = Animator.StringToHash(animationName);
                animator.CrossFade(hash, crossFade);
            }
        }

        /// <summary>
        /// Check if current animation state matches the given name
        /// </summary>
        protected bool IsPlayingAnimation(string animationName, int layerIndex = 0)
        {
            if (animator == null) return false;

            var stateInfo = animator.GetCurrentAnimatorStateInfo(layerIndex);
            return stateInfo.IsName(animationName);
        }

        /// <summary>
        /// Get normalized time of current animation
        /// </summary>
        protected float GetAnimationTime(int layerIndex = 0)
        {
            if (animator == null) return 0f;

            return animator.GetCurrentAnimatorStateInfo(layerIndex).normalizedTime;
        }

        /// <summary>
        /// Check if animation has finished playing
        /// </summary>
        protected bool IsAnimationFinished(int layerIndex = 0)
        {
            if (animator == null) return true;

            var stateInfo = animator.GetCurrentAnimatorStateInfo(layerIndex);
            return stateInfo.normalizedTime >= 1.0f && !animator.IsInTransition(layerIndex);
        }
    }

    /// <summary>
    /// Simple base state for non-networked entities (keeps compatibility with original FSM)
    /// </summary>
    public abstract class SimpleBaseState<T> : IState
    {
        protected readonly T        entity;
        protected readonly Animator animator;
        protected const    float    crossFadeDuration = 0.1f;

        protected SimpleBaseState(T entity, Animator animator = null)
        {
            this.entity   = entity;
            this.animator = animator;
        }

        public virtual void EnterState()       { }
        public virtual void StateUpdate()      { }
        public virtual void StateFixedUpdate() { }
        public virtual void ExitState()        { }

        protected void PlayAnimation(int animationHash, float crossFade = crossFadeDuration)
        {
            if (animator != null)
            {
                animator.CrossFade(animationHash, crossFade);
            }
        }

        protected void PlayAnimation(string animationName, float crossFade = crossFadeDuration)
        {
            if (animator != null)
            {
                var hash = Animator.StringToHash(animationName);
                animator.CrossFade(hash, crossFade);
            }
        }
    }
}