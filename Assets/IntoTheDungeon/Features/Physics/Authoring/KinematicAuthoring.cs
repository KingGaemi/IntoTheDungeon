// Core.Runtime/Unity/Components
using UnityEngine;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Features.Physics.Components;
using IntoTheDungeon.Runtime.Unity.World;



namespace IntoTheDungeon.Features.Physics.Authoring
{
    [DisallowMultipleComponent]
    // 1. Authoring (Editor 편집용)
    public class KinematicAuthoring : MonoBehaviour, IAuthoring
    {
        [SerializeField] EntityRootBehaviour entityRoot;
        [SerializeField] PhysicsBodyReference physicsBodyReference;
        // Baker 정의

        public IBaker CreateBaker()
        {
            return new KinematicBaker();
        }
        class KinematicBaker : UnityBaker<KinematicAuthoring>
        {
            protected override void Bake(KinematicAuthoring authoring)
            {
                
                AddComponent(new KinematicComponent
                {
                    Direction = new IntoTheDungeon.Core.Util.Vec2(1.0f, 0),
                    Magnitude = 0f
                });



                Debug.Log($"[KinematicBaker] Baked {authoring.name} -> Entity {Entity}");
            }
        }
    }


    public class PhysicsBodyReference : IntoTheDungeon.Core.ECS.Abstractions.IComponentData
    {
        public Rigidbody2D Rigidbody;
    }
}
