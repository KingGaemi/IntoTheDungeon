using UnityEngine;
using IntoTheDungeon.Features.Unity;

public abstract class PhysicsEntityBehaviour : EntityBehaviour, IRigidBodyDependent
{
    public Rigidbody2D Rigidbody { get; set; }

    protected override void OnAwake()
    {
        base.OnAwake();
        this.InitializeRigidBody();
    }
}