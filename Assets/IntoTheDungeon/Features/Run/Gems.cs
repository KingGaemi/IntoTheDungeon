using System.Collections.Generic;

namespace IntoTheDungeon.Features.Run
{
    /// <summary>
    /// 잼: 판이 끝나도 남는 수집품. 잼 슬롯에 끼우면 파티 전원에게 효과가 걸린다.
    /// 같은 잼을 모을수록 등급이 오른다(1개 Lv.1 · 3개 Lv.2 · 9개 Lv.3).
    /// </summary>
    public sealed class GemDef
    {
        public readonly string Id;
        public readonly string Name;
        public readonly float R, G, B;                      // 화면 색
        readonly System.Func<int, string> _desc;            // 등급별 설명
        readonly System.Action<PartyModifiers, int> _apply; // 등급별 효과

        public GemDef(string id, string name, (float r, float g, float b) color, System.Func<int, string> desc, System.Action<PartyModifiers, int> apply)
        {
            Id = id; Name = name; (R, G, B) = color; _desc = desc; _apply = apply;
        }

        public string Describe(int level) => _desc(System.Math.Clamp(level, 1, GemPool.MaxLevel));
        public void Apply(PartyModifiers m, int level) => _apply(m, System.Math.Clamp(level, 1, GemPool.MaxLevel));
    }

    /// <summary>잼 목록(임시값). 효과는 등급 1·2·3 순.</summary>
    public static class GemPool
    {
        public const int MaxLevel = 3;
        static readonly int[] Thresholds = { 1, 3, 9 };   // 이 개수가 되면 그 등급

        static float Pick(int level, float a, float b, float c) => level >= 3 ? c : level == 2 ? b : a;

        public static readonly GemDef[] All =
        {
            new("ruby", "루비", (0.9f, 0.2f, 0.25f),
                lv => $"파티 공격력 +{Pick(lv, 8, 15, 25)}%", (m, lv) => m.DamageMul *= 1f + Pick(lv, 8, 15, 25) / 100f),
            new("emerald", "에메랄드", (0.2f, 0.8f, 0.4f),
                lv => $"파티 최대 체력 +{Pick(lv, 10, 20, 30)}%", (m, lv) => m.MaxHpMul *= 1f + Pick(lv, 10, 20, 30) / 100f),
            new("topaz", "토파즈", (1f, 0.75f, 0.2f),
                lv => $"파티 공격 속도 +{Pick(lv, 8, 15, 22)}%", (m, lv) => m.AttackSpeedMul *= 1f + Pick(lv, 8, 15, 22) / 100f),
            new("sapphire", "사파이어", (0.25f, 0.45f, 1f),
                lv => $"필살기 재사용 대기 -{Pick(lv, 10, 18, 25)}%", (m, lv) => m.SkillCooldownMul *= 1f - Pick(lv, 10, 18, 25) / 100f),
            new("amethyst", "자수정", (0.7f, 0.4f, 0.95f),
                lv => $"전투 후 회복 +{Pick(lv, 8, 15, 25)}%p", (m, lv) => m.PostBattleHealAdd += Pick(lv, 8, 15, 25) / 100f),
            new("onyx", "오닉스", (0.55f, 0.55f, 0.6f),
                lv => $"파티 모든 스탯 +{Pick(lv, 1, 2, 4)}", (m, lv) =>
                {
                    int v = (int)Pick(lv, 1, 2, 4);
                    m.StatBonus.Str += v; m.StatBonus.Agi += v; m.StatBonus.Int += v; m.StatBonus.Wil += v;
                }),
            new("citrine", "황수정", (0.95f, 0.85f, 0.3f),
                lv => $"처치 골드 +{Pick(lv, 20, 40, 60)}%", (m, lv) => m.GoldMul *= 1f + Pick(lv, 20, 40, 60) / 100f),
            new("moonstone", "월장석", (0.75f, 0.9f, 1f),
                lv => $"파티 이동 속도 +{Pick(lv, 8, 15, 22)}%", (m, lv) => m.MoveSpeedMul *= 1f + Pick(lv, 8, 15, 22) / 100f),
        };

        public static GemDef Find(string id)
        {
            foreach (var g in All) if (g.Id == id) return g;
            return null;
        }

        public static GemDef Roll(System.Random rng) => All[rng.Next(All.Length)];

        public static int LevelOf(int count)
        {
            int lv = 0;
            for (int i = 0; i < Thresholds.Length; i++) if (count >= Thresholds[i]) lv = i + 1;
            return lv;
        }

        /// <summary>다음 등급까지 필요한 개수(최고 등급이면 0).</summary>
        public static int NextThreshold(int count)
        {
            foreach (int t in Thresholds) if (count < t) return t;
            return 0;
        }
    }

    /// <summary>
    /// 판을 넘어 쌓이는 진행(잼 보유·장착, 잼 슬롯 해금 기록). 파일 저장은 Unity 쪽(MetaStore)이 Version을 보고 한다.
    /// 잼 슬롯: 처음 1칸, 보스 1회 처치 2칸, 보스 3회 처치 3칸, 던전 클리어 1회 4칸.
    /// </summary>
    public sealed class MetaProgress
    {
        public const int MaxSlots = 4;

        public static MetaProgress Current { get; private set; } = new();
        public static void Set(MetaProgress meta) => Current = meta ?? new MetaProgress();

        readonly Dictionary<string, int> _counts = new();
        readonly string[] _slots = new string[MaxSlots];   // 슬롯별 낀 잼 Id(빈 칸 null). 잠긴 슬롯 것은 무시
        public int BossKills { get; private set; }
        public int Clears { get; private set; }
        public int Runs { get; private set; }
        /// <summary>바뀔 때마다 오른다(저장용).</summary>
        public int Version { get; private set; }

        public int SlotCount => 1 + (BossKills >= 1 ? 1 : 0) + (BossKills >= 3 ? 1 : 0) + (Clears >= 1 ? 1 : 0);

        public static string SlotUnlockText(int slot) => slot switch
        {
            1 => "보스 1회 처치",
            2 => "보스 3회 처치",
            3 => "던전 클리어 1회",
            _ => ""
        };

        public int Count(string id) => _counts.TryGetValue(id, out var n) ? n : 0;
        public int Level(string id) => GemPool.LevelOf(Count(id));

        /// <summary>그 슬롯에 낀 잼 Id(비었거나 잠긴 슬롯이면 null).</summary>
        public string InSlot(int slot) => slot >= 0 && slot < SlotCount ? _slots[slot] : null;

        /// <summary>낀 슬롯 번호(안 꼈으면 -1).</summary>
        public int SlotOf(string id)
        {
            for (int i = 0; i < SlotCount; i++) if (_slots[i] == id) return i;
            return -1;
        }

        public bool IsEquipped(string id) => SlotOf(id) >= 0;

        public int EquippedCount
        {
            get { int n = 0; for (int i = 0; i < SlotCount; i++) if (_slots[i] != null) n++; return n; }
        }

        public void AddGem(string id)
        {
            _counts[id] = Count(id) + 1;
            Version++;
        }

        public void AddBossKill() { BossKills++; Version++; }

        public void EndRun(bool cleared)
        {
            Runs++;
            if (cleared) Clears++;
            Version++;
        }

        /// <summary>빈 슬롯(앞에서부터)에 끼운다. 못 끼우면 이유. 이미 끼웠으면 그대로.</summary>
        public string Equip(string id)
        {
            if (IsEquipped(id)) return null;
            for (int i = 0; i < SlotCount; i++)
                if (_slots[i] == null) return PutInSlot(i, id);
            return Count(id) <= 0 ? "아직 없는 잼입니다" : "빈 잼 슬롯이 없습니다";
        }

        /// <summary>
        /// 그 슬롯에 끼운다(끌어다 놓기). 이미 다른 슬롯에 낀 잼이면 두 슬롯을 맞바꾸고,
        /// 그 슬롯에 다른 잼이 있었으면 빠진다(목록으로). 못 끼우면 이유.
        /// </summary>
        public string PutInSlot(int slot, string id)
        {
            if (Count(id) <= 0) return "아직 없는 잼입니다";
            if (slot < 0 || slot >= SlotCount) return "잠긴 잼 슬롯입니다";
            int from = SlotOf(id);
            if (from == slot) return null;
            if (from >= 0) _slots[from] = _slots[slot];   // 맞바꾸기
            _slots[slot] = id;
            Version++;
            return null;
        }

        public void Unequip(string id)
        {
            int i = SlotOf(id);
            if (i < 0) return;
            _slots[i] = null;
            Version++;
        }

        /// <summary>끼운 잼 효과를 파티 보정값에 더한다(RunData.BuildModifiers).</summary>
        public void Apply(PartyModifiers m)
        {
            for (int i = 0; i < SlotCount; i++)
            {
                var gem = GemPool.Find(_slots[i]);
                int lv = _slots[i] != null ? Level(_slots[i]) : 0;
                if (gem != null && lv > 0) gem.Apply(m, lv);
            }
        }

        public MetaSave ToSave()
        {
            var s = new MetaSave { BossKills = BossKills, Clears = Clears, Runs = Runs };
            foreach (var kv in _counts) { s.GemIds.Add(kv.Key); s.GemCounts.Add(kv.Value); }
            foreach (var id in _slots) s.Equipped.Add(id ?? "");   // 슬롯 순서 그대로(빈 칸은 "")
            return s;
        }

        public static MetaProgress FromSave(MetaSave s)
        {
            var m = new MetaProgress();
            if (s == null) return m;
            m.BossKills = s.BossKills; m.Clears = s.Clears; m.Runs = s.Runs;
            for (int i = 0; i < s.GemIds.Count && i < s.GemCounts.Count; i++)
                if (GemPool.Find(s.GemIds[i]) != null) m._counts[s.GemIds[i]] = s.GemCounts[i];
            for (int i = 0; i < s.Equipped.Count && i < MaxSlots; i++)
            {
                var id = s.Equipped[i];
                if (!string.IsNullOrEmpty(id) && m.Count(id) > 0 && System.Array.IndexOf(m._slots, id) < 0) m._slots[i] = id;
            }
            return m;
        }
    }

    [System.Serializable]
    public sealed class MetaSave
    {
        public int BossKills, Clears, Runs;
        public List<string> GemIds = new();
        public List<int> GemCounts = new();
        public List<string> Equipped = new();
    }
}
