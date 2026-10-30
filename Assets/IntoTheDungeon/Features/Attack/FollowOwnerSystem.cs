using System.Collections.Generic;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.ECS.Systems;
using IntoTheDungeon.Core.ECS.Abstractions.Scheduling;
using IntoTheDungeon.Core.ECS.Components;
using IntoTheDungeon.Core.Util;
using IntoTheDungeon.Features.State;

namespace IntoTheDungeon.Features.Attack
{
    /// <summary>
    /// FollowOwner가 붙은 히트박스를 주인 위치·방향에 맞춰 옮긴다. 주인이 사라지면 히트박스도 지운다.
    /// </summary>
    public sealed class FollowOwnerSystem : GameSystem, ITick
    {
        readonly List<Entity> _orphans = new(8);

        public static Vec2 ComputeCenter(Vec2 ownerPos, Vec2 offset, float facingSign)
            => new(ownerPos.X + offset.X * facingSign, ownerPos.Y + offset.Y);

        public void Tick(float dt)
        {
            var em = _world.EntityManager;
            _orphans.Clear();

            foreach (var chunk in em.GetChunks(typeof(HitboxComponent), typeof(FollowOwnerComponent)))
            {
                var hitboxes = chunk.GetComponentArray<HitboxComponent>();
                var follows = chunk.GetComponentArray<FollowOwnerComponent>();
                var entities = chunk.GetEntities();

                for (int i = 0; i < chunk.Count; i++)
                {
                    ref var hb = ref hitboxes[i];
                    if (!em.Exists(hb.Owner) || !em.TryGetComponent<TransformComponent>(hb.Owner, out var ownerTr))
                    {
                        _orphans.Add(entities[i]);
                        continue;
                    }

                    float sign = em.TryGetComponent<StateComponent>(hb.Owner, out var st) ? st.FacingToSign() : 1f;
                    hb.Center = ComputeCenter(ownerTr.Position, follows[i].Offset, sign);
                }
            }

            foreach (var e in _orphans)
                em.DestroyEntity(e);
        }
    }
}
