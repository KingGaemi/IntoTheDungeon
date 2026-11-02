using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.Abstractions.Messages.Combat;
using IntoTheDungeon.Features.Battle;
using IntoTheDungeon.Features.Character;
using IntoTheDungeon.Features.Run;
using IntoTheDungeon.Unity.Bridge.View;

namespace IntoTheDungeon.Unity.Hud
{
    /// <summary>
    /// 딜 미터(전투 중 오른쪽 위, 배속 버튼 아래): 접힌 탭에 마우스를 올리면 펼쳐지고, 누르면 펼친 채로 고정(다시 누르면 해제).
    /// 탭 3개(딜량·탱킹·회복)마다 파티원별 막대 + 수치(초당 값 포함)와 초당 값 꺾은선 그래프(3초 평균)를 보여 준다.
    /// 기록은 CombatMeter(이번 방, 전투 시간만). 피해는 ViewBridge.AnyDamaged로 받아 넘긴다.
    /// </summary>
    public sealed class MeterHud : MonoBehaviour, IWorldInjectable
    {
        const int Slots = CombatMeter.Slots;
        const int GraphW = 460, GraphH = 150;
        const float Refresh = 0.25f;

        static readonly Color[] SlotColors =
        {
            new(1f, 0.55f, 0.3f, 1f), new(0.4f, 0.8f, 1f, 1f), new(0.6f, 1f, 0.45f, 1f), new(0.95f, 0.5f, 0.9f, 1f)
        };
        static readonly string[] TabNames = { "딜량", "탱킹", "회복" };

        [SerializeField] Color panelColor = new(0.06f, 0.07f, 0.12f, 0.92f);
        [SerializeField] Color accent = new(1f, 0.82f, 0.3f, 1f);

        sealed class Row
        {
            public GameObject Root;
            public Image Portrait;
            public RectTransform Fill;
            public Image FillImage;
            public Text Name, Value;
        }

        sealed class HoverArea : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            public bool Hovered;
            public void OnPointerEnter(PointerEventData e) => Hovered = true;
            public void OnPointerExit(PointerEventData e) => Hovered = false;
        }

        IWorld _world;
        BattleProgress _progress;
        CharacterCatalog _catalog;
        Font _font;
        GameObject _root, _panel;
        HoverArea _hover;
        Text _tabLabel, _summary, _graphLabel;
        readonly Image[] _tabs = new Image[3];
        readonly Row[] _rows = new Row[Slots];
        Texture2D _graph;
        Color32[] _pixels;
        MeterKind _kind = MeterKind.Dealt;
        bool _pinned;
        float _timer;

        public void Init(IWorld world)
        {
            _world = world;
            world.TryGet(out _progress);
            world.TryGet(out _catalog);
            if (!_root) Build();
            ViewBridge.AnyDamaged -= OnDamaged;
            ViewBridge.AnyDamaged += OnDamaged;
        }

        void OnDestroy() => ViewBridge.AnyDamaged -= OnDamaged;

        void OnDamaged(DamagedEvent e)
        {
            if (_world != null) CombatMeter.Damage(_world.EntityManager, e.Source, e.Victim, e.Amount);
        }

        void Update()
        {
            if (_progress == null || !_root) return;
            bool battle = _progress.Phase != BattlePhase.Map;
            if (_root.activeSelf != battle) _root.SetActive(battle);
            if (!battle) return;

            bool open = _pinned || _hover.Hovered;
            if (_panel.activeSelf != open) _panel.SetActive(open);

            float total = 0f;
            for (int s = 0; s < Slots; s++) total += CombatMeter.Total(MeterKind.Dealt, s);
            float t = Mathf.Max(1f, CombatMeter.Elapsed);
            _tabLabel.text = $"딜 미터 {(open ? "▼" : "▶")}   파티 DPS {total / t:0.0}{(_pinned ? "  (고정)" : "")}";
            if (!open) return;

            _timer -= Time.unscaledDeltaTime;
            if (_timer > 0f) return;
            _timer = Refresh;
            Redraw();
        }

        void Redraw()
        {
            var run = RunState.Current;
            float t = Mathf.Max(1f, CombatMeter.Elapsed);
            for (int i = 0; i < _tabs.Length; i++) _tabs[i].color = i == (int)_kind ? accent : new Color(1f, 1f, 1f, 0.15f);

            float max = 1f, sum = 0f;
            for (int s = 0; s < Slots; s++)
            {
                float v = Value(s);
                max = Mathf.Max(max, v);
                sum += CombatMeter.Total(_kind, s);
            }

            for (int s = 0; s < Slots; s++)
            {
                var row = _rows[s];
                bool exists = s < run.PartySize;
                if (row.Root.activeSelf != exists) row.Root.SetActive(exists);
                if (!exists) continue;

                string recipe = run.Members[s];
                var portrait = PortraitTable.Of(recipe);
                if (row.Portrait.sprite != portrait) row.Portrait.sprite = portrait;
                row.Portrait.enabled = portrait;
                row.Name.text = $"{s + 1}. {(_catalog != null ? _catalog.DisplayName(recipe) : recipe)}";
                row.FillImage.color = SlotColors[s];
                row.Fill.anchorMax = new Vector2(Mathf.Clamp01(Value(s) / max), 1f);

                float total = CombatMeter.Total(_kind, s);
                float share = sum > 0f ? total / sum * 100f : 0f;
                row.Value.text = _kind switch
                {
                    MeterKind.Dealt => $"{total:N0}  ·  DPS {total / t:0.0}  ·  {share:0}%",
                    MeterKind.Taken => $"받음 {total:N0}  ·  막음 {CombatMeter.Blocked(s):N0}  ·  초당 {total / t:0.0}",
                    _ => $"{total:N0}  ·  HPS {total / t:0.0}  ·  {share:0}%"
                };
            }

            string unit = _kind == MeterKind.Dealt ? "DPS" : _kind == MeterKind.Taken ? "초당 받은 피해" : "HPS";
            _summary.text = $"전투 시간 {CombatMeter.Elapsed:0}초   ·   파티 {TabNames[(int)_kind]} {sum:N0}   ·   파티 {unit} {sum / t:0.0}";
            _graphLabel.text = $"{unit} 변화 (3초 평균)";
            DrawGraph(run.PartySize);
        }

        // 막대 길이 기준: 탱킹은 받은 피해 + 막은 피해, 나머지는 합계
        float Value(int s) => _kind == MeterKind.Taken ? CombatMeter.Total(_kind, s) + CombatMeter.Blocked(s) : CombatMeter.Total(_kind, s);

        // 초당 값(3초 이동 평균)을 파티원별 색 꺾은선으로
        void DrawGraph(int partySize)
        {
            var bg = new Color32(12, 14, 22, 255);
            var grid = new Color32(40, 44, 60, 255);
            for (int i = 0; i < _pixels.Length; i++) _pixels[i] = bg;
            for (int g = 1; g < 4; g++)
                for (int x = 0; x < GraphW; x++) _pixels[(GraphH * g / 4) * GraphW + x] = grid;

            int secs = CombatMeter.Seconds;
            if (secs >= 2)
            {
                var series = new float[Slots, secs];
                float top = 1f;
                for (int s = 0; s < Slots && s < partySize; s++)
                    for (int k = 0; k < secs; k++)
                    {
                        float a = 0f; int n = 0;
                        for (int j = Mathf.Max(0, k - 2); j <= k; j++) { a += CombatMeter.At(_kind, j, s); n++; }
                        series[s, k] = a / n;
                        top = Mathf.Max(top, series[s, k]);
                    }
                top *= 1.1f;
                for (int s = 0; s < Slots && s < partySize; s++)
                {
                    Color32 c = SlotColors[s];
                    for (int k = 1; k < secs; k++)
                    {
                        int x0 = (k - 1) * (GraphW - 1) / (secs - 1), x1 = k * (GraphW - 1) / (secs - 1);
                        int y0 = (int)(series[s, k - 1] / top * (GraphH - 3)) + 1, y1 = (int)(series[s, k] / top * (GraphH - 3)) + 1;
                        Line(x0, y0, x1, y1, c);
                    }
                }
            }
            _graph.SetPixels32(_pixels);
            _graph.Apply(false);
        }

        void Line(int x0, int y0, int x1, int y1, Color32 c)
        {
            int dx = Mathf.Abs(x1 - x0), dy = -Mathf.Abs(y1 - y0), sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1, err = dx + dy;
            while (true)
            {
                Plot(x0, y0, c); Plot(x0, y0 + 1, c);   // 두 줄 두께
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        void Plot(int x, int y, Color32 c)
        {
            if (x >= 0 && x < GraphW && y >= 0 && y < GraphH) _pixels[y * GraphW + x] = c;
        }

        void SelectTab(int i)
        {
            _kind = (MeterKind)i;
            _timer = 0f;
        }

        // ───────── 화면 만들기 ─────────

        void Build()
        {
            var canvasGo = new GameObject("MeterCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 93;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // 오른쪽 위, 배속 버튼 아래. 탭 + 펼친 패널을 한 덩어리로(그 위에 마우스가 있는 동안 펼침)
            _root = new GameObject("Meter", typeof(RectTransform));
            _root.transform.SetParent(canvasGo.transform, false);
            var rrt = (RectTransform)_root.transform;
            rrt.anchorMin = rrt.anchorMax = rrt.pivot = new Vector2(1f, 1f);
            rrt.anchoredPosition = new Vector2(-30f, -106f);
            rrt.sizeDelta = new Vector2(520f, 46f);
            var hit = _root.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);   // 탭 줄 전체가 마우스를 받게
            _hover = _root.AddComponent<HoverArea>();

            var tab = NewImage(rrt, new Color(0.12f, 0.14f, 0.24f, 0.95f));
            tab.raycastTarget = true;
            Stretch(tab.rectTransform, 0f);
            var tabBtn = tab.gameObject.AddComponent<Button>();
            tabBtn.targetGraphic = tab;
            tabBtn.onClick.AddListener(() => _pinned = !_pinned);
            _tabLabel = NewText(tab.rectTransform, "", 22);
            _tabLabel.alignment = TextAnchor.MiddleLeft;
            Stretch(_tabLabel.rectTransform, 14f);

            // 펼친 패널(탭 바로 아래, 탭 자식이라 마우스를 옮겨도 펼친 채)
            var panel = NewImage(rrt, panelColor);
            panel.raycastTarget = true;
            _panel = panel.gameObject;
            var prt = panel.rectTransform;
            prt.anchorMin = new Vector2(0f, 0f); prt.anchorMax = new Vector2(1f, 0f); prt.pivot = new Vector2(0.5f, 1f);
            prt.anchoredPosition = new Vector2(0f, -4f);
            prt.sizeDelta = new Vector2(0f, 520f);

            for (int i = 0; i < 3; i++)
            {
                int k = i;
                var b = NewImage(prt, Color.white);
                b.raycastTarget = true;
                Top(b.rectTransform, new Vector2(10f + i * 168f, -10f), new Vector2(160f, 40f));
                var bb = b.gameObject.AddComponent<Button>();
                bb.targetGraphic = b;
                bb.onClick.AddListener(() => SelectTab(k));
                var t = NewText(b.rectTransform, TabNames[i], 22);
                t.color = new Color(0.1f, 0.1f, 0.15f, 1f);
                Stretch(t.rectTransform, 0f);
                _tabs[i] = b;
            }

            for (int s = 0; s < Slots; s++) _rows[s] = BuildRow(prt, -60f - s * 54f);

            _summary = NewText(prt, "", 18);
            _summary.alignment = TextAnchor.MiddleLeft;
            _summary.color = new Color(1f, 1f, 1f, 0.75f);
            Top(_summary.rectTransform, new Vector2(14f, -278f), new Vector2(496f, 26f));
            _graphLabel = NewText(prt, "", 18);
            _graphLabel.alignment = TextAnchor.MiddleLeft;
            _graphLabel.color = accent;
            Top(_graphLabel.rectTransform, new Vector2(14f, -306f), new Vector2(496f, 26f));

            _graph = new Texture2D(GraphW, GraphH, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            _pixels = new Color32[GraphW * GraphH];
            var raw = new GameObject("Graph", typeof(RectTransform), typeof(RawImage));
            raw.transform.SetParent(prt, false);
            raw.GetComponent<RawImage>().texture = _graph;
            raw.GetComponent<RawImage>().raycastTarget = false;
            Top((RectTransform)raw.transform, new Vector2(30f, -336f), new Vector2(GraphW, GraphH + 20f));

            _panel.SetActive(false);
        }

        Row BuildRow(RectTransform parent, float y)
        {
            var row = new Row();
            var back = NewImage(parent, new Color(0f, 0f, 0f, 0.35f));
            row.Root = back.gameObject;
            Top(back.rectTransform, new Vector2(10f, y), new Vector2(500f, 48f));

            row.Portrait = NewImage(back.rectTransform, Color.white);
            row.Portrait.preserveAspect = true;
            Top(row.Portrait.rectTransform, new Vector2(4f, -4f), new Vector2(40f, 40f));

            var barBack = NewImage(back.rectTransform, new Color(1f, 1f, 1f, 0.08f));
            Top(barBack.rectTransform, new Vector2(52f, -26f), new Vector2(440f, 16f));
            row.FillImage = NewImage(barBack.rectTransform, Color.white);
            row.Fill = row.FillImage.rectTransform;
            row.Fill.anchorMin = Vector2.zero; row.Fill.anchorMax = new Vector2(0f, 1f);
            row.Fill.offsetMin = row.Fill.offsetMax = Vector2.zero;

            row.Name = NewText(back.rectTransform, "", 18);
            row.Name.alignment = TextAnchor.MiddleLeft;
            Top(row.Name.rectTransform, new Vector2(52f, -2f), new Vector2(170f, 24f));
            row.Value = NewText(back.rectTransform, "", 17);
            row.Value.alignment = TextAnchor.MiddleRight;
            Top(row.Value.rectTransform, new Vector2(200f, -2f), new Vector2(292f, 24f));
            row.Root.SetActive(false);
            return row;
        }

        Image NewImage(Transform parent, Color color)
        {
            var go = new GameObject("Image", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        Text NewText(Transform parent, string text, int fontSize)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = _font;
            t.text = text;
            t.fontSize = fontSize;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.raycastTarget = false;
            t.color = Color.white;
            return t;
        }

        // 부모 왼쪽 위 기준 배치(pos.y는 아래로 음수)
        static void Top(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        static void Stretch(RectTransform rt, float inset)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, 0f);
            rt.offsetMax = new Vector2(-inset, 0f);
        }
    }
}
