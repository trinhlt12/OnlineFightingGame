namespace _GAME.Scripts.FSM
{
    using Fusion;
    using UnityEngine;

    /// <summary>
    /// Enhanced BaseState that supports networking through Photon Fusion
    /// Now with automatic animation playback on state entry
    /// </summary>
    public abstract class NetworkedBaseState<T> : IState where T : NetworkBehaviour
    {
        protected readonly T        entity;
        protected readonly Animator animator;
        private            string   animationName;
        protected void SetAnimationName(string newName)
        {
            this.animationName = newName;
        }

        // Common animation settings
        protected const float crossFadeDuration = 0.1f;

        // Debug settings
        protected virtual bool EnableStateLogs => true;

        /// <summary>
        /// Constructor with automatic animation support
        /// </summary>
        /// <param name="entity">The networked entity this state controls</param>
        /// <param name="animationName">Animation to play when entering this state (can be null/empty)</param>
        /// <param name="animator">Optional animator component (will try to find on entity if null)</param>
        protected NetworkedBaseState(T entity, string animationName, Animator animator = null)
        {
            this.entity = entity;
            this.animationName = animationName;
            this.animator = animator ?? entity.GetComponent<Animator>();
        }

        public virtual void EnterState()
        {
            if (EnableStateLogs)
                Debug.Log($"[{GetType().Name}] EnterState - Authority: {(entity.HasStateAuthority ? "Server" : "Client")}, Animation: {animationName ?? "None"}");

            // Automatically play state animation
            PlayStateAnimation();
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
            if (EnableStateLogs)
                Debug.Log($"[{GetType().Name}] ExitState - Authority: {(entity.HasStateAuthority ? "Server" : "Client")}");
        }

        /// <summary>
        /// Automatically plays the animation assigned to this state
        /// Override this method if you need custom animation logic
        /// </summary>
        protected virtual void PlayStateAnimation()
        {
            if (string.IsNullOrEmpty(animationName))
            {
                if (EnableStateLogs)
                    Debug.Log($"[{GetType().Name}] No animation name specified, skipping animation playback");
                return;
            }

            // Try multiple ways to play animation based on entity type
            if (TryPlayAnimationViaPlayerController() || TryPlayAnimationViaAnimator())
            {
                if (EnableStateLogs)
                    Debug.Log($"[{GetType().Name}] Successfully played animation: {animationName}");
            }
            else
            {
                Debug.LogWarning($"[{GetType().Name}] Failed to play animation: {animationName} - No valid animation component found");
            }
        }

        /// <summary>
        /// Try to play animation through PlayerController (preferred method)
        /// </summary>
        private bool TryPlayAnimationViaPlayerController()
        {
            if (entity is _GAME.Scripts.Core.PlayerController playerController)
            {
                playerController.PlayAnimation(animationName);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Try to play animation directly through Animator component
        /// </summary>
        private bool TryPlayAnimationViaAnimator()
        {
            if (animator != null)
            {
                PlayAnimation(animationName, crossFadeDuration);
                return true;
            }
            return false;
        }

        // Helper methods for common networking operations
        protected bool HasStateAuthority => entity.HasStateAuthority;
        protected bool HasInputAuthority => entity.HasInputAuthority;
        protected NetworkRunner Runner => entity.Runner;

        /// <summary>
        /// Play animation if animator is available (with crossfade)
        /// </summary>
        protected void PlayAnimation(int animationHash, float crossFade = crossFadeDuration)
        {
            if (animator != null)
            {
                animator.CrossFade(animationHash, crossFade);
            }
        }

        /// <summary>
        /// Play animation by name (with crossfade)
        /// </summary>
        protected void PlayAnimation(string animName, float crossFade = crossFadeDuration)
        {
            if (animator != null)
            {
                var hash = Animator.StringToHash(animName);
                animator.CrossFade(hash, crossFade);
            }
        }

        /// <summary>
        /// Play animation immediately without crossfade
        /// </summary>
        protected void PlayAnimationImmediate(string animName)
        {
            if (animator != null)
            {
                animator.Play(animName);
            }
        }

        /// <summary>
        /// Check if current animation state matches the given name
        /// </summary>
        protected bool IsPlayingAnimation(string animName, int layerIndex = 0)
        {
            if (animator == null) return false;

            var stateInfo = animator.GetCurrentAnimatorStateInfo(layerIndex);
            return stateInfo.IsName(animName);
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

        /// <summary>
        /// Force play a different animation (useful for state-specific overrides)
        /// </summary>
        protected void PlayCustomAnimation(string customAnimationName, float crossFade = crossFadeDuration)
        {
            if (string.IsNullOrEmpty(customAnimationName))
            {
                Debug.LogWarning($"[{GetType().Name}] Attempted to play null/empty custom animation");
                return;
            }

            if (entity is _GAME.Scripts.Core.PlayerController playerController)
            {
                playerController.PlayAnimation(customAnimationName);
            }
            else if (animator != null)
            {
                PlayAnimation(customAnimationName, crossFade);
            }

            if (EnableStateLogs)
                Debug.Log($"[{GetType().Name}] Played custom animation: {customAnimationName}");
        }
    }

    /// <summary>
    /// Simple base state for non-networked entities (keeps compatibility with original FSM)
    /// Also updated with automatic animation support
    /// </summary>
    public abstract class SimpleBaseState<T> : IState
    {
        protected readonly T entity;
        protected readonly Animator animator;
        protected readonly string animationName;
        protected const float crossFadeDuration = 0.1f;

        protected SimpleBaseState(T entity, string animationName, Animator animator = null)
        {
            this.entity = entity;
            this.animationName = animationName;
            this.animator = animator;
        }

        public virtual void EnterState()
        {
            PlayStateAnimation();
        }

        public virtual void StateUpdate() { }
        public virtual void StateFixedUpdate() { }
        public virtual void ExitState() { }

        protected virtual void PlayStateAnimation()
        {
            if (!string.IsNullOrEmpty(animationName) && animator != null)
            {
                PlayAnimation(animationName, crossFadeDuration);
            }
        }

        protected void PlayAnimation(int animationHash, float crossFade = crossFadeDuration)
        {
            if (animator != null)
            {
                animator.CrossFade(animationHash, crossFade);
            }
        }

        protected void PlayAnimation(string animName, float crossFade = crossFadeDuration)
        {
            if (animator != null)
            {
                var hash = Animator.StringToHash(animName);
                animator.CrossFade(hash, crossFade);
            }
        }
    }
}