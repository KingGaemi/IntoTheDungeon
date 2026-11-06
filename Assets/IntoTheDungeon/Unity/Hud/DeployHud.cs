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
    /// 끌지 않고 눌러서도 옮긴다: 캐릭터를 누르면 선택(발밑 칸이 하얗게 깜빡), 다른 캐릭터를 누르면 자리 바꿈, 빈 칸을 누르면 그리로 이동.
    /// 같은 캐릭터를 다시 누르거나 빈 바닥·우클릭·Esc면 선택 취소.
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
        Text _stopLine;
        RectTransform _stopIcon;
        // 첫 웨이브 적 칩 줄(적 종류마다 이름 ×수 + 망치·요구 저지력, 보스는 금색). 구성이 바뀔 때만 다시 만든다
        RectTransform _enemyRow;
        WaveSpawn[] _enemyBuiltFor;
        int _enemyBuiltWaves = -1;
        readonly List<(Image hammer, Text num, int req)> _stopMarks = new();   // 파티 최고 저지력과 비교해 청록/빨강
        readonly Text[] _rowLabels = new Text[Formation.Cols];
        Vector2 _grabOffset;
        float _time;

        // 눌러서 옮기기: 누른 캐릭터(아직 끌기인지 클릭인지 모름) · 선택된 캐릭터
        const float DragStartPixels = 12f;   // 이만큼 움직여야 끌기로 본다(그 전에 떼면 클릭)
        int _pressSlot = -1, _selectedSlot = -1;
        Vector2 _pressScreen;
        Vec2 _pressFeet;

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

            // 칸: 열 색, 누가 서 있으면 진하게, 끄는 중이면 놓일 칸이 깜빡. 눌러서 고른 캐릭터의 칸은 하얗게, 마우스 아래 칸이 놓일 곳
            if (_selectedSlot >= run.Members.Count) _selectedSlot = -1;
            int selectedCell = _selectedSlot >= 0 ? run.CellOf(_selectedSlot) : -1;
            int target = _progress.DragSlot >= 0 ? NearestCell(_progress.DragPos, middle)
                       : _selectedSlot >= 0 ? TileAt(MouseWorld(cam), middle) : -1;
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
                if (c == selectedCell) { t.Ring.color = Color.Lerp(Color.white, col, 0.3f * pulse); t.Fill.color = new Color(1f, 1f, 1f, 0.3f + 0.2f * pulse); }
                float s = hot || c == selectedCell ? 1.08f : 1f;
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

            UpdateEnemyRow();
            _stopLine.text = PartyStopLine(run);
            _stopIcon.anchoredPosition = new Vector2(16f - _stopLine.preferredWidth * 0.5f - 22f, 336f);   // 글자 바로 왼쪽
            if (_stopIcon.gameObject.activeSelf != _stopLine.text.Length > 0) _stopIcon.gameObject.SetActive(_stopLine.text.Length > 0);

            if (_progress.DragSlot < 0 && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
                Confirm();
        }

        Vec2 MouseWorld(Camera cam)
        {
            if (!cam) return default;
            var w = cam.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, -cam.transform.position.z));
            return new Vec2(w.x, w.y);   // 화면 y = 깊이(높이 0)
        }

        void HandleInput(RunData run, Camera cam, Vec2 middle)
        {
            if (!cam) return;
            var mouse = MouseWorld(cam);

            if (_progress.DragSlot < 0)
            {
                if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape)) { _selectedSlot = -1; _pressSlot = -1; return; }

                if (Input.GetMouseButtonDown(0))
                {
                    if (EventSystem.current && EventSystem.current.IsPointerOverGameObject()) return;   // 버튼 위 클릭
                    int slot = Pick(mouse, middle, run, out var feet);
                    if (slot >= 0)
                    {
                        _pressSlot = slot;   // 끌기인지 클릭인지는 움직임으로
                        _pressScreen = Input.mousePosition;
                        _pressFeet = feet;
                        _grabOffset = new Vector2(feet.X - mouse.X, feet.Y - mouse.Y);
                        return;
                    }
                    // 캐릭터가 없는 곳: 고른 캐릭터가 있으면 그 칸으로, 아니면 선택 취소
                    int tile = TileAt(mouse, middle);
                    if (_selectedSlot >= 0 && tile >= 0) run.PlaceInCell(_selectedSlot, tile);
                    _selectedSlot = -1;
                    return;
                }

                if (_pressSlot < 0) return;
                if (Input.GetMouseButton(0))
                {
                    // 충분히 움직이면 끌기 시작(선택은 풀림)
                    if (((Vector2)Input.mousePosition - _pressScreen).magnitude >= DragStartPixels)
                    {
                        _progress.DragSlot = _pressSlot;
                        _progress.DragPos = _pressFeet;
                        _pressSlot = -1;
                        _selectedSlot = -1;
                    }
                    return;
                }

                // 움직이지 않고 뗐다 = 클릭: 고르기 / 같은 캐릭터면 취소 / 다른 캐릭터면 자리 바꿈
                int clicked = _pressSlot;
                _pressSlot = -1;
                if (_selectedSlot < 0) _selectedSlot = clicked;
                else if (_selectedSlot == clicked) _selectedSlot = -1;
                else
                {
                    int cell = run.CellOf(clicked);
                    if (cell >= 0) run.PlaceInCell(_selectedSlot, cell);   // 그 칸에 놓으면 둘이 자리를 바꾼다
                    _selectedSlot = -1;
                }
                return;
            }

            var drag = new Vec2(mouse.X + _grabOffset.x, mouse.Y + _grabOffset.y);
            if (_area != null) drag = _area.Clamp(drag);
            _progress.DragPos = drag;
            if (Input.GetMouseButton(0)) return;

            int dropCell = NearestCell(drag, middle);
            if (dropCell >= 0) run.PlaceInCell(_progress.DragSlot, dropCell);   // 칸 밖에 놓으면 원래 자리로
            _progress.DragSlot = -1;
        }

        // 그 점이 들어 있는 칸(없으면 -1)
        int TileAt(Vec2 p, Vec2 middle)
        {
            for (int c = 0; c < Formation.Cells; c++)
                if (InTile(p, Formation.CellPos(c, middle, _area))) return c;
            return -1;
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
            _selectedSlot = _pressSlot = -1;
            _progress.DeployConfirmed = true;
        }

        // 첫 웨이브 적 줄: 적 종류마다 칩(이름 ×수, 패턴이 있으면 망치+요구 저지력, 되살아남 ×N), 끝에 웨이브 수.
        // 보스 이름은 금색. 칩에 마우스를 올리면 Tooltips.Enemy(패턴 이름·요구 저지력 청록/빨강·특성). 구성(첫 웨이브 배열)이 바뀔 때만 다시 만든다
        void UpdateEnemyRow()
        {
            if (_config == null || !_enemyRow) return;
            var first = _config.Waves.Count > 0 ? _config.Waves[0] : null;
            if (first != _enemyBuiltFor || _config.Waves.Count != _enemyBuiltWaves)
            {
                _enemyBuiltFor = first;
                _enemyBuiltWaves = _config.Waves.Count;
                _stopMarks.Clear();
                for (int i = _enemyRow.childCount - 1; i >= 0; i--) Destroy(_enemyRow.GetChild(i).gameObject);
                if (first != null) BuildEnemyRow(first);
            }
            if (_stopMarks.Count == 0) return;
            int best = BestPartyStop();
            foreach (var (hammer, num, req) in _stopMarks)
            {
                var col = best >= req ? StopPowerUi.Color : StopPowerUi.ShortColor;   // 모자라면 빨강(부정 신호)
                if (hammer.color != col) { hammer.color = col; num.color = col; }
            }
        }

        void BuildEnemyRow(WaveSpawn[] first)
        {
            var counts = new Dictionary<string, int>();
            var order = new List<WaveSpawn>();
            foreach (var s in first)
            {
                string key = s.Recipe + (s.Boss ? "!" : "");
                if (!counts.ContainsKey(key)) { counts[key] = 0; order.Add(s); }
                counts[key] += s.Count;
            }
            var chips = new List<RectTransform>();
            var widths = new List<float>();
            foreach (var s in order)
            {
                chips.Add(BuildEnemyChip(s, counts[s.Recipe + (s.Boss ? "!" : "")], out float w));
                widths.Add(w);
            }
            var wave = NewText(_enemyRow, $"웨이브 {_config.Waves.Count}", 20);
            wave.color = new Color(1f, 1f, 1f, 0.6f);
            chips.Add(wave.rectTransform);
            widths.Add(wave.preferredWidth + 8f);

            // 가운데 정렬로 가로 배치
            const float gap = 30f;
            float total = -gap;
            foreach (var w in widths) total += w + gap;
            float x = -total * 0.5f;
            for (int i = 0; i < chips.Count; i++)
            {
                Place(chips[i], new Vector2(x + widths[i] * 0.5f, 0f), new Vector2(widths[i], 32f));
                x += widths[i] + gap;
            }
        }

        RectTransform BuildEnemyChip(WaveSpawn s, int count, out float width)
        {
            var bg = NewImage(_enemyRow, new Color(0f, 0f, 0f, 0f));   // 마우스 받는 투명 판(툴팁)
            bg.gameObject.name = "Enemy";
            var root = bg.transform;
            var def = _catalog != null ? _catalog.Get(s.Recipe) : CharacterDefinition.Default;
            string name = _catalog != null ? _catalog.DisplayName(s.Recipe) : s.Recipe;

            var label = NewText(root, $"{name} ×{count}", 22);
            label.color = s.Boss ? UiKit.Gold : new Color(1f, 0.72f, 0.6f, 1f);   // 보스 이름 금색
            label.alignment = TextAnchor.MiddleLeft;
            float w = label.preferredWidth;
            LeftPlace(label.rectTransform, 0f, w);

            // 패턴이 있으면 망치 + 요구 저지력(보스는 내려찍기 기준). 색은 UpdateEnemyRow가 파티 최고 저지력과 비교해 정한다
            int req = s.Boss ? Boss.StopRequired : def.SlamRadius > 0f ? def.SlamStopRequired : 0;
            if (req > 0)
            {
                var hammer = NewImage(root, StopPowerUi.Color);
                hammer.sprite = StopPowerUi.Icon;
                hammer.preserveAspect = true;
                LeftPlace(hammer.rectTransform, w + 8f, 22f, 22f);
                var num = NewText(root, req.ToString(), 22);
                num.color = StopPowerUi.Color;
                num.alignment = TextAnchor.MiddleLeft;
                float nw = num.preferredWidth;
                LeftPlace(num.rectTransform, w + 33f, nw);
                _stopMarks.Add((hammer, num, req));
                w += 33f + nw;
            }
            // 되살아남 ×N
            if (def.ReviveCharges > 0)
            {
                var revive = NewImage(root, Color.white);
                revive.sprite = UiIcons.Of(UiIcons.Revive);
                revive.preserveAspect = true;
                if (!revive.sprite) revive.enabled = false;
                LeftPlace(revive.rectTransform, w + 8f, 22f, 22f);
                var num = NewText(root, $"×{def.ReviveCharges}", 22);
                num.color = new Color(1f, 1f, 1f, 0.85f);
                num.alignment = TextAnchor.MiddleLeft;
                float nw = num.preferredWidth;
                LeftPlace(num.rectTransform, w + 33f, nw);
                w += 33f + nw;
            }
            width = w;

            bool boss = s.Boss;
            var captured = def;
            UiTooltip.Bind(bg.gameObject, () => Tooltips.Enemy(captured, boss, BestPartyStop()));
            return bg.rectTransform;
        }

        // 칩 안 왼쪽 정렬 배치(왼쪽 가운데 앵커)
        static void LeftPlace(RectTransform rt, float x, float w, float h = 32f)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(x, 0f);
            rt.sizeDelta = new Vector2(w, h);
        }

        /// <summary>살아 있는 파티원 필살기 중 가장 높은 저지력(_party는 Update가 매 프레임 모아 둔다. 월드가 없으면 -1 = 비교 안 함).</summary>
        int BestPartyStop()
        {
            if (_world == null) return -1;
            var em = _world.EntityManager;
            int best = 0;
            foreach (var m in _party)
                if (m.Alive && em.TryGetComponent<IntoTheDungeon.Features.Attack.SpecialSkillComponent>(m.Entity, out var sk) && sk.StopPower > best) best = sk.StopPower;
            return best;
        }

        // 파티원별 필살기 저지력(장비 포함): 적 강타의 「저지 N」과 바로 비교할 수 있게
        string PartyStopLine(RunData run)
        {
            var em = _world.EntityManager;
            var sb = new StringBuilder("필살기 저지력  ");
            int n = 0;
            foreach (var m in _party)
            {
                if (m.Slot >= Party.NoSlot || !m.Alive || m.Slot >= run.Members.Count) continue;
                if (!em.TryGetComponent<IntoTheDungeon.Features.Attack.SpecialSkillComponent>(m.Entity, out var sk)) continue;
                string who = _catalog != null ? _catalog.DisplayName(run.Members[m.Slot]) : run.Members[m.Slot];
                sb.Append(n++ > 0 ? "   ·   " : "").Append(who).Append(' ').Append(sk.StopPower);
            }
            return n > 0 ? sb.ToString() : "";
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
            var band = UiKit.Panel(NewImage(_ui.transform, Color.white), new Color(0.05f, 0.055f, 0.09f, 0.86f));
            Place(band.rectTransform, new Vector2(0f, 420f), new Vector2(1500f, 150f));
            var title = UiKit.Soft(NewText(_ui.transform, "전투 배치", 44));
            title.color = new Color(1f, 0.86f, 0.45f, 1f);
            Place(title.rectTransform, new Vector2(0f, 455f), new Vector2(800f, 56f));
            var hint = NewText(_ui.transform, "", 21);
            hint.color = new Color(1f, 1f, 1f, 0.7f);
            Place(hint.rectTransform, new Vector2(0f, 410f), new Vector2(1450f, 32f));
            _enemyRow = new GameObject("Enemies", typeof(RectTransform)).GetComponent<RectTransform>();
            _enemyRow.SetParent(_ui.transform, false);
            Place(_enemyRow, new Vector2(0f, 372f), new Vector2(1450f, 32f));
            // 파티 저지력 줄(청록 망치 + 파티원별 숫자)
            band.rectTransform.sizeDelta = new Vector2(1500f, 190f);
            band.rectTransform.anchoredPosition = new Vector2(0f, 400f);
            _stopLine = NewText(_ui.transform, "", 22);
            _stopLine.color = StopPowerUi.Color;
            Place(_stopLine.rectTransform, new Vector2(16f, 336f), new Vector2(1450f, 32f));
            var stopIcon = NewImage(_ui.transform, StopPowerUi.Color);
            stopIcon.sprite = StopPowerUi.Icon;
            _stopIcon = stopIcon.rectTransform;
            Place(_stopIcon, new Vector2(-300f, 336f), new Vector2(28f, 28f));

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
            var btn = btnImg.gameObject.AddComponent<Button>();
            UiKit.Button(btn, btnImg, new Color(0.62f, 0.4f, 0.12f, 1f), new Color(1f, 0.85f, 0.4f, 1f));
            btn.onClick.AddListener(Confirm);
            var bt = UiKit.Soft(NewText(btnImg.transform, "전투 시작  <size=20>Space</size>", 36));
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
