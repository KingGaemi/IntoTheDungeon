using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Features.Battle;
using IntoTheDungeon.Features.Character;
using IntoTheDungeon.Features.Run;

namespace IntoTheDungeon.Unity.Hud
{
    /// <summary>
    /// 갈림길 지도 화면(코드로 만든다). 전투가 없을 때(BattlePhase.Map, ChoosingRoom) 화면을 덮는다.
    /// 아래층부터 위로 원형 메달 노드(방 종류 아이콘)를 놓고 길을 잇는다: 지나온 길은 흰 실선, 갈 수 있는 길은 금 점선이 흐르고, 못 가는 길은 흐리다.
    /// 현재 위치엔 리더 초상과 금 고리, 보스 노드 뒤엔 금 빛무리, 왼쪽엔 층 번호. 위엔 파티 초상·체력 미니바와 마지막 소식 칩.
    /// 방을 누르면 메달이 커지며 0.25초 뒤 들어간다(화면이 잠깐 어두워졌다 밝아짐 — BattleStatusHud.RequestFade).
    /// 설명 글 없음: 방 이름 라벨은 처음 하는 판에만, 나머지는 마우스를 올리면 툴팁(Tooltips.Room, 적 구성은 비공개 — 동료 방도 그냥 「?」).
    /// </summary>
    public sealed class DungeonMapHud : MonoBehaviour, IWorldInjectable
    {
        [SerializeField] Vector2 mapSize = new(1300f, 660f);
        [SerializeField] float nodeSize = 72f, bossNodeSize = 100f;
        [SerializeField] Color battleColor = MapIcons.Tint(RoomType.Battle);
        [SerializeField] Color eliteColor = MapIcons.Tint(RoomType.Elite);
        [SerializeField] Color restColor = MapIcons.Tint(RoomType.Rest);
        [SerializeField] Color bossColor = MapIcons.Tint(RoomType.Boss);
        [SerializeField] Color shopColor = MapIcons.Tint(RoomType.Shop);
        [SerializeField] Color eventColor = MapIcons.Tint(RoomType.Event);
        [SerializeField] Color highlight = UiKit.Accent;

        const float PickDelay = 0.25f;                                  // 누른 뒤 들어가기까지(메달이 커지는 연출)
        const float DashLen = 12f, DashGap = 10f, FlowSpeed = 40f;      // 갈 수 있는 길의 금 점선 흐름(픽셀/초)
        const float FlowPeriod = DashLen + DashGap, DashWidth = 5f;
        const float PartyStep = 72f, HpBarWidth = 52f;

        /// <summary>갈 수 있는 길 하나의 점선(점 Image들을 Update에서 흘린다).</summary>
        sealed class Flow
        {
            public MapNode To;
            public Vector2 From, Dir;
            public float Length;
            public readonly List<Image> Dashes = new();
            public float Bright;   // 0 보통 · 1 고른 길(밝게) · -1 고르지 않은 길(흐리게)
        }

        sealed class PartySlot { public GameObject Root; public Image Frame, Portrait, Fill; public Text Fallback; }

        BattleProgress _progress;
        CharacterCatalog _catalog;
        GameObject _root, _backToCamp;
        RectTransform _mapArea, _partyRow, _msgChip;
        Image _bg, _msgIcon, _curRing;
        Text _title, _msgLabel;
        RunData _builtFor;
        DungeonMap _builtMap;   // 스테이지가 바뀌면 지도도 바뀐다
        int _builtVersion = -1, _builtStage = -1;
        int _version;                       // 방을 고를 때마다 올려 다시 그린다
        string _shownMsg;
        bool _picking;                      // 누른 뒤 들어가기 연출 중(다른 방은 안 눌림)
        MapNode _picked;
        readonly List<(Image rim, MapNode node)> _choices = new();
        readonly List<Image> _bossGlows = new();
        readonly List<Flow> _flows = new();
        readonly PartySlot[] _party = new PartySlot[CardPool.MaxPartySize];

        public void Init(IWorld world)
        {
            world.TryGet(out _progress);
            world.TryGet(out _catalog);
            if (!_root) Build();
        }

        void Update()
        {
            if (_progress == null || !_root) return;
            var run = RunState.Current;
            bool show = _progress.Phase == BattlePhase.Map && run.Phase == RunPhase.ChoosingRoom;
            if (_root.activeSelf != show) _root.SetActive(show);
            if (!show) return;

            if (_builtFor != run || _builtMap != run.Map || _builtVersion != _version) Redraw(run);
            if (_backToCamp.activeSelf != run.CanReturnToCamp) _backToCamp.SetActive(run.CanReturnToCamp);

            float t = Time.unscaledTime;
            // 고를 수 있는 방은 테두리가 금색으로 맥동, 고르는 중엔 고른 방만 밝게
            float pulse = 0.6f + 0.4f * Mathf.Sin(t * 5f);
            foreach (var (rim, node) in _choices)
            {
                float a = _picking ? (node == _picked ? 1f : 0.25f) : pulse;
                rim.color = new Color(highlight.r, highlight.g, highlight.b, a);
            }
            // 현재 위치 금 고리 1.2Hz
            if (_curRing)
            {
                float k = 0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 2f * 1.2f);
                _curRing.rectTransform.localScale = Vector3.one * (1f + 0.08f * k);
                _curRing.color = new Color(UiKit.Gold.r, UiKit.Gold.g, UiKit.Gold.b, 0.45f + 0.5f * k);
            }
            // 보스 빛무리 숨쉬기
            float glowA = 0.35f + 0.15f * Mathf.Sin(t * 2f);
            foreach (var g in _bossGlows) g.color = new Color(UiKit.Gold.r, UiKit.Gold.g, UiKit.Gold.b, glowA);

            UpdateFlows(t);
            UpdateParty(run);
            UpdateMessage(run);
        }

        // 금 점선 흐름: 점마다 길 위 위치를 옮기고, 길 양 끝에선 잘라서 그린다
        void UpdateFlows(float t)
        {
            float offset = (t * FlowSpeed) % FlowPeriod;
            foreach (var f in _flows)
            {
                float alpha = f.Bright > 0f ? 1f : f.Bright < 0f ? 0.25f : 0.85f;
                var col = new Color(highlight.r, highlight.g, highlight.b, alpha);
                for (int i = 0; i < f.Dashes.Count; i++)
                {
                    float d = i * FlowPeriod + offset - FlowPeriod;   // 한 주기 앞에서 들어온다
                    float s = Mathf.Max(d, 0f), e = Mathf.Min(d + DashLen, f.Length);
                    bool on = e - s > 0.5f;
                    var img = f.Dashes[i];
                    if (img.enabled != on) img.enabled = on;
                    if (!on) continue;
                    var rt = img.rectTransform;
                    rt.sizeDelta = new Vector2(e - s, DashWidth);
                    rt.anchoredPosition = f.From + f.Dir * ((s + e) * 0.5f);
                    img.color = col;
                }
            }
        }

        // 파티 줄: 초상(없으면 이름 첫 글자) + 체력 미니바(쓰러짐은 회색)
        void UpdateParty(RunData run)
        {
            int n = Mathf.Min(_party.Length, run.Members.Count);
            float x0 = -(n - 1) * PartyStep * 0.5f;
            for (int i = 0; i < _party.Length; i++)
            {
                var s = _party[i];
                bool on = i < n;
                if (s.Root.activeSelf != on) s.Root.SetActive(on);
                if (!on) continue;
                ((RectTransform)s.Root.transform).anchoredPosition = new Vector2(x0 + i * PartyStep, 0f);
                string recipe = run.Members[i];
                var sp = PortraitTable.Of(recipe);
                if (s.Portrait.sprite != sp || s.Portrait.enabled != (bool)sp)
                {
                    s.Portrait.sprite = sp;
                    s.Portrait.enabled = sp;
                    s.Fallback.gameObject.SetActive(!sp);
                    if (!sp) { string name = _catalog != null ? _catalog.DisplayName(recipe) : recipe; s.Fallback.text = string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1); }
                }
                float hp = i < run.PartyHp.Count ? Mathf.Clamp01(run.PartyHp[i]) : 1f;
                bool down = hp <= 0f;
                s.Fill.rectTransform.sizeDelta = new Vector2(HpBarWidth * hp, 6f);
                s.Fill.color = down ? UiKit.Disabled : hp <= 0.3f ? UiKit.Negative : UiKit.Hp;
                s.Portrait.color = down ? new Color(0.45f, 0.45f, 0.45f, 0.8f) : Color.white;
                s.Frame.color = down ? UiKit.Disabled : UiKit.Rim;
            }
        }

        // 마지막 소식 칩(아이콘 + 글자): 휴식·샘물 = 회복, 금화 = 동전, 동료 = 영입, 그 밖(이벤트 제목) = 주사위. 스테이지 시작 문구는 제목이 대신한다
        void UpdateMessage(RunData run)
        {
            string msg = run.LastMessage ?? "";
            if (msg == _shownMsg) return;
            _shownMsg = msg;
            bool show = msg.Length > 0 && !msg.StartsWith("스테이지");
            if (_msgChip.gameObject.activeSelf != show) _msgChip.gameObject.SetActive(show);
            if (!show) return;
            Sprite icon = msg.StartsWith("휴식") || msg.StartsWith("샘물") ? UiIcons.Of(UiIcons.Heal)
                        : msg.Contains("금화") ? RelicIcons.Of("coin")
                        : msg.Contains("동료") ? UiIcons.Of(UiIcons.Recruit)
                        : UiIcons.Of(UiIcons.Dice);
            _msgIcon.sprite = icon;
            _msgIcon.gameObject.SetActive(icon);
            _msgLabel.text = msg;
            _msgLabel.rectTransform.offsetMin = new Vector2(icon ? 42f : 14f, 0f);
            _msgChip.sizeDelta = new Vector2(_msgLabel.preferredWidth + (icon ? 42f : 14f) + 16f, 36f);
            UiTween.Scale(_msgChip, new Vector3(0.9f, 0.9f, 1f), Vector3.one, 0.2f, Ease.OutBack);
        }

        // 방을 누름: 메달이 커지고 고른 길만 밝아진 뒤 0.25초 뒤에 들어간다. 덮개는 그동안 어두워졌다가 다음 화면에서 밝아진다
        void Select(MapNode node, RectTransform frame)
        {
            if (_picking) return;
            var run = RunState.Current;
            if (!run.CanEnter(node)) return;
            _picking = true;
            _picked = node;
            UiTooltip.Hide();
            UiTween.Scale(frame, Vector3.one, new Vector3(1.15f, 1.15f, 1f), 0.12f, Ease.OutBack);
            foreach (var f in _flows) f.Bright = f.To == node ? 1f : -1f;
            BattleStatusHud.RequestFade(PickDelay, 0.3f);
            UiTween.Delay(PickDelay, () =>
            {
                if (!this) return;   // 씬이 바뀜
                _picking = false;
                if (_picked == node && RunState.Current == run) OnPick(node);
                _picked = null;
            });
        }

        void OnPick(MapNode node)
        {
            var run = RunState.Current;
            if (!run.CanEnter(node)) return;
            run.Enter(node);   // 전투 방이면 BattleFlowSystem이 보고 전투를 시작한다(지도는 Phase가 바뀌며 닫힘)
            _version++;
        }

        void Redraw(RunData run)
        {
            _builtFor = run;
            _builtMap = run.Map;
            _builtVersion = _version;
            _title.text = $"스테이지 {run.Stage} / {RunData.MaxStage}  <size=24><color=#8CF08C>#{RunSeed.Encode(run.Seed)}</color></size>";
            if (run.StageTier > 0) _title.text += $"  <size=24><color=#FF6B61>{DungeonTier.Name(run.StageTier)}</color></size>";
            if (_builtStage != run.Stage) { _builtStage = run.Stage; _bg.color = StageTint(run.Stage); }
            _choices.Clear();
            _bossGlows.Clear();
            _flows.Clear();
            _curRing = null;
            UiTooltip.Hide();   // 가리키던 노드가 사라진다
            for (int i = _mapArea.childCount - 1; i >= 0; i--) Destroy(_mapArea.GetChild(i).gameObject);

            var choices = new HashSet<MapNode>(run.Choices());
            int layers = run.Map.LayerCount;
            bool firstRun = MetaProgress.Current.FirstRun;

            // 층 가로선·번호(맨 뒤)
            for (int l = 0; l < layers; l++)
            {
                float y = LayerY(layers, l);
                var line = UiKit.NewImage(_mapArea, new Color(1f, 1f, 1f, 0.06f), name: "LayerLine");
                UiKit.Place(line.rectTransform, new Vector2(0f, y), new Vector2(mapSize.x + 80f, 2f));
                var num = UiKit.NewText(_mapArea, (l + 1).ToString(), UiKit.CaptionSize, UiKit.TextDim, TextAnchor.MiddleRight);
                UiKit.Place(num.rectTransform, new Vector2(-mapSize.x * 0.5f - 56f, y), new Vector2(40f, 24f));
            }

            // 길 먼저(지나온 길 흰 실선 · 갈 수 있는 길 금 점선 · 그 밖 흐림), 방은 그 위에
            for (int l = 0; l < layers - 1; l++)
                foreach (var node in run.Map.Layers[l])
                    foreach (int j in node.Next)
                    {
                        if (j < 0 || j >= run.Map.Layers[l + 1].Count) continue;
                        var to = run.Map.Layers[l + 1][j];
                        bool walked = node.Visited && to.Visited;
                        bool open = node == run.Current && choices.Contains(to);
                        if (open) BuildFlow(NodePos(run, node), NodePos(run, to), to);
                        else DrawLine(NodePos(run, node), NodePos(run, to), walked ? 6f : 3f, walked ? new Color(1f, 1f, 1f, 0.9f) : new Color(1f, 1f, 1f, 0.12f));
                    }
            // 스테이지 첫 방 고르기(현재 위치 없음): 아래 출발점에서 1층 방들로 점선, 출발점에 리더 표시
            if (run.Current == null)
            {
                var start = new Vector2(0f, LayerY(layers, 0) - 90f);
                foreach (var node in run.Map.Layers[0])
                    if (choices.Contains(node)) BuildFlow(start, NodePos(run, node), node);
                var anchor = new GameObject("Start", typeof(RectTransform)).GetComponent<RectTransform>();
                anchor.SetParent(_mapArea, false);
                UiKit.Place(anchor, start, new Vector2(24f, 24f));
                BuildCurrentMarker(anchor, 14f, run);
            }

            foreach (var row in run.Map.Layers)
                foreach (var node in row)
                    DrawNode(run, node, choices.Contains(node), firstRun);
        }

        float LayerY(int layers, int layer) => layers <= 1 ? 0f : (float)layer / (layers - 1) * mapSize.y - mapSize.y * 0.5f;

        Vector2 NodePos(RunData run, MapNode node)
        {
            int count = run.Map.Layers[node.Layer].Count;
            float x = (node.Index + 1f) / (count + 1f) * mapSize.x - mapSize.x * 0.5f;
            return new Vector2(x, LayerY(run.Map.LayerCount, node.Layer));
        }

        Color ColorOf(RoomType type) => type switch
        {
            RoomType.Elite => eliteColor,
            RoomType.Rest => restColor,
            RoomType.Boss => bossColor,
            RoomType.Shop => shopColor,
            RoomType.Event => eventColor,
            _ => battleColor
        };

        static Color StageTint(int stage) => stage >= 2 ? new Color(0.1f, 0.16f, 0.12f, 0.5f) : new Color(0.12f, 0.14f, 0.2f, 0.5f);   // 1 청회 · 2 이끼

        void DrawNode(RunData run, MapNode node, bool choosable, bool firstRun)
        {
            var pos = NodePos(run, node);
            bool boss = node.Type == RoomType.Boss;
            bool current = node == run.Current;
            bool past = node.Visited && !current;
            float dia = boss ? bossNodeSize : nodeSize;
            Color fill = ColorOf(node.Type);
            if (past) fill = Color.Lerp(fill, Color.black, 0.55f);
            else if (!choosable && !current) fill = Color.Lerp(fill, Color.black, 0.3f);

            // 보스 뒤 금 빛무리(지나온 뒤엔 없음)
            if (boss && !past)
            {
                var glow = UiKit.NewImage(_mapArea, new Color(UiKit.Gold.r, UiKit.Gold.g, UiKit.Gold.b, 0.4f), name: "BossGlow");
                glow.sprite = SoftGlowSprite;
                UiKit.Place(glow.rectTransform, pos, new Vector2(dia * 2.4f, dia * 2.4f));
                _bossGlows.Add(glow);
            }

            // 그림자 → 테두리(고를 수 있으면 금 맥동 · 현재 흰 · 그 외 어두움; 마우스·클릭은 여기서 받는다) → 바탕 원 → 아이콘
            var shadow = UiKit.NewImage(_mapArea, new Color(0f, 0f, 0f, 0.5f), name: "Shadow");
            shadow.sprite = UiKit.CircleSprite;
            UiKit.Place(shadow.rectTransform, pos + new Vector2(0f, -5f), new Vector2(dia + 8f, dia + 8f));

            var rimColor = choosable ? highlight : current ? Color.white : new Color(0f, 0f, 0f, 0.6f);
            var frame = UiKit.NewImage(_mapArea, rimColor, raycast: true, name: "Node");
            frame.sprite = UiKit.CircleRimSprite;
            UiKit.Place(frame.rectTransform, pos, new Vector2(dia + 8f, dia + 8f));

            var body = UiKit.NewImage(frame.transform, fill, name: "Body");
            body.sprite = UiKit.CircleSprite;
            UiKit.Stretch(body.rectTransform, 4f);

            float iconSize = boss ? 44f : 32f;
            var icon = UiKit.NewImage(frame.transform, past ? new Color(1f, 1f, 1f, 0.4f) : Color.white, name: "Icon");
            icon.sprite = MapIcons.ForRoom(node.Type);
            icon.preserveAspect = true;
            UiKit.Place(icon.rectTransform, new Vector2(0f, firstRun ? (boss ? 10f : 8f) : 0f), new Vector2(iconSize, iconSize));   // 라벨을 병기하면 아이콘은 위로
            if (!icon.sprite)   // 시트가 없으면 글자로
            {
                icon.enabled = false;
                var fallback = UiKit.NewText(frame.transform, Encounters.Label(node.Type), boss ? 30 : 22, past ? UiKit.TextDim : UiKit.TextMain);
                UiKit.Stretch(fallback.rectTransform);
            }
            else if (firstRun)   // 처음 하는 판에만 방 이름 병기
            {
                var label = UiKit.NewText(frame.transform, Encounters.Label(node.Type), UiKit.CaptionSize, past ? UiKit.TextDim : UiKit.TextMain);
                UiKit.Place(label.rectTransform, new Vector2(0f, -(iconSize * 0.5f + (boss ? 8f : 6f))), new Vector2(dia, 20f));
            }

            // 보스·정예 잼 배지(오른쪽 위 모서리에 걸쳐서, 지나온 방은 없음)
            string gem = boss ? Encounters.BossGemOf(run.Stage) : node.Type == RoomType.Elite ? Encounters.EliteGem : null;
            if (gem != null && !past)
            {
                var gemImg = UiKit.NewImage(frame.transform, Color.white, name: "Gem");
                gemImg.sprite = MapIcons.Gem(gem);
                gemImg.preserveAspect = true;
                float gs = boss ? 48f : 36f;
                UiKit.Place(gemImg.rectTransform, new Vector2(1f, 1f), new Vector2(gs * 0.3f, gs * 0.3f), new Vector2(gs, gs));
                gemImg.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.8f);
                if (!gemImg.sprite) gemImg.enabled = false;
            }

            if (current) BuildCurrentMarker(frame.rectTransform, dia, run);

            if (choosable)
            {
                var btn = frame.gameObject.AddComponent<Button>();
                btn.targetGraphic = body;
                var cb = btn.colors;
                cb.normalColor = Color.white;
                cb.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
                cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
                cb.selectedColor = Color.white;
                cb.colorMultiplier = 1.2f;
                cb.fadeDuration = 0.08f;
                btn.colors = cb;
                var n = node;
                var frt = frame.rectTransform;
                btn.onClick.AddListener(() => Select(n, frt));
                _choices.Add((frame, node));
            }
            var captured = node;
            UiTooltip.Bind(frame.gameObject, () => Tooltips.Room(captured, RunState.Current));
        }

        // 현재 위치: 발밑 금 고리(맥동) + 위에 리더 초상(둥근 금 틀, 없으면 ★)
        void BuildCurrentMarker(RectTransform parent, float dia, RunData run)
        {
            var ring = UiKit.NewImage(parent, UiKit.Gold, name: "HereRing");
            ring.sprite = UiKit.CircleRimSprite;
            UiKit.Place(ring.rectTransform, Vector2.zero, new Vector2(dia + 22f, dia + 22f));
            ring.transform.SetAsFirstSibling();
            _curRing = ring;

            var holder = UiKit.NewImage(parent, UiKit.Gold, name: "Leader");
            holder.sprite = UiKit.CircleSprite;
            UiKit.Place(holder.rectTransform, new Vector2(0f, dia * 0.5f + 32f), new Vector2(50f, 50f));
            holder.gameObject.AddComponent<Mask>().showMaskGraphic = true;   // 네모 초상을 둥글게 자른다
            var sp = run.Members.Count > 0 ? PortraitTable.Of(run.Members[0]) : null;
            if (sp)
            {
                var portrait = UiKit.NewImage(holder.transform, Color.white, name: "Portrait");
                portrait.sprite = sp;
                portrait.preserveAspect = true;
                UiKit.Stretch(portrait.rectTransform, 3f);
            }
            else
            {
                var inner = UiKit.NewImage(holder.transform, new Color(0.1f, 0.1f, 0.14f, 1f), name: "Inner");
                inner.sprite = UiKit.CircleSprite;
                UiKit.Stretch(inner.rectTransform, 3f);
                var star = UiKit.NewText(holder.transform, "★", 22, UiKit.Gold);
                UiKit.Stretch(star.rectTransform);
            }
        }

        void DrawLine(Vector2 a, Vector2 b, float width, Color color)
        {
            var img = UiKit.NewImage(_mapArea, color, name: "Path");
            var rt = img.rectTransform;
            var d = b - a;
            rt.anchoredPosition = (a + b) * 0.5f;
            rt.sizeDelta = new Vector2(d.magnitude, width);
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        }

        // 갈 수 있는 길: 점 Image들을 미리 만들어 두고 Update에서 흘린다(노드 ≤ 32 · 점 ≤ 300)
        void BuildFlow(Vector2 a, Vector2 b, MapNode to)
        {
            var d = b - a;
            float len = d.magnitude;
            if (len < 1f) return;
            var f = new Flow { To = to, From = a, Dir = d / len, Length = len };
            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            int count = Mathf.CeilToInt(len / FlowPeriod) + 1;
            for (int i = 0; i < count; i++)
            {
                var img = UiKit.NewImage(_mapArea, highlight, name: "Dash");
                img.rectTransform.sizeDelta = new Vector2(DashLen, DashWidth);
                img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
                f.Dashes.Add(img);
            }
            _flows.Add(f);
        }

        void Build()
        {
            var canvas = UiKit.NewCanvas(transform, "MapCanvas", 200);   // 전투 UI·필살기 버튼 위
            _root = canvas.gameObject;

            var dim = UiKit.NewImage(_root.transform, new Color(0.04f, 0.04f, 0.08f, 1f), raycast: true, name: "Dim");   // 불투명 + 뒤의 버튼이 눌리지 않게
            UiKit.Stretch(dim.rectTransform);
            _bg = UiKit.NewImage(_root.transform, StageTint(1), name: "Gradient");   // 스테이지 색 세로 그라데이션(위가 밝음)
            _bg.sprite = GradientSprite;
            UiKit.Stretch(_bg.rectTransform);

            _title = UiKit.Soft(UiKit.NewText(_root.transform, "", UiKit.TitleSize, UiKit.Gold));
            UiKit.Place(_title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -48f), new Vector2(900f, 60f));

            // 파티 줄(초상 + 체력 미니바, 왼쪽) · 마지막 소식 칩(오른쪽)
            _partyRow = new GameObject("Party", typeof(RectTransform)).GetComponent<RectTransform>();
            _partyRow.SetParent(_root.transform, false);
            UiKit.Place(_partyRow, new Vector2(0.5f, 1f), new Vector2(-120f, -146f), new Vector2(300f, 64f));
            for (int i = 0; i < _party.Length; i++) _party[i] = BuildPartySlot(i);

            var chip = UiKit.Card(UiKit.NewImage(_root.transform, Color.white, name: "Message"), UiKit.RimDim);
            _msgChip = chip.rectTransform;
            _msgChip.anchorMin = _msgChip.anchorMax = new Vector2(0.5f, 1f);
            _msgChip.pivot = new Vector2(0f, 0.5f);
            _msgChip.anchoredPosition = new Vector2(190f, -146f);
            _msgChip.sizeDelta = new Vector2(200f, 36f);
            _msgIcon = UiKit.NewImage(_msgChip, Color.white, name: "Icon");
            _msgIcon.preserveAspect = true;
            UiKit.Place(_msgIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(26f, 26f));
            _msgLabel = UiKit.NewText(_msgChip, "", UiKit.BodySize, UiKit.TextMain, TextAnchor.MiddleLeft);
            UiKit.Stretch(_msgLabel.rectTransform);
            _msgLabel.rectTransform.offsetMin = new Vector2(42f, 0f);
            _msgChip.gameObject.SetActive(false);

            var area = new GameObject("MapArea", typeof(RectTransform));
            area.transform.SetParent(_root.transform, false);
            _mapArea = (RectTransform)area.transform;
            UiKit.Place(_mapArea, new Vector2(0f, -80f), mapSize);

            // 방을 고르기 전까지 정비 화면으로 돌아가기(잘못 눌러 지도로 왔을 때)
            var back = UiKit.NewImage(_root.transform, new Color(0.16f, 0.22f, 0.45f, 1f), raycast: true, name: "BackToCamp");
            UiKit.Place(back.rectTransform, new Vector2(0f, 1f), new Vector2(40f, -40f), new Vector2(300f, 64f));
            var btn = back.gameObject.AddComponent<Button>();
            UiKit.Button(btn, back, new Color(0.16f, 0.22f, 0.45f, 1f));
            btn.onClick.AddListener(() => RunState.Current.ReturnToCamp());
            var label = UiKit.NewText(back.transform, "← 정비로 돌아가기", 28);
            UiKit.Stretch(label.rectTransform);
            _backToCamp = back.gameObject;
            _backToCamp.SetActive(false);
            _root.SetActive(false);
        }

        PartySlot BuildPartySlot(int i)
        {
            var s = new PartySlot();
            var root = new GameObject($"Member{i}", typeof(RectTransform));
            root.transform.SetParent(_partyRow, false);
            s.Root = root;
            UiKit.Place((RectTransform)root.transform, Vector2.zero, new Vector2(64f, 64f));

            s.Frame = UiKit.NewImage(root.transform, UiKit.Rim, name: "Frame");
            s.Frame.sprite = UiKit.CircleSprite;
            UiKit.Place(s.Frame.rectTransform, new Vector2(0f, 6f), new Vector2(48f, 48f));
            s.Frame.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            s.Portrait = UiKit.NewImage(s.Frame.transform, Color.white, name: "Portrait");
            s.Portrait.preserveAspect = true;
            UiKit.Stretch(s.Portrait.rectTransform, 3f);
            s.Fallback = UiKit.NewText(s.Frame.transform, "", UiKit.BodySize, UiKit.TextMain);
            UiKit.Stretch(s.Fallback.rectTransform);
            s.Fallback.gameObject.SetActive(false);

            var back = UiKit.NewImage(root.transform, new Color(0f, 0f, 0f, 0.6f), name: "HpBack");
            UiKit.Place(back.rectTransform, new Vector2(0f, -24f), new Vector2(HpBarWidth, 6f));
            s.Fill = UiKit.NewImage(back.transform, UiKit.Hp, name: "HpFill");
            UiKit.Place(s.Fill.rectTransform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(HpBarWidth, 6f));

            int captured = i;
            UiTooltip.Bind(s.Frame.gameObject, () => PartyTooltip(captured));
            root.SetActive(false);
            return s;
        }

        // 파티원 초상 툴팁: 이름 · 리더/동료 · 체력 %
        TooltipContent PartyTooltip(int i)
        {
            var run = RunState.Current;
            if (i >= run.Members.Count) return null;
            float hp = i < run.PartyHp.Count ? run.PartyHp[i] : 1f;
            var c = new TooltipContent
            {
                Title = _catalog != null ? _catalog.DisplayName(run.Members[i]) : run.Members[i],
                Kind = i == 0 ? "리더" : "동료",
                Icon = PortraitTable.Of(run.Members[i]),
                Rim = hp <= 0f ? UiKit.Disabled : UiKit.RimDim
            };
            c.Stats.Add((UiIcons.Of(UiIcons.Hp), hp > 0f ? $"{Mathf.RoundToInt(hp * 100f)}%" : "쓰러짐", hp <= 0.3f ? UiKit.Negative : UiKit.Hp));
            return c;
        }

        // ───────── 코드로 만든 스프라이트(씬이 바뀌어도 그대로 쓴다) ─────────

        static Sprite s_gradient, s_softGlow;

        /// <summary>4×64 세로 그라데이션(위 불투명 → 아래 투명, 흰색 → Image.color로 물들임).</summary>
        static Sprite GradientSprite
        {
            get
            {
                if (s_gradient) return s_gradient;
                var tex = new Texture2D(4, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                for (int y = 0; y < 64; y++)
                {
                    float a = y / 63f;
                    a *= a;
                    for (int x = 0; x < 4; x++) tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
                tex.Apply();
                s_gradient = Sprite.Create(tex, new Rect(0, 0, 4, 64), new Vector2(0.5f, 0.5f), 100f);
                return s_gradient;
            }
        }

        /// <summary>64px 부드러운 원형 빛무리(가운데 진하고 가장자리로 사라짐).</summary>
        static Sprite SoftGlowSprite
        {
            get
            {
                if (s_softGlow) return s_softGlow;
                const int n = 64;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                float c = n * 0.5f, r = n * 0.5f;
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float d = Mathf.Sqrt((x + 0.5f - c) * (x + 0.5f - c) + (y + 0.5f - c) * (y + 0.5f - c)) / r;
                        float a = Mathf.Clamp01(1f - d);
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                    }
                tex.Apply();
                s_softGlow = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
                return s_softGlow;
            }
        }
    }
}
