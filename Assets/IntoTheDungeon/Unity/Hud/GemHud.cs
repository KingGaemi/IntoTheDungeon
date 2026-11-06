using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Features.Battle;
using IntoTheDungeon.Features.Run;

namespace IntoTheDungeon.Unity.Hud
{
    /// <summary>
    /// 잼 세팅 창(임시 UI, 코드로 만든다): 새 게임 직전 봉인석 고르기 화면의 「잼 세팅」 버튼(G키)으로 연다.
    /// 봉인석처럼 판에 들어가기 전에만 바꾸고, 판 도중엔 봉인석 배지에 마우스를 올려 보기만 한다.
    /// 위에 금속 테 소켓(잠긴 칸은 여는 조건), 아래에 가진 잼. 잼을 끌어다 소켓에 놓으면 "딸깍" 끼워지고(번쩍 + 튀어 오름),
    /// 소켓의 잼을 밖으로 끌어내면 빠진다. 소켓끼리 끌면 맞바꾼다. 누르기만 해도 끼우기(빈 소켓)/빼기가 된다.
    /// 끼운 잼은 판이 끝나도 그대로이고, 효과는 판 내내 파티 전원에게 걸린다. 닫을 때 저장한다.
    /// </summary>
    public sealed class GemHud : MonoBehaviour
    {
        [SerializeField] KeyCode toggleKey = KeyCode.G;
        [SerializeField] Color panelColor = new(0.09f, 0.1f, 0.17f, 1f);
        [SerializeField] Color accent = UiKit.Accent;
        const float PopTime = 0.28f;

        /// <summary>끌기·누르기·마우스 올리기를 받아 GemHud에 넘긴다(소켓 또는 목록 칸).</summary>
        sealed class Grip : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
        {
            public GemHud Owner;
            public int Socket = -1;   // 소켓이면 번호
            public int Gem = -1;      // 목록 칸이면 GemPool 번호
            public void OnBeginDrag(PointerEventData e) => Owner.BeginDrag(this, e);
            public void OnDrag(PointerEventData e) => Owner.DragTo(e.position);
            public void OnEndDrag(PointerEventData e) => Owner.EndDrag(e.position);
            public void OnPointerClick(PointerEventData e) => Owner.Click(this);
            public void OnPointerEnter(PointerEventData e) => Owner.Hover(this);
            public void OnPointerExit(PointerEventData e) => Owner.Hover(null);
        }

        sealed class SocketView
        {
            public RectTransform Rect;
            public Image Rim, Gem, Flash, Glow;
            public Text Name, Desc;
            public float PopAge = 99f;
        }

        sealed class GemTile
        {
            public Image Frame, Body, Icon;
            public Text Name, Stars, Count, Desc, Badge;
        }

        Font _font;
        GameObject _panel;
        RectTransform _dragLayer;
        Text _stats, _info, _message;
        Image _ghost;
        readonly SocketView[] _sockets = new SocketView[MetaProgress.MaxSlots];
        readonly GemTile[] _tiles = new GemTile[GemPool.All.Length];
        Dictionary<string, Sprite> _icons;
        Sprite _socketSprite;
        bool _open;

        string _dragId;          // 끌고 있는 잼
        int _dragFromSocket = -1;
        int _hoverSocket = -1;   // 끄는 중 포인터 아래 소켓
        Grip _hovered;

        public bool IsOpen => _open;
        /// <summary>창이 열리거나 닫힌 프레임(같은 키 입력이 여는 쪽·닫는 쪽에서 두 번 처리되지 않게).</summary>
        public int ToggledFrame { get; private set; } = -1;

        public void Setup()
        {
            if (!_panel) Build();
        }

        public void SetOpen(bool on)
        {
            if (_open != on) Toggle();
        }

        Sprite Icon(string id)
        {
            if (_icons == null)
            {
                _icons = new Dictionary<string, Sprite>();
                foreach (var s in Resources.LoadAll<Sprite>("GemIcons")) _icons[s.name] = s;
            }
            return id != null && _icons.TryGetValue(id, out var sp) ? sp : null;
        }

        void Update()
        {
            if (!_panel) return;
            if (_open && ToggledFrame != Time.frameCount && (Input.GetKeyDown(toggleKey) || Input.GetKeyDown(KeyCode.Escape))) Toggle();
            if (_panel.activeSelf != _open) _panel.SetActive(_open);
            if (_open) Refresh(MetaProgress.Current);
        }

        void Toggle()
        {
            _open = !_open;
            ToggledFrame = Time.frameCount;
            _message.text = "";
            CancelDrag();
            if (!_open) MetaStore.SaveIfChanged();   // 바꾼 잼 세팅을 바로 남긴다
        }

        void Refresh(MetaProgress meta)
        {
            _stats.text = "";
            float t = Time.unscaledTime;

            for (int s = 0; s < _sockets.Length; s++)
            {
                var v = _sockets[s];
                bool unlocked = s < meta.SlotCount;
                string id = meta.InSlot(s);
                var gem = GemPool.Find(id);
                bool hidden = _dragFromSocket == s;   // 끌어낸 잼은 소켓에서 빈 것처럼

                v.Rim.color = !unlocked ? new Color(0.35f, 0.35f, 0.4f, 1f) : _hoverSocket == s ? accent * 1.2f : Color.white;
                v.Glow.enabled = unlocked && (_hoverSocket == s || (_dragId != null && id == null));
                v.Glow.color = _hoverSocket == s ? new Color(1f, 0.85f, 0.35f, 0.55f + 0.25f * Mathf.Sin(t * 10f)) : new Color(1f, 1f, 1f, 0.12f + 0.08f * Mathf.Sin(t * 4f));

                v.Gem.enabled = gem != null && !hidden;
                if (v.Gem.enabled)
                {
                    v.Gem.sprite = Icon(id);
                    v.PopAge += Time.unscaledDeltaTime;
                    float k = Mathf.Clamp01(v.PopAge / PopTime);
                    float pop = 1f + 0.35f * Mathf.Sin(k * Mathf.PI) * (1f - k);   // 끼우는 순간 튀어 올랐다 자리 잡음
                    v.Gem.rectTransform.localScale = Vector3.one * pop;
                }
                v.Flash.enabled = v.PopAge < PopTime;
                if (v.Flash.enabled) v.Flash.color = new Color(1f, 0.95f, 0.7f, 0.9f * (1f - v.PopAge / PopTime));

                if (!unlocked)
                {
                    v.Name.text = "잠김";
                    v.Desc.text = "";   // 여는 조건은 마우스를 올리면
                }
                else if (gem == null || hidden)
                {
                    v.Name.text = "";
                    v.Desc.text = "";
                }
                else
                {
                    int lv = meta.Level(id);
                    v.Name.text = $"{gem.Name}  Lv.{lv}";
                    v.Name.color = GemColor(gem);
                    v.Desc.text = gem.Describe(lv);
                }
                if (gem == null || hidden || !unlocked) v.Name.color = Color.white;
            }

            for (int i = 0; i < _tiles.Length; i++)
            {
                var gem = GemPool.All[i];
                var tile = _tiles[i];
                int count = meta.Count(gem.Id), lv = meta.Level(gem.Id);
                bool owned = count > 0, equipped = meta.IsEquipped(gem.Id);
                var c = GemColor(gem);
                tile.Icon.sprite = Icon(gem.Id);
                tile.Icon.color = owned ? (equipped ? new Color(1f, 1f, 1f, 0.45f) : Color.white) : new Color(0f, 0f, 0f, 0.55f);   // 없으면 실루엣
                UiKit.SetRim(tile.Body, equipped ? accent : owned ? c * 0.8f : new Color(0.2f, 0.2f, 0.25f, 1f));
                tile.Body.color = owned ? Color.Lerp(panelColor, c, 0.18f) : new Color(0.12f, 0.12f, 0.16f, 1f);
                tile.Name.text = owned || gem.IsBoss ? gem.Name : "???";   // 보스 잼은 못 얻었어도 이름을 보여 준다(기대하게)
                tile.Stars.text = owned ? new string('★', lv) + new string('☆', GemPool.MaxLevel - lv) : "";
                int next = GemPool.NextThreshold(count);
                tile.Count.text = !owned ? "" : next > 0 ? $"{count} / {next}개" : $"{count}개 (최고)";
                tile.Desc.text = owned ? gem.Describe(lv) : gem.IsBoss ? gem.Source : "";
                if (gem.IsBoss) UiKit.SetRim(tile.Body, owned ? (equipped ? accent : UiKit.Gold) : new Color(0.6f, 0.45f, 0.2f, 1f));   // 보스 잼은 금테
                tile.Badge.enabled = equipped;
            }

            if (_dragId == null && _hovered != null) _info.text = Detail(_hovered, meta);
            else if (_dragId == null) _info.text = "";
        }

        string Detail(Grip g, MetaProgress meta)
        {
            string id = g.Socket >= 0 ? meta.InSlot(g.Socket) : GemPool.All[g.Gem].Id;
            var gem = GemPool.Find(id);
            if (gem == null) return g.Socket >= 0 && g.Socket >= meta.SlotCount ? $"잠긴 소켓: {MetaProgress.SlotUnlockText(g.Socket)} 시 열림" : "빈 소켓";
            if (meta.Count(id) <= 0) return gem.IsBoss ? $"보스 잼 · {gem.Name}: {gem.Source}   ·   Lv.1 {gem.Describe(1)}" : "아직 얻지 못한 잼";
            return $"{gem.Name}   Lv.1 {gem.Describe(1)}   ·   Lv.2 {gem.Describe(2)}   ·   Lv.3 {gem.Describe(3)}";
        }

        static Color GemColor(GemDef gem) => new(gem.R, gem.G, gem.B, 1f);

        // ───────── 끌기·누르기 ─────────

        void BeginDrag(Grip g, PointerEventData e)
        {
            var meta = MetaProgress.Current;
            string id = g.Socket >= 0 ? meta.InSlot(g.Socket) : GemPool.All[g.Gem].Id;
            if (id == null || meta.Count(id) <= 0) return;
            _dragId = id;
            _dragFromSocket = g.Socket;
            _ghost.sprite = Icon(id);
            _ghost.enabled = true;
            _info.text = g.Socket >= 0 ? "다른 소켓에 놓으면 맞바꾸고, 밖에 놓으면 뺍니다" : "소켓에 놓아 끼우세요";
            DragTo(e.position);
        }

        void DragTo(Vector2 screen)
        {
            if (_dragId == null) return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_dragLayer, screen, null, out var local))
                _ghost.rectTransform.anchoredPosition = local;
            _hoverSocket = SocketAt(screen);
        }

        void EndDrag(Vector2 screen)
        {
            if (_dragId == null) return;
            var meta = MetaProgress.Current;
            int target = SocketAt(screen);
            if (target >= 0 && target < meta.SlotCount)
            {
                var gem = GemPool.Find(_dragId);
                var why = meta.PutInSlot(target, _dragId);
                if (why == null)
                {
                    _sockets[target].PopAge = 0f;
                    if (_dragFromSocket >= 0 && _dragFromSocket != target && meta.InSlot(_dragFromSocket) != null) _sockets[_dragFromSocket].PopAge = 0f;
                    _message.text = $"{gem.Name} 장착 (다음 전투부터 적용)";
                }
                else _message.text = why;
            }
            else if (target >= 0) _message.text = "잠긴 소켓입니다";
            else if (_dragFromSocket >= 0)
            {
                meta.Unequip(_dragId);
                _message.text = $"{GemPool.Find(_dragId).Name}을(를) 뺐습니다";
            }
            CancelDrag();
        }

        void CancelDrag()
        {
            _dragId = null;
            _dragFromSocket = -1;
            _hoverSocket = -1;
            if (_ghost) _ghost.enabled = false;
        }

        int SocketAt(Vector2 screen)
        {
            for (int s = 0; s < _sockets.Length; s++)
                if (RectTransformUtility.RectangleContainsScreenPoint(_sockets[s].Rect, screen, null)) return s;
            return -1;
        }

        void Click(Grip g)
        {
            var meta = MetaProgress.Current;
            if (g.Socket >= 0)
            {
                string id = meta.InSlot(g.Socket);
                if (id == null) return;
                meta.Unequip(id);
                _message.text = $"{GemPool.Find(id).Name}을(를) 뺐습니다";
                return;
            }
            var gem = GemPool.All[g.Gem];
            if (meta.IsEquipped(gem.Id)) { meta.Unequip(gem.Id); _message.text = $"{gem.Name}을(를) 뺐습니다"; return; }
            var why = meta.Equip(gem.Id);
            if (why == null) { _sockets[meta.SlotOf(gem.Id)].PopAge = 0f; _message.text = $"{gem.Name} 장착 (다음 전투부터 적용)"; }
            else _message.text = why;
        }

        void Hover(Grip g) => _hovered = g;

        // ───────── 화면 만들기 ─────────

        void Build()
        {
            var canvasGo = new GameObject("GemCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 240;   // 지도·정비 화면 위
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _socketSprite = Resources.Load<Sprite>("GemSocket");
            var root = canvasGo.transform;

            var dim = NewImage(root, new Color(0f, 0f, 0f, 0.72f));
            dim.raycastTarget = true;
            Stretch(dim.rectTransform);
            _panel = dim.gameObject;
            var panel = UiKit.Panel(NewImage(dim.transform, panelColor));
            panel.raycastTarget = true;
            Place(panel.rectTransform, new Vector2(0f, -10f), new Vector2(1320f, 960f));
            var prt = panel.rectTransform;

            var title = NewText(prt, "잼", 40);
            title.color = accent;
            Place(title.rectTransform, new Vector2(0f, 408f), new Vector2(1260f, 52f));
            _stats = NewText(prt, "", 22);
            _stats.color = new Color(1f, 1f, 1f, 0.65f);
            Place(_stats.rectTransform, new Vector2(0f, 368f), new Vector2(1260f, 30f));

            // 소켓 줄(받침 위에 금속 테 소켓 4개)
            var shelf = UiKit.Card(NewImage(prt, Color.white), fill: new Color(0f, 0f, 0f, 0.35f));
            Place(shelf.rectTransform, new Vector2(0f, 205f), new Vector2(1240f, 290f));
            for (int s = 0; s < _sockets.Length; s++) _sockets[s] = BuildSocket(prt, s, new Vector2(-465f + s * 310f, 235f));

            var label = NewText(prt, "가진 잼", 24);
            label.alignment = TextAnchor.MiddleLeft;
            label.color = accent;
            Place(label.rectTransform, new Vector2(0f, 38f), new Vector2(1240f, 30f));
            for (int i = 0; i < _tiles.Length; i++)
                _tiles[i] = BuildTile(prt, i, new Vector2(-465f + (i % 4) * 310f, -40f - (i / 4) * 134f));

            _info = NewText(prt, "", 20);
            _info.color = new Color(1f, 1f, 1f, 0.8f);
            _info.horizontalOverflow = HorizontalWrapMode.Wrap;
            Place(_info.rectTransform, new Vector2(0f, -392f), new Vector2(1240f, 44f));
            _message = NewText(prt, "", 24);
            _message.color = accent;
            Place(_message.rectTransform, new Vector2(0f, -426f), new Vector2(1240f, 32f));
            NewButton(prt, "완료", new Vector2(0f, -462f), new Vector2(300f, 56f), accent, Toggle);

            // 끄는 잼(맨 위)
            _dragLayer = new GameObject("DragLayer", typeof(RectTransform)).GetComponent<RectTransform>();
            _dragLayer.SetParent(dim.transform, false);
            Stretch(_dragLayer);
            _ghost = NewImage(_dragLayer, Color.white);
            _ghost.preserveAspect = true;
            _ghost.rectTransform.sizeDelta = new Vector2(110f, 110f);
            _ghost.enabled = false;

            _panel.SetActive(false);
        }

        SocketView BuildSocket(RectTransform parent, int index, Vector2 pos)
        {
            var v = new SocketView();
            var holder = new GameObject($"Socket{index + 1}", typeof(RectTransform), typeof(Image));
            holder.transform.SetParent(parent, false);
            v.Rect = (RectTransform)holder.transform;
            Place(v.Rect, pos, new Vector2(150f, 150f));
            var hit = holder.GetComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);   // 놓기·누르기 판정

            v.Glow = NewImage(v.Rect, Color.white);
            v.Glow.sprite = FxSprites.DotSprite;
            Place(v.Glow.rectTransform, Vector2.zero, new Vector2(210f, 210f));
            v.Rim = NewImage(v.Rect, Color.white);
            v.Rim.sprite = _socketSprite;
            Place(v.Rim.rectTransform, Vector2.zero, new Vector2(150f, 150f));
            v.Gem = NewImage(v.Rect, Color.white);
            v.Gem.preserveAspect = true;
            Place(v.Gem.rectTransform, Vector2.zero, new Vector2(104f, 104f));
            v.Flash = NewImage(v.Rect, Color.white);
            v.Flash.sprite = FxSprites.DotSprite;
            Place(v.Flash.rectTransform, Vector2.zero, new Vector2(190f, 190f));
            v.Flash.enabled = false;

            v.Name = NewText(parent, "", 22);
            Place(v.Name.rectTransform, pos + new Vector2(0f, -95f), new Vector2(300f, 28f));
            v.Desc = NewText(parent, "", 18);
            v.Desc.color = new Color(1f, 1f, 1f, 0.75f);
            Place(v.Desc.rectTransform, pos + new Vector2(0f, -120f), new Vector2(300f, 24f));

            var grip = holder.AddComponent<Grip>();
            grip.Owner = this;
            grip.Socket = index;
            return v;
        }

        GemTile BuildTile(RectTransform parent, int index, Vector2 pos)
        {
            var t = new GemTile();
            t.Frame = NewImage(parent, new Color(0f, 0f, 0f, 0f));   // 마우스 받는 자리(테두리는 Body의 둥근 테)
            t.Frame.raycastTarget = true;
            Place(t.Frame.rectTransform, pos, new Vector2(290f, 126f));
            t.Body = UiKit.Card(NewImage(t.Frame.rectTransform, panelColor));
            var brt = t.Body.rectTransform;
            brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
            brt.offsetMin = new Vector2(3f, 3f); brt.offsetMax = new Vector2(-3f, -3f);

            t.Icon = NewImage(brt, Color.white);
            t.Icon.preserveAspect = true;
            Corner(t.Icon.rectTransform, new Vector2(10f, -8f), new Vector2(84f, 84f));
            t.Name = NewText(brt, "", 24);
            t.Name.alignment = TextAnchor.MiddleLeft;
            Corner(t.Name.rectTransform, new Vector2(118f, -10f), new Vector2(165f, 30f));
            t.Stars = NewText(brt, "", 20);
            t.Stars.alignment = TextAnchor.MiddleLeft;
            t.Stars.color = accent;
            Corner(t.Stars.rectTransform, new Vector2(118f, -40f), new Vector2(80f, 24f));
            t.Count = NewText(brt, "", 16);
            t.Count.alignment = TextAnchor.MiddleRight;
            t.Count.color = new Color(1f, 1f, 1f, 0.7f);
            Corner(t.Count.rectTransform, new Vector2(180f, -40f), new Vector2(100f, 24f));
            t.Desc = NewText(brt, "", 16);
            t.Desc.alignment = TextAnchor.UpperLeft;
            t.Desc.horizontalOverflow = HorizontalWrapMode.Wrap;
            Corner(t.Desc.rectTransform, new Vector2(118f, -66f), new Vector2(165f, 54f));
            t.Badge = NewText(brt, "장착 중", 18);
            t.Badge.color = accent;
            Corner(t.Badge.rectTransform, new Vector2(10f, -96f), new Vector2(84f, 22f));

            var grip = t.Frame.gameObject.AddComponent<Grip>();
            grip.Owner = this;
            grip.Gem = index;
            return t;
        }

        Button NewButton(Transform parent, string text, Vector2 pos, Vector2 size, Color color, UnityEngine.Events.UnityAction onClick)
        {
            var img = NewImage(parent, color);
            img.raycastTarget = true;
            Place(img.rectTransform, pos, size);
            var btn = img.gameObject.AddComponent<Button>();
            UiKit.Button(btn, img, color);
            btn.onClick.AddListener(onClick);
            var t = NewText(img.transform, text, 24);
            t.color = new Color(0.1f, 0.1f, 0.15f, 1f);
            Stretch(t.rectTransform);
            return btn;
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

        // 부모 왼쪽 위 기준(pos.y는 아래로 음수)
        static void Corner(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static void Place(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }
    }
}
