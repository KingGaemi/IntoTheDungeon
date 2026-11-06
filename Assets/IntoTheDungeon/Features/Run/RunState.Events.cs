using System.Collections.Generic;

namespace IntoTheDungeon.Features.Run
{
    /// <summary>「?」방·휴식 방 선택 이벤트(events 트랙): 대기 중인 이벤트, 본 이벤트, 선택 적용, 체력 비율 도우미, 저장.</summary>
    public sealed partial class RunData
    {
        /// <summary>비용으로 체력을 깎을 때의 바닥(살아 있는 파티원을 쓰러뜨리지 않는다, 임시값).</summary>
        public const float MinHpAfterCost = 0.05f;

        /// <summary>지금 고르는 중인 이벤트(지도·정비 위 모달). 없으면 null.</summary>
        public EventDef PendingEvent { get; private set; }
        /// <summary>이번 판에 이미 나온 이벤트 Id(같은 이벤트는 한 번).</summary>
        public readonly List<string> SeenEvents = new();

        /// <summary>
        /// 선택지 하나를 고른다. 이벤트 없음·범위 밖·숨김·불가면 false.
        /// 적용 → 본 이벤트 기록 → 이벤트 닫기. 지도 위 이벤트에서 아이템·특성 고르기가 생기면 정비 화면을 연다
        /// (주운 아이템은 다음 방에 들어가면 사라지고, 특성 고르기는 정비 화면에만 있으므로).
        /// </summary>
        public bool ChooseEvent(int option)
        {
            var def = PendingEvent;
            if (def == null || option < 0 || option >= def.Options.Count) return false;
            var opt = def.Options[option];
            if (!opt.Visible(this) || !opt.Usable(this)) return false;
            LastMessage = $"{def.Title}: {opt.Label}";   // Apply가 결과(도박 금액 등)를 뒤에 붙일 수 있다
            opt.Apply?.Invoke(this);
            if (!SeenEvents.Contains(def.Id)) SeenEvents.Add(def.Id);
            PendingEvent = null;
            if (Phase == RunPhase.ChoosingRoom && (Loot.Count > 0 || PendingTraits.Count > 0)) OpenEventCamp();
            InventoryVersion++;
            return true;
        }

        /// <summary>지도 위 이벤트 뒤 정비 화면(상점·보상 없음, 휴식 방 정비와 같음). 나가면 「정비로 돌아가기」는 없다.</summary>
        void OpenEventCamp()
        {
            Phase = RunPhase.Camp;
            InRestRoom = true;
            InShopRoom = false;
            PendingRewards.Clear();
            RewardTaken = true;
            Shop.Clear();
        }

        /// <summary>매복 ①: 지금 방에서 강제 구성(ForcedEncounter) 전투를 시작한다(BattleFlowSystem이 InRoom을 보고 BeginRoom).</summary>
        internal void StartForcedBattle()
        {
            if (Current == null) return;
            Phase = RunPhase.InRoom;
        }

        // ───────── 체력 비율 도우미(비율을 고치기 전엔 반드시 EnsurePartyHp) ─────────

        /// <summary>PartyHp가 파티 수보다 짧으면 1f(가득)로 채운다. "비어 있으면 전원 가득" 규칙과 같은 뜻.</summary>
        internal void EnsurePartyHp()
        {
            while (PartyHp.Count < PartySize) PartyHp.Add(1f);
        }

        internal bool IsAlive(int slot) => slot >= 0 && slot < PartySize && (slot >= PartyHp.Count || PartyHp[slot] > 0f);
        internal bool AnyFallenSlot() => AnyFallen();
        internal bool AnyHurtOrFallen() => AnyHurt() || AnyFallen();

        /// <summary>살아 있는 전원 체력 비율 −ratio(쓰러진 슬롯은 그대로, 바닥 MinHpAfterCost).</summary>
        internal void HurtLiving(float ratio)
        {
            EnsurePartyHp();
            for (int i = 0; i < PartyHp.Count; i++)
                if (PartyHp[i] > 0f) PartyHp[i] = System.MathF.Max(MinHpAfterCost, PartyHp[i] - ratio);
        }

        /// <summary>그 슬롯만 −ratio(살아 있을 때만, 바닥 MinHpAfterCost).</summary>
        internal void HurtSlot(int slot, float ratio)
        {
            EnsurePartyHp();
            if (slot < 0 || slot >= PartyHp.Count || PartyHp[slot] <= 0f) return;
            PartyHp[slot] = System.MathF.Max(MinHpAfterCost, PartyHp[slot] - ratio);
        }

        /// <summary>살아 있는 전원 +ratio(최대 1). fallenTo가 0보다 크면 쓰러진 슬롯은 그 비율로 일어난다.</summary>
        internal void HealLiving(float ratio, float fallenTo = 0f)
        {
            EnsurePartyHp();
            for (int i = 0; i < PartyHp.Count; i++)
            {
                if (PartyHp[i] > 0f) PartyHp[i] = System.MathF.Min(1f, PartyHp[i] + ratio);
                else if (fallenTo > 0f) PartyHp[i] = fallenTo;
            }
        }

        /// <summary>그 슬롯만 +ratio(살아 있을 때만, 최대 1).</summary>
        internal void HealSlot(int slot, float ratio)
        {
            EnsurePartyHp();
            if (slot < 0 || slot >= PartyHp.Count || PartyHp[slot] <= 0f) return;
            PartyHp[slot] = System.MathF.Min(1f, PartyHp[slot] + ratio);
        }

        /// <summary>쓰러진 파티원 중 앞 슬롯 하나를 ratio로 일으킨다. 없으면 false.</summary>
        internal bool ReviveFirstFallen(float ratio)
        {
            EnsurePartyHp();
            for (int i = 0; i < PartyHp.Count; i++)
                if (PartyHp[i] <= 0f) { PartyHp[i] = ratio; return true; }
            return false;
        }

        /// <summary>살아 있는 슬롯 중 최저 레벨(동률은 앞). 없으면 -1.</summary>
        internal int LowestLevelLivingSlot()
        {
            int best = -1, bestLv = int.MaxValue;
            for (int s = 0; s < PartySize; s++)
            {
                if (!IsAlive(s)) continue;
                int lv = LevelOf(s);
                if (lv < bestLv) { bestLv = lv; best = s; }
            }
            return best;
        }

        // ───────── 특성·유물 도우미 ─────────

        /// <summary>그 파티원 특성 3택1을 바로 건다(AddXp와 같은 규칙: 교체 특성은 한 번만). 고를 게 없으면 false.</summary>
        internal bool OfferTrait(int slot)
        {
            if (slot < 0 || slot >= Members.Count) return false;
            var owned = new List<string>(TraitsOf(slot));
            foreach (var p in PendingTraits) if (p.Slot == slot) owned.AddRange(p.Options.FindAll(o => TraitPool.Find(o)?.SkillSwap == true));
            var pick = TraitPool.Roll(Members[slot], owned, slot, LevelOf(slot), _rng);
            if (pick.Options.Count == 0) return false;
            PendingTraits.Add(pick);
            return true;
        }

        internal bool HasRelic(string id)
        {
            foreach (var r in Relics) if (r.Id == id) return true;
            return false;
        }

        /// <summary>유물을 넣는다(null·중복이면 false). 효과는 다음 전투 시작 때 BuildModifiers로 반영.</summary>
        internal bool GiveRelic(CardDef c)
        {
            if (c == null || Relics.Contains(c)) return false;
            Relics.Add(c);
            return true;
        }

        /// <summary>안 가진 저주 유물 하나를 받는다(없으면 null).</summary>
        internal CardDef TakeCursedRelic()
        {
            var c = RelicPool.RollCursed(this, _rng);
            if (c != null) Relics.Add(c);
            return c;
        }

        /// <summary>가장 최근에 얻은 저주 아닌 유물의 번호(없으면 -1).</summary>
        internal int LastPlainRelicIndex()
        {
            for (int i = Relics.Count - 1; i >= 0; i--) if (!Relics[i].Cursed) return i;
            return -1;
        }

        /// <summary>안 가진 저주 유물이 남아 있나(난수 없이 검사 — Can 판정용).</summary>
        internal static bool HasCursedLeft(RunData run)
        {
            foreach (var r in RelicPool.All) if (r.Cursed && !run.Relics.Contains(r)) return true;
            return false;
        }

        /// <summary>안 가진 일반 유물(저주·이벤트 전용 아님)이 남아 있나(난수 없이 검사).</summary>
        internal static bool HasPlainRelicLeft(RunData run)
        {
            foreach (var r in RelicPool.All) if (r.Flags == RelicFlags.None && !run.Relics.Contains(r)) return true;
            return false;
        }

        // ───────── 저장(추가형: 옛 저장은 빈 값으로 읽힌다) ─────────

        partial void SaveEvents(RunSave s)
        {
            s.PendingEvent = PendingEvent?.Id ?? "";
            s.SeenEvents.Clear();
            s.SeenEvents.AddRange(SeenEvents);
        }

        partial void LoadEvents(RunSave s)
        {
            PendingEvent = EventPool.Find(s.PendingEvent);
            if (s.SeenEvents != null)
                foreach (var id in s.SeenEvents)
                    if (!string.IsNullOrEmpty(id) && !SeenEvents.Contains(id)) SeenEvents.Add(id);
        }
    }
}
