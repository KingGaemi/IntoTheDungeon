using IntoTheDungeon.Core.ECS.Abstractions.Scheduling;
using IntoTheDungeon.Core.Physics.Abstractions;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.ECS.Systems;

namespace IntoTheDungeon.Features.Physics.Systems
{
    public sealed class PhysicsApplySystem : GameSystem, ILateTick
    {
        public PhysicsApplySystem(int priority = 0) : base(priority) { }        
        private IPhysicsBodyStore _store;

        public override void Initialize(IWorld world)
        {
            _world = world;
            _store = _world.Require<IPhysicsBodyStore>();
        }
        public void LateTick(float dt)
        {
            foreach (var ch in _world.EntityManager.GetChunks(typeof(PhysicsCommand),
                                                            typeof(PhysicsBodyRef)))
            {
                var cmd = ch.GetComponentArray<PhysicsCommand>();
                var pref = ch.GetComponentArray<PhysicsBodyRef>();

                for (int i = 0; i < ch.Count; i++)
                {
                    ref var c = ref cmd[i];
                    if (!c.HasVel) continue;

                    var body = _store.Get(pref[i].Handle);
                    var (vx0, vy0) = body.GetLinearVelocity();
                    float nx = vx0, ny = vy0;
                    if (c.HasVel) {
                        if ((c.Axes & VelAxes.X) != 0) nx = c.Mode==VelMode.Set ? c.V.X : vx0 + c.V.X;
                        if ((c.Axes & VelAxes.Y) != 0) ny = c.Mode==VelMode.Set ? c.V.Y : vy0 + c.V.Y;
                        body.SetLinearVelocity(nx, ny);
                        c.HasVel = false;
                    }
                }
            }
        }
    }


}
