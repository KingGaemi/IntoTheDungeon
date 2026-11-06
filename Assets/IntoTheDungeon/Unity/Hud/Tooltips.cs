using UnityEngine;
using IntoTheDungeon.Features.Attack;
using IntoTheDungeon.Features.Battle;
using IntoTheDungeon.Features.Character;
using IntoTheDungeon.Features.Run;

namespace IntoTheDungeon.Unity.Hud
{
    /// <summary>툴팁 내용 팩토리(유물·아이템·특성·잼·능력치·필살기·단순). map-tooltip 트랙이 Room/Enemy를 덧붙인다.</summary>
    public static class Tooltips
    {
        const string CursedTag = " · <color=#FF6B61>저주</color>";   // 빨강 = 부정(저주)

        public static TooltipContent Relic(CardDef relic)
        {
            if (relic == null) return null;
            var rim = relic.Cursed ? UiKit.Cursed : UiKit.Rarity(relic.Rarity);
            return new TooltipContent
            {
                Title = relic.Title, TitleColor = UiKit.Rarity(relic.Rarity),
                Icon = RelicIcons.Of(relic.Id),
                Kind = $"유물 · {ItemDef.RarityLabel(relic.Rarity)}" + (relic.Cursed ? CursedTag : ""),
                Body = StopPowerUi.Highlight(relic.Description),
                Rim = rim
            };
        }

        public static TooltipContent Item(ItemDef def)
        {
            if (def == null) return null;
            var c = UiKit.Rarity(def.Rarity);
            return new TooltipContent
            {
                Title = def.Name, TitleColor = c,
                Icon = ItemIconTable.Icon(def.Id),
                Kind = $"아이템 · {ItemDef.RarityLabel(def.Rarity)} · {def.W}×{def.H}",
                Body = StopPowerUi.Highlight(def.Description),
                Rim = c
            };
        }

        public static TooltipContent Trait(TraitDef def)
        {
            if (def == null) return null;
            var c = def.Rare ? UiKit.Rarity(ItemRarity.Epic) : UiKit.TextMain;
            return new TooltipContent
            {
                Title = def.Title, TitleColor = c,
                Kind = def.Rare ? "특성 ★" : def.SkillSwap ? "필살기 교체" : "특성",
                Body = StopPowerUi.Highlight(def.Description),
                Rim = def.Rare ? UiKit.Rarity(ItemRarity.Epic) : UiKit.RimDim
                // Icon: TraitIcons(camp-premium 트랙)가 생기면 def.Icon으로 채운다
            };
        }

        public static TooltipContent Gem(GemDef gem, int level)
        {
            if (gem == null) return null;
            var c = new Color(gem.R, gem.G, gem.B, 1f);
            return new TooltipContent
            {
                Title = gem.Name, TitleColor = c,
                Kind = $"잼 Lv{level}",
                Body = StopPowerUi.Highlight(gem.Describe(level)),
                Rim = c
            };
        }

        public static TooltipContent Stat(string name, string tip, Sprite icon)
            => new() { Title = name, Icon = icon, Body = StopPowerUi.Highlight(tip), Rim = UiKit.RimDim };

        /// <summary>필살기: 이름·설명 + 숫자 줄(저지력(0이면 생략)·마나·대기 초·×배율).</summary>
        public static TooltipContent Skill(in SpecialSkillComponent sk, SkillPreset preset)
        {
            var c = new TooltipContent
            {
                Title = SkillNames.Of(preset), TitleColor = sk.Magic ? UiKit.Magic : UiKit.TextMain,
                Kind = sk.Magic ? "필살기 · 마법" : "필살기",
                Body = StopPowerUi.Highlight(SkillNames.Describe(preset)),
                Rim = sk.Magic ? UiKit.Magic : UiKit.RimDim
            };
            if (sk.StopPower > 0) c.Stats.Add((StopPowerUi.Icon, sk.StopPower.ToString(), UiKit.Stop));
            c.Stats.Add((UiIcons.Of(UiIcons.Mana), $"{sk.ManaCost:0}", UiKit.Mana));
            c.Stats.Add((null, $"{sk.Cooldown:0.#}초", UiKit.TextDim));
            if (sk.DamageMultiplier > 0f) c.Stats.Add((UiIcons.Of(UiIcons.Atk), $"×{sk.DamageMultiplier:0.#}", UiKit.TextMain));
            return c;
        }

        public static TooltipContent Simple(string title, string body, Color? titleColor = null, Sprite icon = null)
            => new() { Title = title, TitleColor = titleColor ?? UiKit.TextMain, Body = StopPowerUi.Highlight(body), Icon = icon, Rim = UiKit.RimDim };

        // ───────── map-tooltip 트랙 추가분: 지도 방 · 배치 화면 적 ─────────

        /// <summary>
        /// 지도 방: 종류·층·상태(현재/지나옴/다음) + 숫자(정예: 처치 금화 ×1.3·보상 티어 +1·정예 잼, 보스: 내려찍기 요구 저지력·보스 잼).
        /// 적 구성은 비공개 — 동료가 정해진 물음표 방도 일반 「?」와 똑같이 보인다.
        /// </summary>
        public static TooltipContent Room(MapNode node, RunData run)
        {
            if (node == null) return null;
            var tint = MapIcons.Tint(node.Type);
            string state = run == null ? "" : node == run.Current ? " · 현재" : node.Visited ? " · 지나옴" : run.CanEnter(node) ? " · 다음" : "";
            var c = new TooltipContent
            {
                Title = Encounters.Label(node.Type), TitleColor = Color.Lerp(tint, Color.white, 0.5f),
                Icon = MapIcons.ForRoom(node.Type),
                Kind = $"{node.Layer + 1}층{state}",
                Rim = tint
            };
            switch (node.Type)
            {
                case RoomType.Elite:
                    c.Stats.Add((RelicIcons.Of("coin"), "×1.3", UiKit.Gold));           // 정예 처치 골드(KillRewardSystem.EliteGoldMul과 같은 값, 임시값)
                    c.Stats.Add((UiIcons.Of(UiIcons.Relic), "+1", UiKit.Positive));    // 보상 티어 +1
                    AddGem(c, Encounters.EliteGem);
                    break;
                case RoomType.Boss:
                    c.Stats.Add((StopPowerUi.Icon, Boss.StopRequired.ToString(), UiKit.Stop));   // 보스 내려찍기 요구 저지력
                    if (run != null) AddGem(c, Encounters.BossGemOf(run.Stage));
                    break;
            }
            return c;
        }

        // 잼 이름은 길어서(「망자의 왕관석」) Stats 칸 대신 Body 한 줄에 잼 색으로(아이콘은 지도 배지가 보여 준다)
        static void AddGem(TooltipContent c, string gemId)
        {
            var gem = GemPool.Find(gemId);
            if (gem == null) return;
            string line = $"<color=#{ColorUtility.ToHtmlStringRGB(new Color(gem.R, gem.G, gem.B, 1f))}>{gem.Name}</color>";
            c.Body = string.IsNullOrEmpty(c.Body) ? line : c.Body + "\n" + line;
        }

        /// <summary>
        /// 배치 화면 적: 이름(보스는 금색) + 패턴 이름·요구 저지력(partyStop ≥ 0이면 파티 최고 저지력과 비교해 청록/빨강) + 되살아남 + 특성 한 줄.
        /// 새 PatternKind 값이 와도 예외 없이 "패턴"으로 표기한다.
        /// </summary>
        public static TooltipContent Enemy(CharacterDefinition def, bool boss, int partyStop = -1)
        {
            var c = new TooltipContent
            {
                Title = string.IsNullOrEmpty(def.DisplayName) ? (boss ? "보스" : "적") : def.DisplayName,
                TitleColor = boss ? UiKit.Gold : UiKit.TextMain,
                Kind = boss ? "보스" : def.Passive ? "적 · 고정" : def.Ranged ? "적 · 원거리" : "적 · 근접",
                Rim = boss ? UiKit.Gold : UiKit.RimDim
            };
            var body = new System.Text.StringBuilder();
            if (boss) AddPattern(c, body, "내려찍기", Boss.StopRequired, partyStop);   // 보스는 Promote가 내려찍기로 덮어쓴다
            else if (def.SlamRadius > 0f)
            {
                AddPattern(c, body, PatternName(def.SlamKind), def.SlamStopRequired, partyStop);
                if (def.SlamAltPower > 0f) AddPattern(c, body, PatternName(def.SlamAltKind), def.SlamStopRequired, partyStop);
            }
            if (def.ReviveCharges > 0)
            {
                bool ok = partyStop < 0 || partyStop >= def.ReviveStopRequired;
                c.Stats.Add((UiIcons.Of(UiIcons.Revive), $"×{def.ReviveCharges}", ok ? UiKit.Stop : UiKit.Negative));
                Line(body).Append($"되살아남 ×{def.ReviveCharges} · 분쇄 {StopText(def.ReviveStopRequired, partyStop)}");
            }
            if (!string.IsNullOrEmpty(def.TraitText)) Line(body).Append(StopPowerUi.Highlight(def.TraitText));
            c.Body = body.ToString();
            return c;
        }

        static System.Text.StringBuilder Line(System.Text.StringBuilder sb) => sb.Length > 0 ? sb.Append('\n') : sb;

        static void AddPattern(TooltipContent c, System.Text.StringBuilder body, string name, int required, int partyStop)
        {
            if (required > 0 && c.Stats.Count == 0)
                c.Stats.Add((StopPowerUi.Icon, required.ToString(), partyStop < 0 || partyStop >= required ? UiKit.Stop : UiKit.Negative));
            Line(body).Append(required > 0 ? $"{name} · {StopText(required, partyStop)}" : name);
        }

        /// <summary>"저지력 N": 충분하면 청록, 모자라면 빨강(비교값이 없으면 청록).</summary>
        static string StopText(int required, int partyStop)
            => $"<color={(partyStop < 0 || partyStop >= required ? StopPowerUi.Hex : StopPowerUi.ShortHex)}>저지력 {required}</color>";

        static string PatternName(PatternKind kind) => kind switch
        {
            PatternKind.Slam => "강타",
            PatternKind.Heal => "치유 의식",
            PatternKind.Hatch => "부화",
            PatternKind.Barrier => "마법 보호막",
            PatternKind.Charge => "돌진",
            _ => "패턴"
        };
    }
}
