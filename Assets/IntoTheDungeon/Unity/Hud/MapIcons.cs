using System.Collections.Generic;
using UnityEngine;
using IntoTheDungeon.Features.Run;

namespace IntoTheDungeon.Unity.Hud
{
    /// <summary>
    /// 지도 아이콘(Resources/MapIcons.png, 코드로 그린 16px 도트 한 줄 시트). 순서 = Order(= make_mapicons.py의 ORDER).
    /// 갈림길 지도 원형 노드 가운데·방 툴팁에 쓴다. 방 종류 색(Tint)도 여기 두어 지도와 툴팁이 같은 색을 쓴다.
    /// </summary>
    public static class MapIcons
    {
        public const string Battle = "battle", Elite = "elite", Rest = "rest", Boss = "boss", Shop = "shop", Event = "event",
            Skull = "skull", Crown = "crown";
        public static readonly string[] Order = { Battle, Elite, Rest, Boss, Shop, Event, Skull, Crown };

        const float UiPpu = 16f;
        static readonly Dictionary<string, Sprite> s_cache = new();
        static Texture2D s_sheet;

        /// <summary>UI(Image)용. 시트가 없거나 칸이 모자라면 null.</summary>
        public static Sprite Of(string id)
        {
            if (id == null) return null;
            if (s_cache.TryGetValue(id, out var sp) && sp) return sp;
            if (!s_sheet)
            {
                s_sheet = Resources.Load<Texture2D>("MapIcons");
                if (!s_sheet) return null;
                s_sheet.filterMode = FilterMode.Point;
            }
            int i = System.Array.IndexOf(Order, id);
            if (i < 0 || (i + 1) * 16 > s_sheet.width) return null;   // 시트가 코드보다 짧으면(옛 PNG) 빈 칸
            sp = Sprite.Create(s_sheet, new Rect(i * 16, 0, 16, 16), new Vector2(0.5f, 0.5f), UiPpu);
            s_cache[id] = sp;
            return sp;
        }

        /// <summary>방 종류 → 아이콘 id. 동료가 정해진 물음표 방도 그냥 물음표(들어가야 안다).</summary>
        public static string IdOf(RoomType type) => type switch
        {
            RoomType.Elite => Elite,
            RoomType.Rest => Rest,
            RoomType.Boss => Boss,
            RoomType.Shop => Shop,
            RoomType.Event => Event,
            _ => Battle
        };

        public static Sprite ForRoom(RoomType type) => Of(IdOf(type));

        /// <summary>방 종류 색(지도 노드 바탕·툴팁 테두리 공용).</summary>
        public static Color Tint(RoomType type) => type switch
        {
            RoomType.Elite => new Color(0.55f, 0.32f, 0.7f, 1f),
            RoomType.Rest => new Color(0.3f, 0.6f, 0.4f, 1f),
            RoomType.Boss => new Color(0.85f, 0.65f, 0.2f, 1f),
            RoomType.Shop => new Color(0.2f, 0.52f, 0.62f, 1f),
            RoomType.Event => new Color(0.62f, 0.5f, 0.28f, 1f),
            _ => new Color(0.3f, 0.36f, 0.5f, 1f)
        };

        static Dictionary<string, Sprite> s_gems;

        /// <summary>잼 아이콘(Resources/GemIcons 다중 스프라이트, 이름 = 잼 Id). 없으면 null.</summary>
        public static Sprite Gem(string id)
        {
            if (id == null) return null;
            if (s_gems == null)
            {
                s_gems = new Dictionary<string, Sprite>();
                foreach (var sp in Resources.LoadAll<Sprite>("GemIcons")) s_gems[sp.name] = sp;
            }
            return s_gems.TryGetValue(id, out var s) && s ? s : null;
        }
    }
}
