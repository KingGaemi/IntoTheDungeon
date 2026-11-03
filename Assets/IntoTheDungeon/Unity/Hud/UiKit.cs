using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace IntoTheDungeon.Unity.Hud
{
    /// <summary>
    /// 공용 UI 스타일(코드로 그린 9-slice): 둥근 모서리 + 위가 살짝 밝은 그라데이션 바탕, 청동 테두리(안쪽 어두운 선),
    /// 아래로 부드러운 그림자. 납작한 단색 네모 대신 이걸 입혀 화면을 고급스럽게 맞춘다.
    /// 쓰는 법: UiKit.Panel(img) / UiKit.Card(img, 테두리색) / UiKit.Button(btn, img, 바탕색).
    /// </summary>
    public static class UiKit
    {
        // 팔레트: 어두운 남색 바탕 + 청동·금 테두리
        public static readonly Color PanelFill = new(0.075f, 0.08f, 0.115f, 0.97f);
        public static readonly Color CardFill = new(0.13f, 0.14f, 0.2f, 1f);
        public static readonly Color Rim = new(0.78f, 0.62f, 0.36f, 1f);
        public static readonly Color RimDim = new(0.45f, 0.4f, 0.33f, 1f);
        public static readonly Color Gold = new(1f, 0.84f, 0.42f, 1f);
        public static readonly Color TextMain = new(0.93f, 0.91f, 0.86f, 1f);
        public static readonly Color TextDim = new(0.93f, 0.91f, 0.86f, 0.6f);

        static Sprite s_fill, s_rim, s_glow;

        /// <summary>둥근 바탕(위가 밝고 아래가 어두운 그라데이션, 흰색 → Image.color로 물들임). 9-slice.</summary>
        public static Sprite FillSprite { get { if (!s_fill) Make(); return s_fill; } }
        /// <summary>둥근 테두리(2px 밝은 선 + 안쪽 1px 어두운 선). 9-slice.</summary>
        public static Sprite RimSprite { get { if (!s_rim) Make(); return s_rim; } }
        /// <summary>부드러운 빛 번짐(선택·강조용). 9-slice.</summary>
        public static Sprite GlowSprite { get { if (!s_glow) Make(); return s_glow; } }

        const int N = 48, R = 10, Border = 16;

        static void Make()
        {
            var fill = NewTex(); var rim = NewTex(); var glow = NewTex();
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float d = RoundRectDist(x + 0.5f, y + 0.5f, N, N, R);   // 안쪽 음수, 가장자리 0
                    float inside = Mathf.Clamp01(0.5f - d);
                    // 위쪽 밝게(위 끝 1px은 더 밝은 하이라이트)
                    float v = (y + 0.5f) / N;
                    float shade = Mathf.Lerp(0.78f, 1f, v);
                    if (d > -2.2f && d < -1.2f && v > 0.5f) shade = 1.18f;
                    fill.SetPixel(x, y, new Color(Mathf.Min(1f, shade), Mathf.Min(1f, shade), Mathf.Min(1f, shade), inside));
                    // 테두리: 바깥 2px 밝은 선 + 그 안 1px 어두운 선
                    float a = 0f; float c = 1f;
                    if (d > -2f && d <= 0.5f) { a = Mathf.Clamp01(0.5f - d) * Mathf.Clamp01(d + 2.5f); c = 1f; }
                    else if (d > -3.2f && d <= -2f) { a = 0.55f; c = 0f; }
                    rim.SetPixel(x, y, new Color(c, c, c, a));
                    // 빛 번짐: 가장자리에서 안쪽으로 옅어짐
                    float g = d > 0.5f ? 0f : Mathf.Clamp01(1f + d / 8f);
                    glow.SetPixel(x, y, new Color(1f, 1f, 1f, g * g));
                }
            fill.Apply(); rim.Apply(); glow.Apply();
            var border = new Vector4(Border, Border, Border, Border);
            s_fill = Sprite.Create(fill, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            s_rim = Sprite.Create(rim, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            s_glow = Sprite.Create(glow, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }

        static Texture2D NewTex() => new(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };

        // 둥근 사각형까지의 거리(안쪽 음수)
        static float RoundRectDist(float x, float y, float w, float h, float r)
        {
            float qx = Mathf.Abs(x - w * 0.5f) - (w * 0.5f - r);
            float qy = Mathf.Abs(y - h * 0.5f) - (h * 0.5f - r);
            float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        /// <summary>큰 판(화면 구역): 어두운 바탕 + 청동 테두리 + 그림자.</summary>
        public static Image Panel(Image img, Color? fill = null, Color? rim = null)
            => Style(img, fill ?? PanelFill, rim ?? Rim, shadow: true);

        /// <summary>판 안의 카드·타일: 조금 밝은 바탕 + 흐린 테두리(등급 색 등을 줄 수 있음).</summary>
        public static Image Card(Image img, Color? rim = null, Color? fill = null)
            => Style(img, fill ?? CardFill, rim ?? RimDim, shadow: false);

        /// <summary>버튼: 바탕색 그라데이션 + 밝은 테두리 + 그림자, 마우스 올리면 밝게·누르면 어둡게.</summary>
        public static Button Button(Button btn, Image img, Color fill, Color? rim = null)
        {
            Style(img, fill, rim ?? Color.Lerp(fill, Color.white, 0.45f), shadow: true);
            btn.targetGraphic = img;
            var cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
            cb.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            cb.selectedColor = Color.white;
            cb.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.7f);
            cb.colorMultiplier = 1.15f;
            cb.fadeDuration = 0.08f;
            btn.colors = cb;
            return btn;
        }

        /// <summary>이미 만든 Image에 둥근 바탕·테두리를 입힌다(테두리는 자식 이미지 "Rim", 다시 부르면 색만 바꿈).</summary>
        public static Image Style(Image img, Color fill, Color rim, bool shadow)
        {
            img.sprite = FillSprite;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1f;
            img.color = fill;
            var rimImg = RimOf(img);
            if (!rimImg)
            {
                var go = new GameObject("Rim", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(img.transform, false);
                go.transform.SetAsFirstSibling();   // 글자·아이콘보다 뒤
                var rt = (RectTransform)go.transform;
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
                rimImg = go.GetComponent<Image>();
                rimImg.sprite = RimSprite;
                rimImg.type = Image.Type.Sliced;
                rimImg.raycastTarget = false;
            }
            rimImg.color = rim;
            if (shadow && !img.GetComponent<Shadow>())
            {
                var sh = img.gameObject.AddComponent<Shadow>();
                sh.effectColor = new Color(0f, 0f, 0f, 0.55f);
                sh.effectDistance = new Vector2(0f, -5f);
            }
            return img;
        }

        /// <summary>테두리 색만 바꾼다(등급 색·선택 강조 등).</summary>
        public static void SetRim(Image img, Color rim)
        {
            var r = RimOf(img);
            if (r) r.color = rim;
        }

        static Image RimOf(Image img)
        {
            var t = img.transform.Find("Rim");
            return t ? t.GetComponent<Image>() : null;
        }

        /// <summary>글자에 부드러운 그림자(외곽선보다 덜 거칠게).</summary>
        public static Text Soft(Text t, float alpha = 0.8f)
        {
            if (!t.GetComponent<Shadow>())
            {
                var sh = t.gameObject.AddComponent<Shadow>();
                sh.effectColor = new Color(0f, 0f, 0f, alpha);
                sh.effectDistance = new Vector2(1.5f, -2f);
            }
            return t;
        }
    }

    /// <summary>글자 대신 쓰는 UI 아이콘(Resources/UiIcons.png, 코드로 그린 16px 도트 한 줄 시트).</summary>
    public static class UiIcons
    {
        public const string Hp = "hp", Atk = "atk", Aspd = "aspd", Def = "def", Spd = "spd", Mana = "mana", Regen = "regen",
            MRes = "mres", Res = "res", Str = "str", Agi = "agi", Int = "int", Wil = "wil", Xp = "xp", Cart = "cart",
            Heal = "heal", Revive = "revive", Recruit = "recruit", Relic = "relic", Item = "item", LvUp = "lvup";
        static readonly string[] Order = { Hp, Atk, Aspd, Def, Spd, Mana, Regen, MRes, Res, Str, Agi, Int, Wil, Xp, Cart, Heal, Revive, Recruit, Relic, Item, LvUp };
        static readonly Dictionary<string, Sprite> s_cache = new();
        static Texture2D s_sheet;

        public static Sprite Of(string id)
        {
            if (id == null) return null;
            if (s_cache.TryGetValue(id, out var sp) && sp) return sp;
            if (!s_sheet)
            {
                s_sheet = Resources.Load<Texture2D>("UiIcons");
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

    /// <summary>마우스를 올리면/내리면 알려 준다(아이콘 설명을 안내 칸에 띄우는 데 씀).</summary>
    public sealed class HoverHook : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public System.Action Enter, Exit;
        public void OnPointerEnter(PointerEventData e) => Enter?.Invoke();
        public void OnPointerExit(PointerEventData e) => Exit?.Invoke();

        public static HoverHook On(GameObject go, System.Action enter, System.Action exit = null)
        {
            var h = go.GetComponent<HoverHook>();
            if (!h) h = go.AddComponent<HoverHook>();   // Unity 객체는 ?? 로 빈 값 판별이 안 됨
            h.Enter = enter;
            h.Exit = exit;
            var g = go.GetComponent<Graphic>();
            if (g) g.raycastTarget = true;
            return h;
        }
    }
}
