using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace IntoTheDungeon.Unity.Hud
{
    /// <summary>툴팁 내용. 팩토리(Tooltips)가 만든다. 글은 Body 한 덩이(리치텍스트), 숫자는 Stats 줄(아이콘+값, 최대 6).</summary>
    public sealed class TooltipContent
    {
        public string Title; public Color TitleColor = UiKit.TextMain;
        public Sprite Icon; public Color IconTint = Color.white;     // 32px 왼쪽 위
        public string Kind;                                         // 한두 단어("유물 · 희귀"), CaptionSize TextDim
        public string Body;                                         // 리치텍스트(StopPowerUi.Highlight는 팩토리가 적용)
        public readonly List<(Sprite icon, string value, Color color)> Stats = new();   // 아이콘+숫자 줄 최대 6
        public Color Rim = UiKit.RimDim;                            // 등급색 등
    }

    /// <summary>
    /// 공용 툴팁(싱글턴, 최초 호출 때 자기 캔버스(sortingOrder 450, Raycaster 없음)와 함께 만들어지고 씬마다 다시).
    /// Bind로 묶으면 마우스를 올리고 0.2초 뒤 마우스 오른쪽 아래에 뜨고, 내리면 사라진다. 화면 밖이면 반대쪽으로.
    /// </summary>
    public sealed class UiTooltip : MonoBehaviour
    {
        public const int SortingOrder = 450;
        public const float Width = 360f, MaxWidth = 460f, Pad = 14f, IconSize = 32f, ShowDelay = 0.2f;
        public const int MaxStats = 6;

        /// <summary>true면 안 띄움(아이템 드래그 중 InventoryItemView가 세움).</summary>
        public static bool Suppressed;

        static UiTooltip s_instance;
        static int s_pending;   // Enter마다 올려서 늦게 온 지연 호출을 걸러낸다

        RectTransform _canvasRt, _panel;
        CanvasGroup _group;
        Image _panelImg, _icon;
        Text _title, _kind, _body;
        readonly RectTransform[] _statRoots = new RectTransform[MaxStats];
        readonly Text[] _statTexts = new Text[MaxStats];
        readonly Image[] _statIcons = new Image[MaxStats];
        GameObject _bound;   // 지금 띄운 대상(사라지면 같이 숨긴다)
        bool _hasBound;      // Bind로 띄웠는지 — 대상이 파괴되면 _bound가 가짜 null이 되어 그것만으론 못 가린다
        bool _shown;

        static UiTooltip Instance
        {
            get
            {
                if (s_instance) return s_instance;
                var canvas = UiKit.NewCanvas(null, "UiTooltip", SortingOrder, raycaster: false);
                s_instance = canvas.gameObject.AddComponent<UiTooltip>();
                s_instance.Build(canvas);
                return s_instance;
            }
        }

        void Build(Canvas canvas)
        {
            _canvasRt = (RectTransform)canvas.transform;
            _panelImg = UiKit.NewImage(transform, UiKit.PanelFill, name: "Tooltip");
            UiKit.Panel(_panelImg);
            _panel = _panelImg.rectTransform;
            _panel.anchorMin = _panel.anchorMax = Vector2.zero;
            _panel.pivot = new Vector2(0f, 1f);
            _panel.sizeDelta = new Vector2(Width, 100f);
            _group = _panelImg.gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;

            _icon = UiKit.NewImage(_panel, Color.white, name: "Icon");
            _icon.preserveAspect = true;
            UiKit.Place(_icon.rectTransform, new Vector2(0f, 1f), new Vector2(Pad, -Pad), new Vector2(IconSize, IconSize));

            _title = UiKit.NewText(_panel, "", UiKit.HeadSize, UiKit.TextMain, TextAnchor.UpperLeft);
            _kind = UiKit.NewText(_panel, "", UiKit.CaptionSize, UiKit.TextDim, TextAnchor.UpperLeft);
            _body = UiKit.NewText(_panel, "", UiKit.BodySize, UiKit.TextMain, TextAnchor.UpperLeft, overflow: false);
            _body.horizontalOverflow = HorizontalWrapMode.Wrap;
            _body.verticalOverflow = VerticalWrapMode.Overflow;
            UiKit.Soft(_title);
            for (int i = 0; i < MaxStats; i++)
            {
                _statRoots[i] = UiKit.IconText(_panel, UiKit.CircleSprite, "", UiKit.NumberSize - 4, UiKit.TextMain, out _statTexts[i]);
                var iconT = _statRoots[i].Find("Icon");
                _statIcons[i] = iconT ? iconT.GetComponent<Image>() : null;
                _statRoots[i].gameObject.SetActive(false);
            }
            _panelImg.gameObject.SetActive(false);
        }

        /// <summary>마우스를 올리면 0.2초 뒤 make()로 내용을 만들어 띄우고, 내리면 숨긴다(HoverHook 사용, raycast 자동 ON).</summary>
        public static void Bind(GameObject target, System.Func<TooltipContent> make)
        {
            if (!target || make == null) return;
            HoverHook.On(target,
                enter: () =>
                {
                    int id = ++s_pending;
                    UiTween.Delay(ShowDelay, () =>
                    {
                        if (id != s_pending || Suppressed || !target || !target.activeInHierarchy) return;
                        var c = make();
                        if (c == null) return;
                        ShowAt(Input.mousePosition, c);
                        Instance._bound = target;
                        Instance._hasBound = true;
                    });
                },
                exit: () => { s_pending++; Hide(); });
        }

        /// <summary>그 화면 좌표의 오른쪽 아래(16,−16)에 띄운다. 화면 밖이면 반대쪽. 등장 scale 0.96→1·alpha 0→1 0.12초.</summary>
        public static void ShowAt(Vector2 screen, TooltipContent c)
        {
            if (c == null || Suppressed) return;
            var t = Instance;
            t._bound = null;
            t._hasBound = false;
            t.Fill(c);
            t.PlaceAt(screen);
            bool fresh = !t._shown;
            t._shown = true;
            t._panelImg.gameObject.SetActive(true);
            if (fresh)
            {
                UiTween.Scale(t._panel, new Vector3(0.96f, 0.96f, 1f), Vector3.one, 0.12f);
                UiTween.Fade(t._group, 0f, 1f, 0.12f);
            }
        }

        public static void Hide()
        {
            if (!s_instance || !s_instance._shown) return;
            s_instance._shown = false;
            s_instance._bound = null;
            s_instance._hasBound = false;
            UiTween.Kill(s_instance._panel);
            UiTween.Kill(s_instance._group);
            s_instance._panelImg.gameObject.SetActive(false);
        }

        void Fill(TooltipContent c)
        {
            float width = Width;
            bool hasIcon = c.Icon;
            float textLeft = Pad + (hasIcon ? IconSize + 10f : 0f);
            _icon.gameObject.SetActive(hasIcon);
            if (hasIcon) { _icon.sprite = c.Icon; _icon.color = c.IconTint; }

            _title.text = c.Title ?? "";
            _title.color = c.TitleColor;
            UiKit.Place(_title.rectTransform, new Vector2(0f, 1f), new Vector2(textLeft, -Pad), new Vector2(width - textLeft - Pad, UiKit.HeadSize + 6f));
            float titleW = _title.preferredWidth;
            if (titleW > width - textLeft - Pad) width = Mathf.Min(MaxWidth, titleW + textLeft + Pad);
            _title.rectTransform.sizeDelta = new Vector2(width - textLeft - Pad, UiKit.HeadSize + 6f);

            float y = -Pad - (UiKit.HeadSize + 6f);
            bool hasKind = !string.IsNullOrEmpty(c.Kind);
            _kind.gameObject.SetActive(hasKind);
            if (hasKind)
            {
                _kind.text = c.Kind;
                UiKit.Place(_kind.rectTransform, new Vector2(0f, 1f), new Vector2(textLeft, y), new Vector2(width - textLeft - Pad, UiKit.CaptionSize + 4f));
                y -= UiKit.CaptionSize + 4f;
            }
            if (hasIcon) y = Mathf.Min(y, -Pad - IconSize - 2f);
            y -= 8f;

            int stats = Mathf.Min(MaxStats, c.Stats.Count);
            if (stats > 0)
            {
                float rowH = UiKit.NumberSize + 4f;
                float colW = (width - Pad * 2f) / Mathf.Min(stats, 3);
                for (int i = 0; i < MaxStats; i++)
                {
                    bool on = i < stats;
                    _statRoots[i].gameObject.SetActive(on);
                    if (!on) continue;
                    var (icon, value, color) = c.Stats[i];
                    if (_statIcons[i]) { _statIcons[i].gameObject.SetActive(icon); _statIcons[i].sprite = icon; _statIcons[i].color = Color.white; }
                    _statTexts[i].text = value ?? "";
                    _statTexts[i].color = color;
                    _statTexts[i].rectTransform.offsetMin = new Vector2(icon ? UiKit.NumberSize + 4f : 0f, 0f);
                    int col = i % 3, row = i / 3;
                    UiKit.Place(_statRoots[i], new Vector2(0f, 1f), new Vector2(Pad + col * colW, y - row * rowH), new Vector2(colW - 4f, rowH));
                }
                y -= rowH * ((stats + 2) / 3) + 6f;
            }
            else for (int i = 0; i < MaxStats; i++) _statRoots[i].gameObject.SetActive(false);

            bool hasBody = !string.IsNullOrEmpty(c.Body);
            _body.gameObject.SetActive(hasBody);
            if (hasBody)
            {
                _body.text = c.Body;
                UiKit.Place(_body.rectTransform, new Vector2(0f, 1f), new Vector2(Pad, y), new Vector2(width - Pad * 2f, 10f));
                float h = _body.preferredHeight;
                _body.rectTransform.sizeDelta = new Vector2(width - Pad * 2f, h);
                y -= h;
            }
            y -= Pad;
            _panel.sizeDelta = new Vector2(width, -y);
            UiKit.SetRim(_panelImg, c.Rim);
        }

        void PlaceAt(Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRt, screen, null, out var local);
            var size = _canvasRt.rect.size;
            var p = local + size * 0.5f;   // 캔버스 왼쪽 아래 기준
            float w = _panel.sizeDelta.x, h = _panel.sizeDelta.y;
            float x = p.x + 16f, y = p.y - 16f;
            if (x + w > size.x) x = p.x - 16f - w;        // 오른쪽이 모자라면 왼쪽으로
            if (y - h < 0f) y = p.y + 16f + h;            // 아래가 모자라면 위로
            x = Mathf.Clamp(x, 0f, Mathf.Max(0f, size.x - w));
            y = Mathf.Clamp(y, Mathf.Min(h, size.y), size.y);
            _panel.anchoredPosition = new Vector2(x, y);
        }

        void Update()
        {
            if (!_shown) return;
            if (Suppressed || (_hasBound && (!_bound || !_bound.activeInHierarchy))) Hide();   // 드래그 시작·대상이 닫히거나 파괴됨(아이템 재구성·카드 Clear 등)
        }

        void OnDestroy() { if (s_instance == this) { s_instance = null; Suppressed = false; } }   // 씬이 바뀌면 드래그 억제도 푼다(정적 잔류 방지)
    }
}
