using IntoTheDungeon.Core.ECS;
using IntoTheDungeon.Core.ECS.Components.Physics;
using IntoTheDungeon.Core.Runtime.Physics;
using IntoTheDungeon.Core.Runtime.World;


// KinematicComponent의 Velocity를 이용해 Tick당 Velocity를 IPhysicsBody에 부여
public sealed class KinematicSystem : GameSystem, ILateTick
{
    // private readonly ILogger _log;
    // public KinematicSystem(ILogger log) => _log = log;
    
    
    override public int Priority { get; } = 0;

    override public void Initialize(GameWorld world)
    {
        _world = world;
    }
    public void LateTick(float dt)
    {
        var chunks = _world.EntityManager.GetChunks(typeof(KinematicComponent));

        foreach (var chunk in chunks)
        {
            var kinematics = chunk.GetComponentArray<KinematicComponent>();
            var entities = chunk.GetEntities();

            for (int i = 0; i < chunk.Count; i++)
            {
                ref var kc = ref kinematics[i];

                // Managed Component 조회
                if (_world.EntityManager.HasManagedComponent<UnityPhysicsBody>(entities[i]))
                {
                    var body = _world.EntityManager.GetManagedComponent<UnityPhysicsBody>(entities[i]);
                    
                    body.Rb.linearVelocity = new UnityEngine.Vector2(kc.Velocity.X, body.Rb.linearVelocityY);
                }
            }
        }
    }

    override public void Shutdown() { }
}

