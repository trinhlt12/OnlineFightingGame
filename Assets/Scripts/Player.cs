using System.Collections;
using System.Collections.Generic;
using Fusion;
using Fusion.Addons.SimpleKCC;
using UnityEngine;

public class Player : NetworkBehaviour
{
    [SerializeField] private MeshRenderer[] modelParts;
    [SerializeField] private SimpleKCC      kcc;
    [SerializeField] private float          speed       = 5f;
    [SerializeField] private float          jumpImpulse = 10f;
    [SerializeField] private Transform      cameraTarget;
    [SerializeField] private float          lookSensitivity = 0.15f;

    [Networked] private NetworkButtons PreviousButtons { get; set; }

    public override void Spawned()
    {
        this.kcc.SetGravity(Physics.gravity.y * 2f);
        if (HasInputAuthority)
        {
            foreach (var renderer in this.modelParts)
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            }
            CameraFollow.Singleton.SetTarget(this.cameraTarget);
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (GetInput(out NetInput input))
        {
            this.kcc.AddLookRotation(input.LookDelta * this.lookSensitivity);
            this.UpdateCamTarget();

            Vector3 worldDirection = this.kcc.TransformRotation * new Vector3(input.Direction.x, 0f, input.Direction.y);
            float   jump           = 0f;
            if (input.Buttons.WasPressed(PreviousButtons, InputButton.Jump) && this.kcc.IsGrounded)
            {
                jump = this.jumpImpulse;
            }
            this.kcc.Move(worldDirection.normalized * this.speed, jump);
            PreviousButtons = input.Buttons;
        }
    }

    public override void Render()
    {
        this.UpdateCamTarget();
    }

    private void UpdateCamTarget()
    {
        this.cameraTarget.localRotation = Quaternion.Euler(this.kcc.GetLookRotation().x, 0, 0);
    }
}