using System.Collections.Generic;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.ECS.Systems;
using IntoTheDungeon.Core.ECS.Abstractions.Scheduling;
using IntoTheDungeon.Core.ECS.Components;
using IntoTheDungeon.Core.Abstractions.World;

namespace IntoTheDungeon.Features.Battle
{
    public enum MeterKind { Dealt, Taken, Healed }

    /// <summary>
    /// 이번 방 전투 기록(딜 미터): 파티 슬롯별 준 피해·받은 피해·막은 피해·회복량과 1초 단위 기록(그래프용).
    /// 방에 들어갈 때 비우고, 전투 시간(웨이브 진행 중)만 센다. 실제로 깎인 체력만 센다(넘친 피해·넘친 회복은 빼고).
    /// </summary>
    public static class CombatMeter
    {
        public const int Slots = 4;
        const int MaxSeconds = 900;

        static readonly float[,] s_totals = new float[3, Slots];
        static readonly float[] s_blocked = new float[Slots];
        static readonly List<float[]>[] s_timeline = { new(), new(), new() };

        /// <summary>전투 시간(초).</summary>
        public static float Elapsed { get; private set; }

        public static float Total(MeterKind kind, int slot) => s_totals[(int)kind, slot];
        public static float Blocked(int slot) => s_blocked[slot];
        public static int Seconds => s_timeline[0].Count;

        /// <summary>그 초에 그 슬롯이 기록한 양.</summary>
        public static float At(MeterKind kind, int second, int slot)
        {
            var list = s_timeline[(int)kind];
            return second >= 0 && second < list.Count ? list[second][slot] : 0f;
        }

        public static void Reset()
        {
            System.Array.Clear(s_totals, 0, s_totals.Length);
            System.Array.Clear(s_blocked, 0, s_blocked.Length);
            foreach (var t in s_timeline) t.Clear();
            Elapsed = 0f;
        }

        internal static void Advance(float dt)
        {
            Elapsed += dt;
            int need = System.Math.Min(MaxSeconds, (int)Elapsed + 1);
            foreach (var t in s_timeline)
                while (t.Count < need) t.Add(new float[Slots]);
        }

        static int SlotOf(IEntityManager em, Entity e)
        {
            if (!em.Exists(e) || !em.TryGetComponent<TeamComponent>(e, out var team) || team.TeamFlag != Party.Team) return -1;
            return em.TryGetComponent<PartySlotComponent>(e, out var ps) && ps.Slot >= 0 && ps.Slot < Slots ? ps.Slot : -1;
        }

        static void Add(MeterKind kind, int slot, float amount)
        {
            if (slot < 0 || amount <= 0f) return;
            s_totals[(int)kind, slot] += amount;
            var t = s_timeline[(int)kind];
            if (t.Count == 0) return;   // 전투 시작 전(연출 중)
            int sec = System.Math.Min(t.Count - 1, (int)Elapsed);
            t[sec][slot] += amount;
        }

        /// <summary>실제로 깎인 체력(DamageProcessingSystem): 때린 쪽이 파티면 딜, 맞은 쪽이 파티면 받은 피해.</summary>
        public static void Damage(IEntityManager em, Entity source, Entity victim, int amount)
        {
            if (amount <= 0) return;
            Add(MeterKind.Dealt, SlotOf(em, source), amount);
            Add(MeterKind.Taken, SlotOf(em, victim), amount);
        }

        /// <summary>방어력·난이도·방패로 줄어든 만큼(HitDetectSystem). 탱킹 수치에 같이 보인다.</summary>
        public static void Block(IEntityManager em, Entity victim, int amount)
        {
            int slot = SlotOf(em, victim);
            if (slot >= 0 && amount > 0) s_blocked[slot] += amount;
        }

        /// <summary>실제로 찬 체력(자연 회복·레벨 업 회복 등).</summary>
        public static void Heal(IEntityManager em, Entity target, int amount) => Add(MeterKind.Healed, SlotOf(em, target), amount);
    }

    /// <summary>웨이브 진행 중에만 미터 시간을 센다(웨이브 사이 전진 연출은 뺀다).</summary>
    public sealed class CombatMeterSystem : GameSystem, ITick
    {
        BattleProgress _progress;

        public override void Initialize(IWorld world)
        {
            base.Initialize(world);
            Enabled = world.TryGet(out _progress);
        }

        public void Tick(float dt)
        {
            if (_progress.Phase == BattlePhase.Fighting) CombatMeter.Advance(dt);   // 웨이브 사이 전진 연출은 빼고
        }
    }
}
