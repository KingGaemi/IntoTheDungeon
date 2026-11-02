using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.Abstractions.Gameplay;
using IntoTheDungeon.Core.Util;
using IntoTheDungeon.Features.Battle;
using IntoTheDungeon.Features.Character;
using IntoTheDungeon.Features.Run;

namespace IntoTheDungeon.Unity.Hud
{
    /// <summary>
    /// 전투 전 배치 화면(BattlePhase.Deploy): 바닥에 3열×3줄 칸을 깔고, 파티원을 끌어 칸에 놓는다(다른 파티원이 있으면 자리 바꿈).
    /// 열마다 보정(전열 받는 피해↓ · 중열 공속↑ · 후열 공격력↑·받는 피해↑)을 칸 위에 보여 주고, 이번 방 첫 웨이브 적을 미리 알려 준다.
    /// 「전투 시작」(Space·Enter)을 누르면 BattleFlowSystem이 Intro로 넘어간다. 정한 배치는 판에 남아 다음 방에도 이어진다.
    /// </summary>
    public sealed class DeployHud : MonoBehaviour, IWorldInjectable
    {
        [SerializeField] Color frontColor = new(0.45f, 0.7f, 1f, 1f);
        [SerializeField] Color middleColor = new(0.4f, 0.9f, 0.7f, 1f);
        [SerializeField] Color backColor = new(1f, 0.8f, 0.35f, 1f);
        [SerializeField] Vector2 tileSize = new(1.35f, 0.62f);
        const float GrabHeight = 1.4f;   // 발에서 이 높이까지 누르면 그 캐릭터를 집는다

        sealed class Tile { public SpriteRenderer Fill, Ring; public Vector3 Pos; }

        IWorld _world;
        BattleProgress _progress;
        BattleConfig _config;
        CharacterCatalog _catalog;
        IWalkArea _area;
        Font _font;

        readonly Tile[] _tiles = new Tile[Formation.Cells];
        readonly List<Party.Member> _party = new(8);
        readonly List<Text> _names = new();
        GameObject _tileRoot, _ui;
        RectTransform _canvasRect;
        Text _enemyLine;
        readonly Text[] _rowLabels = new Text[Formation.Cols];
        Vector2 _grabOffset;
        float _time;

        public void Init(IWorld world)
        {
            _world = world;
            world.TryGet(out _progress);
            world.TryGet(out _config);
            world.TryGet(out _catalog);
            world.TryGet(out _area);
            if (!_ui) Build();
        }

        bool Active => _progress != null && _progress.Phase == BattlePhase.Deploy;

        void Update()
        {
            if (!_ui) return;
            bool on = Active;
            if (_ui.activeSelf != on) { _ui.SetActive(on); _tileRoot.SetActive(on); }
            if (!on) return;

            _time += Time.unscaledDeltaTime;
            var run = RunState.Current;
            var em = _world.EntityManager;
            Party.Collect(em, _party);
            var middle = Formation.MiddleOf(_area);
            var cam = Camera.main;

            HandleInput(run, cam, middle);

            // 칸: 열 색, 누가 서 있으면 진하게, 끄는 중이면 놓일 칸이 깜빡
            int target = _progress.DragSlot >= 0 ? NearestCell(_progress.DragPos, middle) : -1;
            for (int c = 0; c < Formation.Cells; c++)
            {
                var p = Formation.CellPos(c, middle, _area);
                var t = _tiles[c];
                t.Pos = new Vector3(p.X, p.Y, 0f);
                t.Fill.transform.position = t.Ring.transform.position = t.Pos;
                bool occupied = run.Formation.IndexOf(c) >= 0;
                var col = RowColor(Formation.RowOf(c));
                float pulse = 0.5f + 0.5f * Mathf.Sin(_time * 8f);
                bool hot = c == target;
                t.Fill.color = new Color(col.r, col.g, col.b, hot ? 0.35f + 0.25f * pulse : occupied ? 0.28f : 0.12f);
                t.Ring.color = hot ? Color.Lerp(col, Color.white, pulse) : new Color(col.r, col.g, col.b, occupied ? 0.9f : 0.45f);
                float s = hot ? 1.08f : 1f;
                t.Fill.transform.localScale = new Vector3(tileSize.x * s, tileSize.y * s, 1f);
                t.Ring.transform.localScale = new Vector3(tileSize.x * 1.05f * s, tileSize.y * 1.05f * s, 1f);
            }

            // 열 이름·보정(맨 위 칸 위)
            for (int col = 0; col < Formation.Cols; col++)
            {
                var row = (FormationRow)col;
                var top = _tiles[Formation.Cell(row, 0)].Pos + new Vector3(0f, 1.6f, 0f);
                PlaceAtWorld(_rowLabels[col].rectTransform, cam, top);
                _rowLabels[col].text = $"{Formation.RowName(row)}\n<size=17>{Formation.RowEffect(row)}</size>";
            }

            // 파티원 이름(발밑)
            int n = 0;
            foreach (var m in _party)
            {
                if (m.Slot >= Party.NoSlot || !m.Alive || m.Slot >= run.Members.Count) continue;
                if (n >= _names.Count) _names.Add(NewText(_ui.transform, "", 18));
                var label = _names[n++];
                label.gameObject.SetActive(true);
                label.text = _catalog != null ? _catalog.DisplayName(run.Members[m.Slot]) : run.Members[m.Slot];
                PlaceAtWorld(label.rectTransform, cam, new Vector3(m.Position.X, m.Position.Y - 0.42f, 0f));
            }
            for (; n < _names.Count; n++) _names[n].gameObject.SetActive(false);

            _enemyLine.text = EnemyPreview();

            if (_progress.DragSlot < 0 && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
                Confirm();
        }

        void HandleInput(RunData run, Camera cam, Vec2 middle)
        {
            if (!cam) return;
            var w = cam.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, -cam.transform.position.z));
            var mouse = new Vec2(w.x, w.y);   // 화면 y = 깊이(높이 0)

            if (_progress.DragSlot < 0)
            {
                if (!Input.GetMouseButtonDown(0)) return;
                if (EventSystem.current && EventSystem.current.IsPointerOverGameObject()) return;   // 버튼 위 클릭
                int slot = Pick(mouse, middle, run, out var feet);
                if (slot < 0) return;
                _progress.DragSlot = slot;
                _grabOffset = new Vector2(feet.X - mouse.X, feet.Y - mouse.Y);
                _progress.DragPos = feet;
                return;
            }

            var drag = new Vec2(mouse.X + _grabOffset.x, mouse.Y + _grabOffset.y);
            if (_area != null) drag = _area.Clamp(drag);
            _progress.DragPos = drag;
            if (Input.GetMouseButton(0)) return;

            int cell = NearestCell(drag, middle);
            if (cell >= 0) run.PlaceInCell(_progress.DragSlot, cell);   // 칸 밖에 놓으면 원래 자리로
            _progress.DragSlot = -1;
        }

        // 캐릭터 몸(발에서 GrabHeight까지)이나 그 캐릭터가 선 칸을 누르면 집는다
        int Pick(Vec2 mouse, Vec2 middle, RunData run, out Vec2 feet)
        {
            feet = default;
            float best = float.MaxValue;
            int pick = -1;
            foreach (var m in _party)
            {
                if (m.Slot >= Party.NoSlot || !m.Alive) continue;
                float dx = System.MathF.Abs(mouse.X - m.Position.X);
                float dy = mouse.Y - m.Position.Y;
                bool body = dx < 0.45f && dy > -0.35f && dy < GrabHeight;
                int cell = run.CellOf(m.Slot);
                bool onTile = cell >= 0 && InTile(mouse, Formation.CellPos(cell, middle, _area));
                if (!body && !onTile) continue;
                float score = dx + System.MathF.Abs(dy - 0.5f) * 0.5f;
                if (score < best) { best = score; pick = m.Slot; feet = m.Position; }
            }
            return pick;
        }

        bool InTile(Vec2 p, Vec2 c)
        {
            float x = (p.X - c.X) / (tileSize.x * 0.5f), y = (p.Y - c.Y) / (tileSize.y * 0.5f);
            return x * x + y * y <= 1f;
        }

        int NearestCell(Vec2 p, Vec2 middle)
        {
            int best = -1;
            float bestD = 1.1f;   // 이보다 멀면 칸 밖
            for (int c = 0; c < Formation.Cells; c++)
            {
                var cp = Formation.CellPos(c, middle, _area);
                float dx = p.X - cp.X, dy = (p.Y - cp.Y) * 1.6f;   // 깊이는 좁으니 가중
                float d = System.MathF.Sqrt(dx * dx + dy * dy);
                if (d < bestD) { bestD = d; best = c; }
            }
            return best;
        }

        void Confirm()
        {
            if (!Active) return;
            _progress.DragSlot = -1;
            _progress.DeployConfirmed = true;
        }

        string EnemyPreview()
        {
            if (_config == null || _config.Waves.Count == 0) return "";
            var counts = new Dictionary<string, int>();
            var order = new List<string>();
            foreach (var s in _config.Waves[0])
            {
                string name = (_catalog != null ? _catalog.DisplayName(s.Recipe) : s.Recipe) + (s.Boss ? "(보스)" : "");
                if (!counts.ContainsKey(name)) { counts[name] = 0; order.Add(name); }
                counts[name] += s.Count;
            }
            var sb = new StringBuilder($"첫 웨이브: ");
            for (int i = 0; i < order.Count; i++) sb.Append(i > 0 ? " · " : "").Append(order[i]).Append(" ×").Append(counts[order[i]]);
            sb.Append($"     (웨이브 {_config.Waves.Count}개)");
            return sb.ToString();
        }

        Color RowColor(FormationRow r) => r switch { FormationRow.Front => frontColor, FormationRow.Middle => middleColor, _ => backColor };

        void PlaceAtWorld(RectTransform rt, Camera cam, Vector3 world)
        {
            if (!cam) return;
            var screen = cam.WorldToScreenPoint(world);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screen, null, out var local);
            rt.anchoredPosition = local;
        }

        void Build()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // 바닥 칸(월드): 방 바닥 그림 바로 위, 캐릭터·그림자보다 뒤
            _tileRoot = new GameObject("DeployTiles");
            _tileRoot.transform.SetParent(transform, false);
            for (int c = 0; c < Formation.Cells; c++)
            {
                var t = new Tile
                {
                    Fill = NewSprite("Fill", FxSprites.DotSprite, -29010),
                    Ring = NewSprite("Ring", FxSprites.RingSprite, -29000)
                };
                _tiles[c] = t;
            }
            _tileRoot.SetActive(false);

            var canvasGo = new GameObject("DeployCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 230;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            _canvasRect = (RectTransform)canvasGo.transform;

            _ui = new GameObject("Deploy", typeof(RectTransform));
            _ui.transform.SetParent(canvasGo.transform, false);
            var urt = (RectTransform)_ui.transform;
            urt.anchorMin = urt.anchorMax = urt.pivot = new Vector2(0.5f, 0.5f);
            urt.sizeDelta = Vector2.zero;

            // 위: 제목·안내·적 미리보기
            var band = NewImage(_ui.transform, new Color(0.03f, 0.03f, 0.07f, 0.78f));
            Place(band.rectTransform, new Vector2(0f, 420f), new Vector2(1500f, 150f));
            var title = NewText(_ui.transform, "전투 배치", 44);
            title.color = new Color(1f, 0.86f, 0.45f, 1f);
            Place(title.rectTransform, new Vector2(0f, 455f), new Vector2(800f, 56f));
            var hint = NewText(_ui.transform, "캐릭터를 끌어 칸에 놓으세요 (다른 캐릭터가 있으면 자리 바꿈)   ·   전투가 시작되면 이 자리에서 출발해 적을 찾아갑니다", 21);
            hint.color = new Color(1f, 1f, 1f, 0.7f);
            Place(hint.rectTransform, new Vector2(0f, 410f), new Vector2(1450f, 32f));
            _enemyLine = NewText(_ui.transform, "", 22);
            _enemyLine.color = new Color(1f, 0.72f, 0.6f, 1f);
            Place(_enemyLine.rectTransform, new Vector2(0f, 372f), new Vector2(1450f, 32f));

            for (int col = 0; col < Formation.Cols; col++)
            {
                var label = NewText(_ui.transform, "", 24);
                label.color = RowColor((FormationRow)col);
                label.lineSpacing = 0.9f;
                var sh = label.gameObject.AddComponent<Shadow>();
                sh.effectColor = new Color(0f, 0f, 0f, 0.9f);
                label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                label.rectTransform.sizeDelta = new Vector2(300f, 60f);
                _rowLabels[col] = label;
            }
            // 화면 좌표 기준(캔버스 가운데 원점)으로 놓기 위해 열 이름·파티원 이름은 _ui(가운데 원점) 아래

            // 아래: 전투 시작
            var btnImg = NewImage(_ui.transform, new Color(0.62f, 0.4f, 0.12f, 1f));
            btnImg.raycastTarget = true;
            Place(btnImg.rectTransform, new Vector2(0f, -430f), new Vector2(420f, 88f));
            var outline = btnImg.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.85f, 0.4f, 0.8f);
            outline.effectDistance = new Vector2(2f, -2f);
            var btn = btnImg.gameObject.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            btn.onClick.AddListener(Confirm);
            var bt = NewText(btnImg.transform, "전투 시작  <size=20>(Space)</size>", 36);
            var brt = bt.rectTransform;
            brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one; brt.offsetMin = brt.offsetMax = Vector2.zero;

            _ui.SetActive(false);
        }

        SpriteRenderer NewSprite(string name, Sprite sprite, int order)
        {
            var go = new GameObject(name, typeof(SpriteRenderer));
            go.transform.SetParent(_tileRoot.transform, false);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingLayerID = 0;   // Default(바닥 그림 -30000 바로 위)
            sr.sortingOrder = order;
            return sr;
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
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true;
            t.raycastTarget = false;
            t.color = Color.white;
            return t;
        }

        static void Place(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }
    }
}
