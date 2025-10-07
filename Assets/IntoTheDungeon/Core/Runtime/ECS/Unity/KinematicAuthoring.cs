// Core.Runtime/Unity/Components
using UnityEngine;
using IntoTheDungeon.Core.Runtime.World;
using IntoTheDungeon.Core.ECS.Components.Physics;
using IntoTheDungeon.Core.Util.Physics;


[DisallowMultipleComponent]
// 1. Authoring (Editor 편집용)
public class KinematicAuthoring : MonoBehaviour
{
    [SerializeField] EntityRootBehaviour entityRoot;
    [SerializeField] PhysicsBodyReference physicsBodyReference;
    // Baker 정의

    public IBaker CreateBaker()
    {
        return new KinematicBaker();
    }
     class KinematicBaker : Baker<KinematicAuthoring>
    {
        public override void Bake(KinematicAuthoring authoring)
        {
            
            AddComponent(new KinematicComponent
            {
                Direction = new Vec2(1, 0),
                Magnitude = 0f
            });



            Debug.Log($"[KinematicBaker] Baked {authoring.name} -> Entity {Entity}");
        }
    }
}


public class PhysicsBodyReference : IntoTheDungeon.Core.ECS.Components.IComponentData
{
    public Rigidbody2D Rigidbody;
}
