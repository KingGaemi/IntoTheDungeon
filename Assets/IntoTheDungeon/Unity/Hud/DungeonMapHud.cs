using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Features.Battle;
using IntoTheDungeon.Features.Run;

namespace IntoTheDungeon.Unity.Hud
{
    /// <summary>
    /// 갈림길 지도 화면(임시 UI, 코드로 만든다). 전투가 없을 때(BattlePhase.Map) 화면을 덮는다.
    /// 아래층부터 위로 방을 놓고 길을 선으로 잇는다. 지금 갈 수 있는 방만 눌린다.
    /// 휴식 방은 누르는 즉시 회복하고 지도에 남고, 전투 방은 누르면 지도가 닫히고 전투가 시작된다.
    /// </summary>
    public sealed class DungeonMapHud : MonoBehaviour, IWorldInjectable
    {
        [SerializeField] Vector2 mapSize = new(1300f, 760f);
        [SerializeField] Vector2 nodeSize = new(96f, 70f);
        [SerializeField] Color battleColor = new(0.3f, 0.36f, 0.5f, 1f);
        [SerializeField] Color eliteColor = new(0.55f, 0.32f, 0.7f, 1f);
        [SerializeField] Color restColor = new(0.3f, 0.6f, 0.4f, 1f);
        [SerializeField] Color bossColor = new(0.85f, 0.65f, 0.2f, 1f);
        [SerializeField] Color highlight = new(1f, 0.82f, 0.3f, 1f);

        BattleProgress _progress;
        GameObject _root, _backToCamp;
        RectTransform _mapArea;
        Text _info;
        Font _font;
        RunData _builtFor;
        DungeonMap _builtMap;   // 스테이지가 바뀌면 지도도 바뀐다
        Text _title;
        int _builtVersion = -1;
        int _version;                       // 방을 고를 때마다 올려 다시 그린다
        readonly List<(Image img, MapNode node)> _choiceImages = new();

        public void Init(IWorld world)
        {
            world.TryGet(out _progress);
            if (!_root) Build();
        }

        void Update()
        {
            if (_progress == null || !_root) return;
            bool show = _progress.Phase == BattlePhase.Map && RunState.Current.Phase == RunPhase.ChoosingRoom;
            if (_root.activeSelf != show) _root.SetActive(show);
            if (!show) return;

            var run = RunState.Current;
            if (_builtFor != run || _builtMap != run.Map || _builtVersion != _version) Redraw(run);

            if (_backToCamp.activeSelf != run.CanReturnToCamp) _backToCamp.SetActive(run.CanReturnToCamp);

            // 고를 수 있는 방은 테두리가 깜빡인다
            float pulse = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 5f);
            foreach (var (img, _) in _choiceImages)
                img.color = new Color(highlight.r, highlight.g, highlight.b, pulse);
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
            _title.text = $"스테이지 {run.Stage} / {RunData.MaxStage} 지도";
            _choiceImages.Clear();
            for (int i = _mapArea.childCount - 1; i >= 0; i--) Destroy(_mapArea.GetChild(i).gameObject);

            var choices = new HashSet<MapNode>(run.Choices());
            int layers = run.Map.LayerCount;

            // 길(선) 먼저, 방(버튼)은 그 위에
            for (int l = 0; l < layers - 1; l++)
                foreach (var node in run.Map.Layers[l])
                    foreach (int j in node.Next)
                    {
                        var to = run.Map.Layers[l + 1][j];
                        bool walked = node.Visited && to.Visited;
                        bool open = node == run.Current && choices.Contains(to);
                        var c = walked ? new Color(1f, 1f, 1f, 0.8f) : open ? highlight : new Color(1f, 1f, 1f, 0.18f);
                        DrawLine(NodePos(run, node), NodePos(run, to), walked || open ? 5f : 3f, c);
                    }

            foreach (var row in run.Map.Layers)
                foreach (var node in row)
                    DrawNode(run, node, choices.Contains(node));

            int alive = 0;
            var hp = new System.Text.StringBuilder();
            for (int i = 0; i < run.PartyHp.Count; i++)
            {
                if (run.PartyHp[i] > 0f) alive++;
                hp.Append(i == 0 ? "" : "  ").Append(run.PartyHp[i] > 0f ? $"{Mathf.RoundToInt(run.PartyHp[i] * 100f)}%" : "쓰러짐");
            }
            string party = run.PartyHp.Count == 0 ? "파티 체력 가득" : $"파티 체력  {hp}";
            string msg = string.IsNullOrEmpty(run.LastMessage) ? "" : $"\n{run.LastMessage}";
            _info.text = $"{(run.CurrentLayer + 1 >= layers ? "" : $"{run.CurrentLayer + 2}층으로 갈 방을 고르세요")}   ·   {party}{msg}";
        }

        Vector2 NodePos(RunData run, MapNode node)
        {
            int layers = run.Map.LayerCount;
            int count = run.Map.Layers[node.Layer].Count;
            float x = (node.Index + 1f) / (count + 1f) * mapSize.x - mapSize.x * 0.5f;
            float y = layers <= 1 ? 0f : (float)node.Layer / (layers - 1) * mapSize.y - mapSize.y * 0.5f;
            return new Vector2(x, y);
        }

        void DrawNode(RunData run, MapNode node, bool choosable)
        {
            var pos = NodePos(run, node);
            Color fill = node.Type switch
            {
                RoomType.Elite => eliteColor,
                RoomType.Rest => restColor,
                RoomType.Boss => bossColor,
                _ => battleColor
            };
            bool past = node.Visited && node != run.Current;
            if (past) fill = Color.Lerp(fill, Color.black, 0.55f);
            else if (!choosable && node != run.Current) fill = Color.Lerp(fill, Color.black, 0.3f);

            var size = node.Type == RoomType.Boss ? nodeSize * 1.4f : nodeSize;

            // 테두리(고를 수 있으면 깜빡이는 금색, 지금 위치면 흰색)
            var frame = NewImage("Node", _mapArea, choosable ? highlight : node == run.Current ? Color.white : new Color(0f, 0f, 0f, 0.6f));
            var frt = frame.rectTransform;
            frt.anchoredPosition = pos;
            frt.sizeDelta = size + new Vector2(8f, 8f);
            if (choosable) _choiceImages.Add((frame, node));

            var body = NewImage("Body", frt, fill);
            Stretch(body.rectTransform, 4f);
            var label = NewText(body.rectTransform, Encounters.Label(node.Type), node.Type == RoomType.Boss ? 34 : 26);
            label.color = past ? new Color(1f, 1f, 1f, 0.5f) : Color.white;

            if (choosable)
            {
                var btn = frame.gameObject.AddComponent<Button>();
                btn.targetGraphic = body;
                var n = node;
                btn.onClick.AddListener(() => OnPick(n));
                frame.raycastTarget = true;
            }
        }

        void DrawLine(Vector2 a, Vector2 b, float width, Color color)
        {
            var img = NewImage("Path", _mapArea, color);
            var rt = img.rectTransform;
            var d = b - a;
            rt.anchoredPosition = (a + b) * 0.5f;
            rt.sizeDelta = new Vector2(d.magnitude, width);
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        }

        void Build()
        {
            _root = new GameObject("MapCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _root.transform.SetParent(transform, false);
            var canvas = _root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;   // 전투 UI·필살기 버튼 위
            var scaler = _root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var dim = NewImage("Dim", _root.transform, new Color(0.04f, 0.04f, 0.08f, 1f));   // 불투명: 뒤 전투 화면이 비치지 않게
            dim.raycastTarget = true;   // 뒤의 필살기 버튼이 눌리지 않게
            Stretch(dim.rectTransform, 0f);

            var title = _title = NewText(_root.transform, "던전 지도", 52);
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(800f, 70f));

            _info = NewText(_root.transform, "", 28);
            Place(_info.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -130f), new Vector2(1600f, 80f));

            var area = new GameObject("MapArea", typeof(RectTransform));
            area.transform.SetParent(_root.transform, false);
            _mapArea = (RectTransform)area.transform;
            Place(_mapArea, new Vector2(0.5f, 0.5f), new Vector2(0f, -60f), mapSize);

            // 방을 고르기 전까지 정비 화면으로 돌아가기(잘못 눌러 지도로 왔을 때)
            var back = NewImage("BackToCamp", _root.transform, new Color(0.16f, 0.22f, 0.45f, 1f));
            back.raycastTarget = true;
            Place(back.rectTransform, new Vector2(0f, 1f), new Vector2(40f, -40f), new Vector2(300f, 64f));
            var btn = back.gameObject.AddComponent<Button>();
            btn.targetGraphic = back;
            btn.onClick.AddListener(() => RunState.Current.ReturnToCamp());
            var label = NewText(back.transform, "← 정비로 돌아가기", 28);
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            _backToCamp = back.gameObject;
            _backToCamp.SetActive(false);
            _root.SetActive(false);
        }

        Image NewImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
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
            var rt = t.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            return t;
        }

        static void Stretch(RectTransform rt, float inset)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }
    }
}
