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
    /// 파티원마다 슬롯 번호(PartySlotComponent)가 있다: 필살기 버튼 1·2·3…, 판(RunState)의 체력 기록 칸과 같다.
    /// 쓰러진 파티원은 다음 방에 스폰하지 않으므로, 슬롯은 생성 순서가 아니라 이 번호로 맞춘다.
    /// 슬롯이 없는 파티원(테스트용 T키 소환 등)은 슬롯 파티원 뒤에 생긴 순서로 붙는다.
    /// </summary>
    public struct PartySlotComponent : IComponentData
    {
        public int Slot;
    }

    public static class Party
    {
        public const TeamFlag Team = TeamFlag.Player;
        public const int NoSlot = 1000;   // 슬롯 없는 파티원의 Slot = NoSlot + 엔티티 번호

        public struct Member
        {
            public int Slot;
            public Entity Entity;
            public Vec2 Position;
            public bool Alive;
            public int Hp, MaxHp;
        }

        /// <summary>그 슬롯의 파티원(없으면 false: 쓰러져 이번 방에 안 나왔거나 빈 슬롯).</summary>
        public static bool TryGetSlot(List<Member> members, int slot, out Member member)
        {
            foreach (var m in members)
                if (m.Slot == slot) { member = m; return true; }
            member = default;
            return false;
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
                    var e = entities[i];
                    into.Add(new Member
                    {
                        Slot = em.TryGetComponent<PartySlotComponent>(e, out var ps) ? ps.Slot : NoSlot + e.Index,
                        Entity = e,
                        Position = transforms[i].Position,
                        Alive = statuses[i].IsAlive,
                        Hp = statuses[i].CurrentHp,
                        MaxHp = statuses[i].MaxHp
                    });
                }
            }
            into.Sort((a, b) => a.Slot.CompareTo(b.Slot));
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
