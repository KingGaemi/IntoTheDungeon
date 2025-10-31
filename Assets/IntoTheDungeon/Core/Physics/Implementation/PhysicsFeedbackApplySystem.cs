using IntoTheDungeon.Core.ECS.Systems;
using IntoTheDungeon.Core.ECS.Abstractions.Scheduling;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.Abstractions.Messages;
using IntoTheDungeon.Core.Physics.Abstractions;
using IntoTheDungeon.Core.ECS.Components;
using IntoTheDungeon.Core.ECS.Abstractions;



namespace IntoTheDungeon.Core.Physics.Implementation
{
    public sealed class PhysFeedbackApplySystem : GameSystem, ITick
    {
        IPhysicsFeedbackStore _fb;
        IHandleOwnerIndex _index;
        ILogger _log;

        IEntityManager _em => _world.EntityManager;


        public override void Initialize(IWorld world)
        {
            base.Initialize(world);

            if (!world.TryGet(out _log))
            {
                Enabled = false;
                return;
            }
            Enabled = world.TryGet(out _fb) && world.TryGet(out _index);
        }

        public void Tick(float dt)
        {
            var handles = _fb.Handles;
            var kinds = _fb.Kinds;
            var flags = _fb.Flags;
            var vecs = _fb.Vecs;
            var angles = _fb.Angles;

            for (int i = 0; i < _fb.Count; i++)
            {
                if (kinds[i] != PhysicsOpKind.SyncPhysToEcs) continue;
                if (!_index.TryGetOwner(handles[i], out var e)) continue;
                if (_em.TryGetComponent(e, out TransformComponent t))
                {
                    if ((flags[i] & PhysFlags.HasPos) != 0) { t.Position.X = vecs[i].X; t.Position.Y = vecs[i].Y; }
                    if ((flags[i] & PhysFlags.HasAngle) != 0) { t.Rotation = angles[i]; }
                    t.Moved = true;
                    _em.SetComponent(e, t);
                    // _log.Log($"[ApplySystem] Entity{e.Index}, x = {t.Position.X}, y = {t.Position.Y}");
                }
            }

            _fb.ClearFrame();
        }
    }
}