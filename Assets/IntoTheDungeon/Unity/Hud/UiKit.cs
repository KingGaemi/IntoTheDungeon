using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using IntoTheDungeon.Features.Run;

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

        // 색 토큰: 빨강 = Negative(부정 신호) 하나뿐. 긍정 강조는 금·청록·연두·파랑
        public static readonly Color Accent = new(1f, 0.82f, 0.3f, 1f);       // 버튼·선택
        public static readonly Color Positive = new(0.55f, 1f, 0.6f, 1f);     // 회복·보너스
        public static readonly Color Negative = new(1f, 0.42f, 0.38f, 1f);    // 부족·위험·대가 전용(= StopPowerUi.ShortColor)
        public static readonly Color Stop = new(0.35f, 0.92f, 0.88f, 1f);     // 저지력(= StopPowerUi.Color)
        public static readonly Color Mana = new(0.3f, 0.55f, 1f, 1f);
        public static readonly Color Xp = new(0.55f, 0.9f, 1f, 1f);
        public static readonly Color Hp = new(0.35f, 0.85f, 0.4f, 1f);
        public static readonly Color EnemyHp = new(0.88f, 0.26f, 0.22f, 1f);
        public static readonly Color Magic = new(0.55f, 0.85f, 1f, 1f);
        public static readonly Color Poison = new(0.75f, 0.45f, 0.95f, 1f);
        public static readonly Color CardHover = new(0.17f, 0.18f, 0.26f, 1f);
        public static readonly Color Dim = new(0f, 0f, 0f, 0.6f);
        public static readonly Color Disabled = new(0.55f, 0.55f, 0.55f, 0.7f);
        public static readonly Color Cursed = new(0.62f, 0.12f, 0.18f, 1f);   // 저주 테두리(진홍 = 부정)

        /// <summary>등급색(아이템·유물 공용. LootRenderer.RarityColor가 여기에 위임).</summary>
        public static Color Rarity(ItemRarity r) => r switch
        {
            ItemRarity.Uncommon => new Color(0.35f, 0.85f, 0.4f, 1f),
            ItemRarity.Rare => new Color(0.3f, 0.6f, 1f, 1f),
            ItemRarity.Epic => new Color(0.72f, 0.42f, 1f, 1f),
            _ => new Color(0.85f, 0.85f, 0.85f, 1f)
        };

        // 타이포 5단계
        public const int TitleSize = 44, HeadSize = 26, BodySize = 20, CaptionSize = 16, NumberSize = 24;

        static Font s_font;
        /// <summary>공용 글꼴(LegacyRuntime.ttf 캐시).</summary>
        public static Font Font
        {
            get
            {
                if (!s_font) s_font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return s_font;
            }
        }

        // ───────── 빌더(새 코드는 이것만 쓴다. 기존 HUD의 복붙 헬퍼는 건드리지 않음) ─────────

        /// <summary>RectTransform + Image. raycastTarget은 기본 꺼짐(버튼·호버 대상만 켠다).</summary>
        public static Image NewImage(Transform parent, Color color, bool raycast = false, string name = "Image")
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        /// <summary>굵은 글자(공용 글꼴, raycast 꺼짐). overflow면 가로·세로 넘침 허용(크기를 안 재도 잘리지 않음).</summary>
        public static Text NewText(Transform parent, string text, int size, Color? color = null, TextAnchor anchor = TextAnchor.MiddleCenter, bool overflow = true)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = Font;
            t.fontStyle = FontStyle.Bold;
            t.fontSize = size;
            t.text = text;
            t.color = color ?? TextMain;
            t.alignment = anchor;
            t.raycastTarget = false;
            t.supportRichText = true;
            if (overflow) { t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow; }
            return t;
        }

        /// <summary>버튼 하나: 바탕(raycast) → Place → Button + 스타일 → 글자(안쪽 여백) Stretch.</summary>
        public static Button NewButton(Transform parent, string label, Vector2 pos, Vector2 size, Color fill, UnityEngine.Events.UnityAction onClick, int fontSize = 24)
        {
            var img = NewImage(parent, fill, raycast: true, name: "Button");
            Place(img.rectTransform, pos, size);
            var btn = img.gameObject.AddComponent<Button>();
            Button(btn, img, fill);
            if (onClick != null) btn.onClick.AddListener(onClick);
            var t = NewText(img.transform, label, fontSize, new Color(0.1f, 0.08f, 0.05f, 1f));
            Stretch(t.rectTransform, 4f);
            return btn;
        }

        /// <summary>anchorMin = anchorMax = pivot = anchor, anchoredPosition = pos, sizeDelta = size.</summary>
        public static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        /// <summary>가운데 앵커로 놓는다.</summary>
        public static void Place(RectTransform rt, Vector2 pos, Vector2 size) => Place(rt, new Vector2(0.5f, 0.5f), pos, size);

        /// <summary>부모에 꽉 채운다(inset만큼 안쪽으로).</summary>
        public static void Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(-inset, -inset);
        }

        /// <summary>화면 캔버스(ScreenSpaceOverlay + 1920×1080 기준 스케일, match 0.5). raycaster가 false면 클릭을 안 받는 순수 표시용.</summary>
        public static Canvas NewCanvas(Transform parent, string name, int sortingOrder, bool raycaster = true)
        {
            var go = raycaster ? new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster))
                               : new GameObject(name, typeof(Canvas), typeof(CanvasScaler));
            if (parent) go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        /// <summary>[아이콘 size+4][글자] 가로 칩. 반환 = 칩 루트(크기·자리는 호출자가 Place). 아이콘이 null이면 글자만.</summary>
        public static RectTransform IconText(Transform parent, Sprite icon, string text, int size, Color color, out Text label)
        {
            var root = new GameObject("Chip", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var rt = (RectTransform)root.transform;
            float iconSize = size + 4;
            float left = 0f;
            if (icon)
            {
                var img = NewImage(root.transform, Color.white, name: "Icon");
                img.sprite = icon;
                img.preserveAspect = true;
                Place(img.rectTransform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(iconSize, iconSize));
                left = iconSize + 4f;
            }
            label = NewText(root.transform, text, size, color, TextAnchor.MiddleLeft);
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(left, 0f); label.rectTransform.offsetMax = Vector2.zero;
            return rt;
        }

        /// <summary>빛 번짐 토글: 자식 "Glow"(GlowSprite, 사방 +12px, raycast 없음, 맨 뒤)를 켜고 끈다. 반환 = glow Image(꺼져 있고 없으면 null).</summary>
        public static Image Glow(Image img, Color c, bool on)
        {
            var t = img.transform.Find("Glow");
            Image glow = t ? t.GetComponent<Image>() : null;
            if (!glow)
            {
                if (!on) return null;
                glow = NewImage(img.transform, c, name: "Glow");
                glow.sprite = GlowSprite;
                glow.type = Image.Type.Sliced;
                glow.transform.SetAsFirstSibling();
                Stretch(glow.rectTransform, -12f);
            }
            glow.color = c;
            glow.gameObject.SetActive(on);
            return glow;
        }

        static Sprite s_circle, s_circleRim;
        const int CircleN = 64;

        /// <summary>64px 부드러운 원(흰색, Image.color로 물들임).</summary>
        public static Sprite CircleSprite { get { if (!s_circle) MakeCircles(); return s_circle; } }
        /// <summary>64px 원 테두리 3px(흰색).</summary>
        public static Sprite CircleRimSprite { get { if (!s_circleRim) MakeCircles(); return s_circleRim; } }

        static void MakeCircles()
        {
            var fill = new Texture2D(CircleN, CircleN, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var rim = new Texture2D(CircleN, CircleN, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            float r = CircleN * 0.5f - 1f, c = CircleN * 0.5f;
            for (int y = 0; y < CircleN; y++)
                for (int x = 0; x < CircleN; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - c) * (x + 0.5f - c) + (y + 0.5f - c) * (y + 0.5f - c)) - r;   // 바깥 양수
                    float a = Mathf.Clamp01(0.5f - d);
                    fill.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    float ring = Mathf.Clamp01(0.5f - d) * Mathf.Clamp01(d + 3.5f);   // 3px 두께
                    rim.SetPixel(x, y, new Color(1f, 1f, 1f, ring));
                }
            fill.Apply(); rim.Apply();
            s_circle = Sprite.Create(fill, new Rect(0, 0, CircleN, CircleN), new Vector2(0.5f, 0.5f), 100f);
            s_circleRim = Sprite.Create(rim, new Rect(0, 0, CircleN, CircleN), new Vector2(0.5f, 0.5f), 100f);
        }

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

        /// <summary>버튼: 바탕색 그라데이션 + 밝은 테두리 + 그림자, 마우스 올리면 밝게·누르면 어둡게. fx면 호버·누름 스케일(ButtonFx)도 붙인다.</summary>
        public static Button Button(Button btn, Image img, Color fill, Color? rim = null, bool fx = true)
        {
            if (fx && !btn.GetComponent<ButtonFx>()) btn.gameObject.AddComponent<ButtonFx>();
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

    /// <summary>
    /// 버튼 호버 1.03·누름 0.97 스케일(unscaled 시간). interactable == false면 스케일 1.
    /// 스케일 연출을 따로 하는 버튼은 UiKit.Button(…, fx: false) 또는 enabled = false.
    /// </summary>
    public sealed class ButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public float HoverScale = 1.03f, PressScale = 0.97f, Speed = 14f;
        bool _hover, _press;
        Button _button;
        RectTransform _rt;

        void Awake() { _button = GetComponent<Button>(); _rt = transform as RectTransform; }
        void OnDisable() { _hover = _press = false; if (_rt) _rt.localScale = Vector3.one; }

        public void OnPointerEnter(PointerEventData e) => _hover = true;
        public void OnPointerExit(PointerEventData e) { _hover = false; _press = false; }
        public void OnPointerDown(PointerEventData e) => _press = true;
        public void OnPointerUp(PointerEventData e) => _press = false;

        void Update()
        {
            if (!_rt) return;
            bool on = !_button || _button.interactable;
            float target = !on ? 1f : _press ? PressScale : _hover ? HoverScale : 1f;
            float k = 1f - Mathf.Exp(-Speed * Time.unscaledDeltaTime);
            float s = Mathf.Lerp(_rt.localScale.x, target, k);
            if (Mathf.Abs(s - target) < 0.001f) s = target;
            _rt.localScale = new Vector3(s, s, 1f);
        }
    }

    /// <summary>글자 대신 쓰는 UI 아이콘(Resources/UiIcons.png, 코드로 그린 16px 도트 한 줄 시트).</summary>
    public static class UiIcons
    {
        public const string Hp = "hp", Atk = "atk", Aspd = "aspd", Def = "def", Spd = "spd", Mana = "mana", Regen = "regen",
            MRes = "mres", Res = "res", Str = "str", Agi = "agi", Int = "int", Wil = "wil", Xp = "xp", Cart = "cart",
            Heal = "heal", Revive = "revive", Recruit = "recruit", Relic = "relic", Item = "item", LvUp = "lvup",
            Dice = "dice", Skull = "skull", Shrine = "shrine";   // events: 도박꾼·저주 상자/유령 상인·저지의 사당
        static readonly string[] Order = { Hp, Atk, Aspd, Def, Spd, Mana, Regen, MRes, Res, Str, Agi, Int, Wil, Xp, Cart, Heal, Revive, Recruit, Relic, Item, LvUp, Dice, Skull, Shrine };
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
