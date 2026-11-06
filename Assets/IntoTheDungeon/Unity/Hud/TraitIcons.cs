using System.Collections.Generic;
using UnityEngine;
using IntoTheDungeon.Features.Run;

namespace IntoTheDungeon.Unity.Hud
{
    /// <summary>
    /// 특성 아이콘(Resources/TraitIcons.png, 코드로 그린 16px 도트 한 줄 시트). 칸 순서는 아래 Order(시트와 같아야 함).
    /// TraitDef.Icon(id)으로 고르고, Icon이 비어 있는 기존 특성은 Id 표로 메운다(relics-traits가 Icon을 채우면 그쪽이 우선).
    /// </summary>
    public static class TraitIcons
    {
        public const string Hp = "hp", Atk = "atk", Aspd = "aspd", Cd = "cd", Stop = "stop", Wil = "wil", Venom = "venom", Hunt = "hunt",
            Leech = "leech", Arcane = "arcane", Shield = "shield", Bow = "bow", Blade = "blade", Swap = "swap", Thorns = "thorns",
            Mana = "mana", Scholar = "scholar", Berserk = "berserk", Rare = "rare";
        static readonly string[] Order = { Hp, Atk, Aspd, Cd, Stop, Wil, Venom, Hunt, Leech, Arcane, Shield, Bow, Blade, Swap, Thorns, Mana, Scholar, Berserk, Rare };

        // Icon이 비어 있는 특성의 기본 칸(특성 Id → 아이콘 id)
        static readonly Dictionary<string, string> s_byTraitId = new()
        {
            ["t_hp"] = Hp, ["t_dmg"] = Atk, ["t_aspd"] = Aspd, ["t_cd"] = Cd, ["t_stop"] = Stop, ["t_wil"] = Wil,
            ["t_venom"] = Venom, ["t_hunt"] = Hunt, ["t_leech"] = Leech, ["t_arcane"] = Arcane, ["t_shield"] = Shield,
            ["t_power"] = Bow, ["t_blade"] = Blade,
        };
        static readonly Dictionary<string, Sprite> s_cache = new();
        static Texture2D s_sheet;

        public static Sprite Of(string id)
        {
            if (id == null) return null;
            if (s_cache.TryGetValue(id, out var sp) && sp) return sp;
            if (!s_sheet)
            {
                s_sheet = Resources.Load<Texture2D>("TraitIcons");
                if (!s_sheet) return null;
                s_sheet.filterMode = FilterMode.Point;
            }
            int i = System.Array.IndexOf(Order, id);
            if (i < 0 || (i + 1) * 16 > s_sheet.width) return null;
            sp = Sprite.Create(s_sheet, new Rect(i * 16, 0, 16, 16), new Vector2(0.5f, 0.5f), 16f);
            s_cache[id] = sp;
            return sp;
        }

        /// <summary>그 특성의 아이콘: def.Icon → 기존 Id 표 → 교체 특성은 swap, 희귀는 rare. 없으면 null.</summary>
        public static Sprite For(TraitDef def)
        {
            if (def == null) return null;
            string id = def.Icon;
            if (string.IsNullOrEmpty(id) && def.Id != null) s_byTraitId.TryGetValue(def.Id, out id);
            if (string.IsNullOrEmpty(id)) id = def.SkillSwap ? Swap : def.Rare ? Rare : null;
            return Of(id);
        }
    }
}
