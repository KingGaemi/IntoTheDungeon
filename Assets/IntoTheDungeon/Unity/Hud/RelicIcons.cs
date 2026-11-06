using System.Collections.Generic;
using UnityEngine;

namespace IntoTheDungeon.Unity.Hud
{
    /// <summary>
    /// 유물 아이콘(Resources/RelicIcons.png, 코드로 그린 16px 도트 한 줄 시트). 순서는 아래 Order(시트 칸 순서와 같아야 함).
    /// 시트 생성: scratchpad/tools/gen/make_relics.py(ORDER 배열이 이 Order와 같아야 한다). Order.Length == RelicPool.All.Length.
    /// </summary>
    public static class RelicIcons
    {
        static readonly string[] Order = { "str", "agi", "int", "wil", "firstaid", "focus", "bulk", "coin", "boots",
                                           "crest", "fang", "heart", "anvil", "horn", "titan", "phoenix",
                                           "warcry", "axe", "quill", "spikes", "echo", "ember", "chalice",
                                           "venom", "hunt", "oath", "rot", "rune",
                                           // 확장 13칸(갈래 7 + 저주 5 + 이벤트 전용 1)
                                           "plague", "nightshade", "siphon", "bramble", "wellspring", "ledger", "tome",
                                           "bloodpact", "greed", "leadboots", "glasscannon", "rustyanvil", "shrinemark" };

        /// <summary>시트 칸 수(검증용).</summary>
        public static int Count => Order.Length;
        static readonly Dictionary<string, Sprite> s_cache = new();
        static Texture2D s_sheet;

        public static Sprite Of(string id)
        {
            if (id == null) return null;
            if (s_cache.TryGetValue(id, out var sp) && sp) return sp;
            if (!s_sheet)
            {
                s_sheet = Resources.Load<Texture2D>("RelicIcons");
                if (!s_sheet) return null;
                s_sheet.filterMode = FilterMode.Point;
            }
            int i = System.Array.IndexOf(Order, id);
            if (i < 0) return null;
            sp = Sprite.Create(s_sheet, new Rect(i * 16, 0, 16, 16), new Vector2(0.5f, 0.5f), 16f);
            s_cache[id] = sp;
            return sp;
        }
    }
}
