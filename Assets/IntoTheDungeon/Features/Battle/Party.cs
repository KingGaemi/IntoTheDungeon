using System.Collections.Generic;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.ECS.Components;
using IntoTheDungeon.Core.Abstractions.Gameplay;
using IntoTheDungeon.Core.Util;
using IntoTheDungeon.Features.Status;

namespace IntoTheDungeon.Features.Battle
{
    /// <summary>
    /// 파티 = Player 팀 캐릭터 전원. 메인 캐릭터 없이 모두 자동전투하고, 플레이어는 파티원별 필살기만 누른다.
    /// 슬롯 순서(필살기 버튼 1·2·3…)는 엔티티 번호 순(먼저 생긴 순)으로 고정한다.
    /// </summary>
    public static class Party
    {
        public const TeamFlag Team = TeamFlag.Player;

        public struct Member
        {
            public Entity Entity;
            public Vec2 Position;
            public bool Alive;
            public int Hp, MaxHp;
        }

        /// <summary>파티원(죽은 사람 포함)을 슬롯 순서로 채운다.</summary>
        public static void Collect(IEntityManager em, List<Member> into)
        {
            into.Clear();
            foreach (var chunk in em.GetChunks(typeof(TeamComponent), typeof(StatusComponent), typeof(TransformComponent)))
            {
                var teams = chunk.GetComponentArray<TeamComponent>();
                var statuses = chunk.GetComponentArray<StatusComponent>();
                var transforms = chunk.GetComponentArray<TransformComponent>();
                var entities = chunk.GetEntities();
                for (int i = 0; i < chunk.Count; i++)
                {
                    if (teams[i].TeamFlag != Team) continue;
                    into.Add(new Member
                    {
                        Entity = entities[i],
                        Position = transforms[i].Position,
                        Alive = statuses[i].IsAlive,
                        Hp = statuses[i].CurrentHp,
                        MaxHp = statuses[i].MaxHp
                    });
                }
            }
            into.Sort((a, b) => a.Entity.Index.CompareTo(b.Entity.Index));
        }

        /// <summary>살아 있는 파티원의 평균 위치(카메라·스폰 기준점). 없으면 false.</summary>
        public static bool TryGetCenter(IEntityManager em, out Vec2 center, out int alive)
        {
            center = default;
            alive = 0;
            var sum = Vec2.Zero;
            foreach (var chunk in em.GetChunks(typeof(TeamComponent), typeof(StatusComponent), typeof(TransformComponent)))
            {
                var teams = chunk.GetComponentArray<TeamComponent>();
                var statuses = chunk.GetComponentArray<StatusComponent>();
                var transforms = chunk.GetComponentArray<TransformComponent>();
                for (int i = 0; i < chunk.Count; i++)
                {
                    if (teams[i].TeamFlag != Team || !statuses[i].IsAlive) continue;
                    sum = sum + transforms[i].Position;
                    alive++;
                }
            }
            if (alive == 0) return false;
            center = sum * (1f / alive);
            return true;
        }
    }
}
