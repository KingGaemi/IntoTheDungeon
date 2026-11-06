using System.Collections.Generic;
using UnityEngine;

namespace IntoTheDungeon.Unity.Hud
{
    /// <summary>
    /// 상태 아이콘(Resources/StatusIcons.png, 코드로 그린 16px 도트 한 줄 시트). 순서 = Order(= make_statusicons.py의 ORDER).
    /// 머리 위 상태 줄(StatusIconRenderer)과 UI 공용. 월드용은 ppu 80(16px → 0.2유닛), UI용은 ppu 16.
    /// </summary>
    public static class StatusIcons
    {
        public const string Barrier = "barrier", Poison = "poison", Guard = "guard", Enrage = "enrage",
            Taunt = "taunt", Groggy = "groggy", Haste = "haste", Slow = "slow";
        public static readonly string[] Order = { Barrier, Poison, Guard, Enrage, Taunt, Groggy, Haste, Slow };

        public const float WorldPpu = 80f;   // 월드: 16px → 0.2유닛
        const float UiPpu = 16f;

        static readonly Dictionary<string, Sprite> s_ui = new(), s_world = new();
        static Texture2D s_sheet;

        /// <summary>UI(Image)용. 없으면 null.</summary>
        public static Sprite Of(string id) => Get(id, UiPpu, s_ui);

        /// <summary>월드 SpriteRenderer용(0.2유닛). 없으면 null.</summary>
        public static Sprite World(string id) => Get(id, WorldPpu, s_world);

        static Sprite Get(string id, float ppu, Dictionary<string, Sprite> cache)
        {
            if (id == null) return null;
            if (cache.TryGetValue(id, out var sp) && sp) return sp;
            if (!s_sheet)
            {
                s_sheet = Resources.Load<Texture2D>("StatusIcons");
                if (!s_sheet) return null;
                s_sheet.filterMode = FilterMode.Point;
            }
            int i = System.Array.IndexOf(Order, id);
            if (i < 0 || (i + 1) * 16 > s_sheet.width) return null;   // 시트가 코드보다 짧으면(옛 PNG) 빈 칸
            sp = Sprite.Create(s_sheet, new Rect(i * 16, 0, 16, 16), new Vector2(0.5f, 0.5f), ppu);
            cache[id] = sp;
            return sp;
        }
    }
}
