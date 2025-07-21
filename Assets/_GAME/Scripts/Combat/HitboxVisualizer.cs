namespace _GAME.Scripts.Combat
{
    using System;
    using _GAME.Scripts.Core;
    using UnityEngine;

    public class HitboxVisualizer : MonoBehaviour

    {
        [Header("Visualization Settings")]
        [SerializeField] private bool showHitboxes = true;
        [SerializeField] private Color activeHitboxColor = Color.red;
        [SerializeField] private Color inactiveHitboxColor = Color.gray;
        [SerializeField] private float hitboxAlpha = 0.3f;
        [SerializeField] private bool showOnlyDuringAttack = true;

        private ComboController _comboController;
        private PlayerController _playerController;

        private void Awake()
        {
            _comboController = GetComponent<ComboController>();
            _playerController = GetComponent<PlayerController>();

            if (_comboController == null || _playerController == null)
            {
                Debug.LogError("HitboxVisualizer requires ComboController and PlayerController components.");
                enabled = false;
            }
        }

        private void Update()
        {
            //only show on local client:
            if (!this.showHitboxes || !Application.isEditor)
            {
                return;
            }

            //only show during attack:
            if (showOnlyDuringAttack && !_comboController.IsExecutingAttack)
            {
                return;
            }
        }

        private void DrawCurrentHitbox()
        {
            var (center, size, layers) = this._comboController.GetHitboxData();

            if(size == Vector2.zero) return;

            var boxColor = this._comboController.IsHitboxActive() ? this.activeHitboxColor : this.inactiveHitboxColor;
            boxColor.a = this.hitboxAlpha;

            //draw wire cube
            DrawWireCube(center, size, boxColor);

            //draw center point
            DrawPoint(center, boxColor, 0.1f);
        }

        private void DrawWireCube(Vector2 center, Vector2 size, Color color)
        {
            var halfSize = size / 2f;
            var bottomLeft = center - halfSize;
            var bottomRight = new Vector2(center.x + halfSize.x, center.y - halfSize.y);
            var topLeft = new Vector2(center.x - halfSize.x, center.y + halfSize.y);
            var topRight = center + halfSize;

            Debug.DrawLine(bottomLeft, bottomRight, color);
            Debug.DrawLine(bottomRight, topRight, color);
            Debug.DrawLine(topRight, topLeft, color);
            Debug.DrawLine(topLeft, bottomLeft, color);
        }

        private void DrawPoint(Vector2 position, Color color, float size)
        {
            var offset = Vector2.one * size;
            Debug.DrawLine(position - offset, position + offset, color);
            Debug.DrawLine(position - new Vector2(offset.x, -offset.y), position + new Vector2(-offset.x, offset.y), color);
        }

        #if UNITY_EDITOR

        private void OnDrawGizmos()
        {
            if(!this.showHitboxes || this._comboController == null) return;

            if(showOnlyDuringAttack && !this._comboController.IsExecutingAttack)
            {
                return;
            }

            var (center, size, layers) = this._comboController.GetHitboxData();

            if(size == Vector2.zero) return;

            var gizmosColor = this._comboController.IsHitboxActive() ? this.activeHitboxColor : this.inactiveHitboxColor;
            gizmosColor.a = this.hitboxAlpha;
            Gizmos.color = gizmosColor;
            Gizmos.DrawWireCube(center, size);

            gizmosColor.a = hitboxAlpha * 0.5f;
            Gizmos.color = gizmosColor;
            Gizmos.DrawCube(center, size);

            Gizmos.color = Color.white;
            Gizmos.DrawSphere(center, 0.05f); // Draw center point for better visibility
        }

        #endif
    }
}