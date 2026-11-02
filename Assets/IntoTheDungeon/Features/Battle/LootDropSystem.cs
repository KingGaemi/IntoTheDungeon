using System.Collections.Generic;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.ECS.Systems;
using IntoTheDungeon.Core.ECS.Abstractions.Scheduling;
using IntoTheDungeon.Core.ECS.Components;
using IntoTheDungeon.Core.Util;
using IntoTheDungeon.Features.Status;
using IntoTheDungeon.Features.Run;

namespace IntoTheDungeon.Features.Battle
{
    /// <summary>바닥에 떨어진 아이템. 그림은 Unity 쪽 LootRenderer가 그린다. 전투에서 이기면 BattleFlowSystem이 모아 판(RunState.Loot)에 넣는다.</summary>
    public struct LootDropComponent : IComponentData
    {
        public int ItemIndex;   // ItemPool.All 번호
        public Vec2 Position;   // 바닥 평면(떨어진 자리)
        public float Age;       // 떨어진 뒤 지난 시간(튀어 오르는 연출용)
        public bool Collected;  // 이미 주웠음(그림 숨김)

        // 방을 깨면 가방으로 날아간다: CollectDelay 뒤 FlyTime 동안 날고, 다 날면 Collected
        public bool Collecting;
        public float CollectAge, CollectDelay;
        public const float FlyTime = 0.55f;
    }

    /// <summary>드랍 판정을 이미 한 시체 표시.</summary>
    public struct LootRolledTag : IComponentData { }

    /// <summary>
    /// 적이 쓰러지면 확률로 그 자리에 아이템을 떨어뜨린다(임시값).
    /// 확률 = 최대 체력/3300(강한 몹일수록 잘 떨굼, 오크 약 12%; 몹 체력에 맞춰 조정) + 방 보너스(정예 +6%, 보스 +15%). 등급은 방 종류·층이 높을수록 좋아진다.
    /// </summary>
    public sealed class LootDropSystem : GameSystem, ITick
    {
        readonly List<(Entity e, Vec2 pos, int maxHp)> _dead = new(8);

        /// <summary>가방으로 날아가는 중인 아이템이 남아 있나(정비 화면 전환은 다 들어간 뒤).</summary>
        public static bool AnyFlying(IEntityManager em)
        {
            foreach (var chunk in em.GetChunks(typeof(LootDropComponent)))
            {
                var drops = chunk.GetComponentArray<LootDropComponent>();
                for (int i = 0; i < chunk.Count; i++)
                    if (drops[i].Collecting && !drops[i].Collected) return true;
            }
            return false;
        }

        public void Tick(float dt)
        {
            var em = _world.EntityManager;

            foreach (var chunk in em.GetChunks(typeof(LootDropComponent)))
            {
                var drops = chunk.GetComponentArray<LootDropComponent>();
                for (int i = 0; i < chunk.Count; i++)
                {
                    drops[i].Age += dt;
                    if (!drops[i].Collecting || drops[i].Collected) continue;
                    drops[i].CollectAge += dt;
                    if (drops[i].CollectAge >= drops[i].CollectDelay + LootDropComponent.FlyTime) drops[i].Collected = true;
                }
            }

            _dead.Clear();
            foreach (var chunk in em.GetChunks(typeof(StatusComponent), typeof(TeamComponent), typeof(TransformComponent)))
            {
                var statuses = chunk.GetComponentArray<StatusComponent>();
                var teams = chunk.GetComponentArray<TeamComponent>();
                var transforms = chunk.GetComponentArray<TransformComponent>();
                var entities = chunk.GetEntities();
                for (int i = 0; i < chunk.Count; i++)
                {
                    if (statuses[i].IsAlive || teams[i].TeamFlag == Party.Team) continue;
                    if (em.HasComponent<LootRolledTag>(entities[i])) continue;
                    _dead.Add((entities[i], transforms[i].Position, statuses[i].MaxHp));
                }
            }
            if (_dead.Count == 0) return;

            var run = RunState.Current;
            var room = run.Current?.Type ?? RoomType.Battle;
            float chanceBonus = room == RoomType.Boss ? 0.15f : room == RoomType.Elite ? 0.06f : 0f;
            float rareBonus = (room == RoomType.Boss ? 1f : room == RoomType.Elite ? 0.5f : 0f) + 0.05f * run.Depth + SealStone.RareBonus(run.SealTier);
            float dropMul = SealStone.DropMul(run.SealTier);

            // 청크 순회가 끝난 뒤 구조 변경
            foreach (var (e, pos, maxHp) in _dead)
            {
                em.AddComponent(e, new LootRolledTag());
                if (run.Rng.NextDouble() >= (maxHp / 3300f + chanceBonus) * dropMul) continue;
                var item = ItemPool.Roll(run.Rng, rareBonus);
                var drop = em.CreateEntity();
                em.AddComponent(drop, new LootDropComponent { ItemIndex = item.Index, Position = pos });
            }
        }
    }
}
