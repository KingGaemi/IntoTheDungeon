using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Features.Battle;
using IntoTheDungeon.Features.Run;

namespace IntoTheDungeon.Unity.Hud
{
    /// <summary>유물 아이콘(Resources/RelicIcons.png, 코드로 그린 16px 도트 한 줄 시트). 순서는 아래 Order.</summary>
    public static class RelicIcons
    {
        static readonly string[] Order = { "str", "agi", "int", "wil", "firstaid", "focus", "bulk", "coin", "boots",
                                           "crest", "fang", "heart", "anvil", "horn", "titan", "phoenix" };
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

    /// <summary>
    /// 판 정보(전투·지도·정비 화면 공용, 코드로 만든다).
    /// 왼쪽 위: 파티 유물 줄(아이콘 + 등급 색 테두리, 앞의 몇 개만) + 「펼치기」 버튼. 마우스를 올리면 이름·등급·효과.
    /// 유물은 개수 제한이 없어서 다 못 보여 주면 「+N 펼치기」, 누르면 유물 페이지(전부, 효과까지)가 열린다(Esc·닫기·바깥 클릭으로 닫음).
    /// 오른쪽 위: 소지 골드(크게, 늘면 반짝). 전투 중엔 배속 버튼 왼쪽.
    /// 배치 중엔 배치 안내와 겹치므로 숨긴다.
    /// </summary>
    public sealed class RunInfoHud : MonoBehaviour, IWorldInjectable
    {
        const float Slot = 64f, Gap = 8f;
        const int BarSlots = 6;   // 줄에 보이는 유물 수(넘치면 펼치기 버튼에 +N)
        static readonly Color GoldColor = new(1f, 0.84f, 0.3f, 1f);

        sealed class Hover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            public int Index;
            public RunInfoHud Owner;
            public void OnPointerEnter(PointerEventData e) => Owner._hover = Index;
            public void OnPointerExit(PointerEventData e) { if (Owner._hover == Index) Owner._hover = -1; }
        }

        sealed class Tile
        {
            public GameObject Root, Empty;
            public Image Frame;
            public RelicMedallion Medal;   // 유물은 둥근 금테 메달(장비 네모 칸과 구분)
        }

        BattleProgress _progress;
        Font _font;
        GameObject _canvas;
        RectTransform _relicBar, _goldBox;
        readonly Tile[] _tiles = new Tile[BarSlots];
        RectTransform _moreButton;
        Text _moreLabel;
        GameObject _page;
        RectTransform _pageContent;
        Text _pageTitle;
        int _pageBuiltFor = -1;
        Text _relicLabel, _gold, _goldGain;
        GameObject _tip;
        RectTransform _tipRect;
        Text _tipText;
        int _hover = -1;
        int _shownGold = -1;
        float _pulse, _gainAge = 99f;

        public void Init(IWorld world)
        {
            world.TryGet(out _progress);
            if (!_canvas) Build();
        }

        void Update()
        {
            if (_progress == null || !_canvas) return;
            var run = RunState.Current;
            bool show = _progress.Phase != BattlePhase.Deploy && run != null;
            if (_canvas.activeSelf != show) _canvas.SetActive(show);
            if (!show) return;

            // 자리: 지도는 「정비로 돌아가기」 버튼 오른쪽, 정비·전투는 왼쪽 맨 위(정비 화면의 상점 판 위쪽 빈 줄).
            // 골드는 오른쪽 끝(전투 중엔 배속 버튼 왼쪽)
            bool map = _progress.Phase == BattlePhase.Map;
            bool camp = map && run.Phase == RunPhase.Camp;
            _relicBar.anchoredPosition = camp ? new Vector2(30f, -18f) : new Vector2(map ? 360f : 30f, -26f);
            _goldBox.anchoredPosition = new Vector2(map ? -30f : -256f, -30f);

            // 유물 줄: 넘치면 마지막 칸 대신 펼치기 버튼에 +N
            int count = run.Relics.Count;
            int shown = count > BarSlots ? BarSlots - 1 : count;
            _relicLabel.text = count > 0 ? $"유물 {count}개" : "유물 없음";
            _moreButton.anchoredPosition = new Vector2(shown * (Slot + Gap), -24f);
            _moreLabel.text = count > shown ? $"+{count - shown}\n<size=15>펼치기</size>" : "펼치기";
            for (int i = 0; i < _tiles.Length; i++)
            {
                var t = _tiles[i];
                var relic = i < shown ? run.Relics[i] : null;
                if (t.Root.activeSelf != (relic != null)) t.Root.SetActive(relic != null);
                if (t.Empty.activeSelf) t.Empty.SetActive(false);
                if (relic == null) continue;
                if (t.Medal.Relic != relic) t.Medal.Set(relic);
            }
            var hovered = !_page.activeSelf && _hover >= 0 && _hover < shown ? run.Relics[_hover] : null;
            if (_tip.activeSelf != (hovered != null)) _tip.SetActive(hovered != null);
            if (hovered != null)
            {
                var col = LootRenderer.RarityColor(hovered.Rarity);
                _tipText.text = $"<size=26><color=#{ColorUtility.ToHtmlStringRGB(col)}>{hovered.Title}</color></size>\n" +
                                $"<size=17>유물 · {ItemDef.RarityLabel(hovered.Rarity)} · 파티 전원</size>\n\n{StopPowerUi.Highlight(hovered.Description)}";
                _tipRect.anchoredPosition = new Vector2(_hover * (Slot + Gap), -Slot - 34f);
            }

            // 유물 페이지: 열려 있으면 유물이 바뀔 때 다시 채운다
            if (_page.activeSelf)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) _page.SetActive(false);
                else if (_pageBuiltFor != count) FillPage(run);
            }

            // 골드: 늘면 숫자가 잠깐 커지고 「+N」이 떠오른다
            float dt = Time.unscaledDeltaTime;
            if (_shownGold < 0) _shownGold = run.Gold;
            if (run.Gold != _shownGold)
            {
                if (run.Gold > _shownGold) { _goldGain.text = $"+{run.Gold - _shownGold}"; _gainAge = 0f; _pulse = 1f; }
                _shownGold = run.Gold;
            }
            _gold.text = run.Gold.ToString("N0");
            _pulse = Mathf.Max(0f, _pulse - dt * 3f);
            _gold.rectTransform.localScale = Vector3.one * (1f + 0.18f * _pulse);
            _gold.color = Color.Lerp(GoldColor, Color.white, _pulse * 0.6f);
            _gainAge += dt;
            float ga = Mathf.Clamp01(1f - (_gainAge - 0.6f) / 0.5f);
            _goldGain.color = new Color(GoldColor.r, GoldColor.g, GoldColor.b, _gainAge < 1.1f ? ga : 0f);
            _goldGain.rectTransform.anchoredPosition = new Vector2(-16f, -66f - 18f * Mathf.Min(1f, _gainAge));
        }

        void Build()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _canvas = new GameObject("RunInfoCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvas.transform.SetParent(transform, false);
            var canvas = _canvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 243;   // 정비(210)·지도(200) 위
            var scaler = _canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // ── 유물 칸(왼쪽 위) ──
            _relicBar = NewRect("Relics", _canvas.transform);
            _relicBar.anchorMin = _relicBar.anchorMax = _relicBar.pivot = new Vector2(0f, 1f);
            _relicBar.sizeDelta = new Vector2((BarSlots + 1) * (Slot + Gap) - Gap, Slot + 26f);
            _relicLabel = NewText(_relicBar, "", 17);
            _relicLabel.alignment = TextAnchor.UpperLeft;
            _relicLabel.color = new Color(1f, 1f, 1f, 0.75f);
            var lrt = _relicLabel.rectTransform;
            lrt.anchorMin = lrt.anchorMax = lrt.pivot = new Vector2(0f, 1f);
            lrt.anchoredPosition = new Vector2(2f, 0f);
            lrt.sizeDelta = new Vector2(200f, 22f);
            for (int i = 0; i < _tiles.Length; i++) _tiles[i] = BuildTile(i);

            // 펼치기 버튼(유물 줄 끝)
            var more = UiKit.Card(NewImage(_relicBar, Color.white), UiKit.Rim);
            more.raycastTarget = true;
            _moreButton = more.rectTransform;
            _moreButton.anchorMin = _moreButton.anchorMax = _moreButton.pivot = new Vector2(0f, 1f);
            _moreButton.sizeDelta = new Vector2(Slot, Slot);
            var moreBody = NewImage(_moreButton, new Color(0f, 0f, 0f, 0f));
            var mbr = moreBody.rectTransform;
            mbr.anchorMin = Vector2.zero; mbr.anchorMax = Vector2.one;
            mbr.offsetMin = new Vector2(2f, 2f); mbr.offsetMax = new Vector2(-2f, -2f);
            _moreLabel = NewText(mbr, "펼치기", 18);
            _moreLabel.supportRichText = true;
            _moreLabel.color = GoldColor;
            _moreLabel.lineSpacing = 0.85f;
            var mlr = _moreLabel.rectTransform;
            mlr.anchorMin = Vector2.zero; mlr.anchorMax = Vector2.one; mlr.offsetMin = mlr.offsetMax = Vector2.zero;
            var moreBtn = more.gameObject.AddComponent<Button>();
            moreBtn.targetGraphic = moreBody;
            moreBtn.onClick.AddListener(() => { _page.SetActive(!_page.activeSelf); _pageBuiltFor = -1; });

            BuildPage();

            // 마우스를 올리면 아래로 설명
            var tip = UiKit.Panel(NewImage(_relicBar, Color.white));
            _tip = tip.gameObject;
            _tipRect = tip.rectTransform;
            _tipRect.anchorMin = _tipRect.anchorMax = _tipRect.pivot = new Vector2(0f, 1f);
            _tipRect.sizeDelta = new Vector2(420f, 150f);
            _tipText = NewText(_tipRect, "", 20);
            _tipText.supportRichText = true;
            _tipText.alignment = TextAnchor.UpperLeft;
            _tipText.horizontalOverflow = HorizontalWrapMode.Wrap;
            var tt = _tipText.rectTransform;
            tt.anchorMin = Vector2.zero; tt.anchorMax = Vector2.one;
            tt.offsetMin = new Vector2(14f, 8f); tt.offsetMax = new Vector2(-14f, -10f);
            _tip.SetActive(false);

            // ── 골드(오른쪽 위) ──
            var box = UiKit.Panel(NewImage(_canvas.transform, Color.white), rim: GoldColor);
            _goldBox = box.rectTransform;
            _goldBox.anchorMin = _goldBox.anchorMax = _goldBox.pivot = new Vector2(1f, 1f);
            _goldBox.sizeDelta = new Vector2(250f, 64f);
            var coin = NewImage(_goldBox, Color.white);
            coin.sprite = RelicIcons.Of("coin");
            var crt = coin.rectTransform;
            crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0f, 0.5f);
            crt.anchoredPosition = new Vector2(12f, 0f);
            crt.sizeDelta = new Vector2(44f, 44f);
            _gold = NewText(_goldBox, "0", 40);
            _gold.alignment = TextAnchor.MiddleRight;
            _gold.color = GoldColor;
            var grt = _gold.rectTransform;
            grt.anchorMin = Vector2.zero; grt.anchorMax = Vector2.one;
            grt.pivot = new Vector2(1f, 0.5f);   // 오른쪽 기준으로 커지게. 피벗은 위치보다 먼저(나중에 바꾸면 칸이 밀려남)
            grt.offsetMin = new Vector2(64f, 0f); grt.offsetMax = new Vector2(-16f, 0f);
            var go = _gold.gameObject.AddComponent<Outline>();
            go.effectColor = new Color(0f, 0f, 0f, 0.9f);
            go.effectDistance = new Vector2(2f, -2f);
            _goldGain = NewText(_goldBox, "", 26);
            _goldGain.alignment = TextAnchor.MiddleRight;
            var ggr = _goldGain.rectTransform;
            ggr.anchorMin = ggr.anchorMax = ggr.pivot = new Vector2(1f, 1f);
            ggr.sizeDelta = new Vector2(200f, 30f);
            _goldGain.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);
        }

        // ───────── 유물 페이지(전부 + 효과) ─────────

        void BuildPage()
        {
            // 화면 전체를 어둡게 덮고(바깥을 누르면 닫힘) 가운데 큰 판
            var dim = NewImage(_canvas.transform, new Color(0f, 0f, 0f, 0.6f));
            dim.raycastTarget = true;
            _page = dim.gameObject;
            var drt = dim.rectTransform;
            drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one; drt.offsetMin = drt.offsetMax = Vector2.zero;
            var dimBtn = _page.AddComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;
            dimBtn.onClick.AddListener(() => _page.SetActive(false));

            var panel = UiKit.Panel(NewImage(drt, Color.white));
            panel.raycastTarget = true;   // 판 안을 눌러도 닫히지 않게
            var prt = panel.rectTransform;
            prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(1280f, 800f);

            _pageTitle = NewText(prt, "", 34);
            _pageTitle.color = GoldColor;
            var trt = _pageTitle.rectTransform;
            trt.anchorMin = new Vector2(0f, 1f); trt.anchorMax = new Vector2(1f, 1f); trt.pivot = new Vector2(0.5f, 1f);
            trt.anchoredPosition = new Vector2(0f, -18f);
            trt.sizeDelta = new Vector2(0f, 50f);
            var sub = NewText(prt, "", 19);
            sub.color = new Color(1f, 1f, 1f, 0.6f);
            var srt = sub.rectTransform;
            srt.anchorMin = new Vector2(0f, 1f); srt.anchorMax = new Vector2(1f, 1f); srt.pivot = new Vector2(0.5f, 1f);
            srt.anchoredPosition = new Vector2(0f, -68f);
            srt.sizeDelta = new Vector2(0f, 28f);

            // 스크롤 영역(유물이 많아도 다 볼 수 있게)
            var viewport = NewImage(prt, new Color(0f, 0f, 0f, 0.01f));
            viewport.raycastTarget = true;
            var vrt = viewport.rectTransform;
            vrt.anchorMin = Vector2.zero; vrt.anchorMax = Vector2.one;
            vrt.offsetMin = new Vector2(30f, 90f); vrt.offsetMax = new Vector2(-30f, -110f);
            viewport.gameObject.AddComponent<RectMask2D>();
            _pageContent = NewRect("Content", vrt);
            _pageContent.anchorMin = new Vector2(0f, 1f); _pageContent.anchorMax = new Vector2(1f, 1f); _pageContent.pivot = new Vector2(0.5f, 1f);
            _pageContent.sizeDelta = new Vector2(0f, 0f);
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = vrt;
            scroll.content = _pageContent;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            // 닫기
            var close = NewImage(prt, GoldColor);
            UiKit.Style(close, GoldColor, Color.Lerp(GoldColor, Color.white, 0.5f), shadow: true);
            close.raycastTarget = true;
            var crt = close.rectTransform;
            crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0.5f, 0f);
            crt.anchoredPosition = new Vector2(0f, 22f);
            crt.sizeDelta = new Vector2(260f, 52f);
            var closeText = NewText(crt, "닫기 (Esc)", 24);
            closeText.color = new Color(0.1f, 0.08f, 0.04f, 1f);
            var ctr = closeText.rectTransform;
            ctr.anchorMin = Vector2.zero; ctr.anchorMax = Vector2.one; ctr.offsetMin = ctr.offsetMax = Vector2.zero;
            var closeBtn = close.gameObject.AddComponent<Button>();
            closeBtn.targetGraphic = close;
            closeBtn.onClick.AddListener(() => _page.SetActive(false));
            _page.SetActive(false);
        }

        // 카드 3열: 아이콘 · 이름(등급 색) · 등급 · 효과
        void FillPage(RunData run)
        {
            _pageBuiltFor = run.Relics.Count;
            for (int i = _pageContent.childCount - 1; i >= 0; i--) Destroy(_pageContent.GetChild(i).gameObject);
            _pageTitle.text = $"파티 유물 {run.Relics.Count}개";
            const int cols = 3;
            const float w = 396f, h = 128f, gap = 14f;
            for (int i = 0; i < run.Relics.Count; i++)
            {
                var relic = run.Relics[i];
                var col = LootRenderer.RarityColor(relic.Rarity);
                var frame = UiKit.Card(NewImage(_pageContent, Color.white), UiKit.Rim);   // 유물은 금테(등급은 메달 보석 점)
                var frt = frame.rectTransform;
                frt.anchorMin = frt.anchorMax = frt.pivot = new Vector2(0f, 1f);
                frt.anchoredPosition = new Vector2((i % cols) * (w + gap), -(i / cols) * (h + gap));
                frt.sizeDelta = new Vector2(w, h);
                var body = NewImage(frt, new Color(0f, 0f, 0f, 0f));
                var brt = body.rectTransform;
                brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
                brt.offsetMin = new Vector2(3f, 3f); brt.offsetMax = new Vector2(-3f, -3f);
                var medal = RelicMedallion.Build(brt, 84f);
                var mrt = (RectTransform)medal.transform;
                mrt.anchorMin = mrt.anchorMax = new Vector2(0f, 0.5f);
                mrt.anchoredPosition = new Vector2(54f, 6f);
                medal.Set(relic);
                var name = NewText(brt, $"{relic.Title}  <size=16><color=#{ColorUtility.ToHtmlStringRGB(col)}>{ItemDef.RarityLabel(relic.Rarity)}</color></size>", 24);
                name.supportRichText = true;
                name.alignment = TextAnchor.UpperLeft;
                name.color = col;
                var nrt = name.rectTransform;
                nrt.anchorMin = new Vector2(0f, 1f); nrt.anchorMax = new Vector2(1f, 1f); nrt.pivot = new Vector2(0f, 1f);
                nrt.offsetMin = new Vector2(106f, -40f); nrt.offsetMax = new Vector2(-8f, -10f);
                var desc = NewText(brt, StopPowerUi.Highlight(relic.Description), 19);
                desc.supportRichText = true;
                desc.alignment = TextAnchor.UpperLeft;
                desc.horizontalOverflow = HorizontalWrapMode.Wrap;
                desc.color = new Color(1f, 1f, 1f, 0.88f);
                desc.fontStyle = FontStyle.Normal;
                var dsr = desc.rectTransform;
                dsr.anchorMin = Vector2.zero; dsr.anchorMax = Vector2.one;
                dsr.offsetMin = new Vector2(106f, 8f); dsr.offsetMax = new Vector2(-10f, -44f);
            }
            int rows = (run.Relics.Count + cols - 1) / cols;
            _pageContent.sizeDelta = new Vector2(0f, rows * (h + gap));
            if (run.Relics.Count == 0)
            {
                var none = NewText(_pageContent, "-", 22);
                none.color = new Color(1f, 1f, 1f, 0.6f);
                var nr = none.rectTransform;
                nr.anchorMin = new Vector2(0f, 1f); nr.anchorMax = new Vector2(1f, 1f); nr.pivot = new Vector2(0.5f, 1f);
                nr.anchoredPosition = new Vector2(0f, -40f);
                nr.sizeDelta = new Vector2(0f, 40f);
            }
        }

        Tile BuildTile(int i)
        {
            var t = new Tile();
            var pos = new Vector2(i * (Slot + Gap), -24f);
            t.Empty = EmptySlotUi.Build(_relicBar, _font, new Vector2(0f, 1f), new Vector2(0f, 1f), pos, new Vector2(Slot, Slot), null, 30);
            t.Frame = NewImage(_relicBar, new Color(0f, 0f, 0f, 0f));   // 자리만(그림은 메달)
            t.Root = t.Frame.gameObject;
            var rt = t.Frame.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(Slot, Slot);
            var body = NewImage(rt, new Color(0f, 0f, 0f, 0f));   // 마우스 받는 투명 판(바탕·테두리는 Frame)
            body.raycastTarget = true;
            var brt = body.rectTransform;
            brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
            brt.offsetMin = new Vector2(3f, 3f); brt.offsetMax = new Vector2(-3f, -3f);
            t.Medal = RelicMedallion.Build(rt, Slot - 6f);
            t.Medal.transform.SetAsFirstSibling();   // 마우스 받는 판보다 뒤
            var hover = body.gameObject.AddComponent<Hover>();
            hover.Index = i;
            hover.Owner = this;
            t.Root.SetActive(false);
            return t;
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static Image NewImage(Transform parent, Color color)
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
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.color = Color.white;
            return t;
        }
    }
}
