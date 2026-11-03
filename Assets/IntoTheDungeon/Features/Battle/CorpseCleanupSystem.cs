using System.Collections.Generic;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.ECS.Systems;
using IntoTheDungeon.Core.ECS.Abstractions.Scheduling;
using IntoTheDungeon.Core.ECS.Components;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Features.Status;

namespace IntoTheDungeon.Features.Battle
{
    /// <summary>시체가 사라지기까지 남은 시간.</summary>
    public struct CorpseComponent : IComponentData
    {
        public float Remaining;
    }

    /// <summary>
    /// 쓰러진 적은 사망 모션을 보여 줄 만큼 누워 있다가 사라진다(그림은 ViewBridge가 옅게 지움).
    /// 파티원 시체는 남긴다: 슬롯·체력 기록과 필살기 버튼("쓰러짐")이 그대로 이어져야 하므로.
    /// </summary>
    public sealed class CorpseCleanupSystem : GameSystem, ITick
    {
        const float CorpseLinger = 1.8f;   // 사망 모션(약 1초) + 잠깐 누워 있기

        IViewOpQueue _viewOps;
        readonly List<Entity> _newCorpses = new(16);
        readonly List<Entity> _expired = new(16);

        public override void Initialize(IWorld world)
        {
            base.Initialize(world);
            world.TryGet(out _viewOps);
        }

        public void Tick(float dt)
        {
            var em = _world.EntityManager;
            _newCorpses.Clear();
            _expired.Clear();

            // 새로 쓰러진 적 찾기(시체 표시가 아직 없는 것)
            foreach (var chunk in em.GetChunks(typeof(StatusComponent), typeof(TeamComponent)))
            {
                var statuses = chunk.GetComponentArray<StatusComponent>();
                var teams = chunk.GetComponentArray<TeamComponent>();
                var entities = chunk.GetEntities();
                for (int i = 0; i < chunk.Count; i++)
                    if (!statuses[i].IsAlive && teams[i].TeamFlag != Party.Team && !em.HasComponent<CorpseComponent>(entities[i]) && !Revive.IsDowned(em, entities[i]))
                        _newCorpses.Add(entities[i]);
            }

            foreach (var chunk in em.GetChunks(typeof(CorpseComponent)))
            {
                var corpses = chunk.GetComponentArray<CorpseComponent>();
                var entities = chunk.GetEntities();
                for (int i = 0; i < chunk.Count; i++)
                {
                    corpses[i].Remaining -= dt;
                    if (corpses[i].Remaining <= 0f) _expired.Add(entities[i]);
                }
            }

            // 청크 순회가 끝난 뒤 구조 변경
            foreach (var e in _newCorpses)
                em.AddComponent(e, new CorpseComponent { Remaining = CorpseLinger });
            foreach (var e in _expired)
            {
                _viewOps?.Enqueue(e);   // 그림 지우기(Despawn)
                em.DestroyEntity(e);
            }
        }
    }
}
