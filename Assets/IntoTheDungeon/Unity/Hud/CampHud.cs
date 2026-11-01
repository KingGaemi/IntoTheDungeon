using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Features.Battle;
using IntoTheDungeon.Features.Run;
using IntoTheDungeon.Features.Character;
using IntoTheDungeon.Features.Stats;
using IntoTheDungeon.Features.Status;

namespace IntoTheDungeon.Unity.Hud
{
    /// <summary>
    /// 전투에서 이긴 뒤의 정비 화면(임시 UI, 코드로 만든다).
    /// 왼쪽: 보상 카드 3장(고른 뒤 「선택 확정」), 파티원별 스탯 카드, 덱 요약.
    /// 오른쪽: 파티원별 장비 칸(4×3, 끼운 파티원에게만 적용), 파티 보관함(6×3, 효과 없음), 이번 전투에서 주운 아이템.
    /// 아이템은 칸 사이를 끌어서 옮기고(끄는 중 우클릭 = 회전), 버리기 칸에 놓으면 버린다. 방을 고르면 못 넣은 아이템은 사라진다.
    /// </summary>
    public sealed class CampHud : MonoBehaviour, IWorldInjectable
    {
        const int RewardSlots = 3;
        const float Cell = 50f;   // 격자 한 칸(픽셀)

        [SerializeField] Color panelColor = new(0.12f, 0.14f, 0.22f, 1f);
        [SerializeField] Color cardColor = new(0.2f, 0.24f, 0.36f, 1f);
        [SerializeField] Color accent = new(1f, 0.82f, 0.3f, 1f);
        [SerializeField] Color recruitColor = new(0.25f, 0.45f, 0.6f, 1f);
        [SerializeField] Color cellColor = new(0.08f, 0.09f, 0.14f, 1f);

        sealed class RewardSlot
        {
            public Image Frame, Body;
            public Text Kind, Title, Desc;
            public Button Button;
            public CardDef Card;
        }

        sealed class MemberCard
        {
            public GameObject Root;
            public Image Body;
            public Text Name, Stats, XpText;
            public RectTransform XpFill;
        }

        readonly RewardSlot[] _rewards = new RewardSlot[RewardSlots];
        readonly MemberCard[] _members = new MemberCard[CardPool.MaxPartySize];
        readonly List<InventoryItemView> _views = new();

        // 아이템을 놓을 수 있는 격자 하나(파티원 장비 칸 또는 보관함)
        sealed class GridView
        {
            public RectTransform Rect;
            public GameObject Block;   // 장비 칸 묶음(이름 포함) — 파티원이 없으면 숨김
            public Text Label;
            public int Slot;           // 파티 슬롯, 보관함은 -1
            public Inventory Inv;
        }
        readonly GridView[] _gear = new GridView[CardPool.MaxPartySize];
        GridView _stash;

        BattleProgress _progress;
        CharacterCatalog _catalog;
        GameObject _root;
        RectTransform _lootArea, _discard, _dragLayer;
        Text _partyTitle, _header, _deck, _info, _toMapLabel;
        Button _confirm;
        Font _font;
        int _selected = -1;
        bool _wasShown;

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
            bool show = _progress.Phase == BattlePhase.Map && run.Phase == RunPhase.Camp;
            if (_root.activeSelf != show) _root.SetActive(show);
            if (!show) { _wasShown = false; return; }
            if (!_wasShown) { _wasShown = true; _selected = -1; RebuildItems(); ShowInfo(null); }

            var head = run.Current != null
                ? $"{run.CurrentLayer + 1}층 {Encounters.Label(run.Current.Type)} 클리어   ·   경험치 +{run.LastXp}   ·   골드 +{run.LastGold}   ·   전투 후 회복 +{Mathf.RoundToInt(run.LastHeal * 100f)}%"
                : "";
            if (run.LastLevelUps.Count > 0)
            {
                var ups = new System.Text.StringBuilder("   ·   레벨 업! ");
                for (int i = 0; i < run.LastLevelUps.Count; i++)
                {
                    int slot = run.LastLevelUps[i];
                    ups.Append(i == 0 ? "" : ", ").Append(NameOf(run, slot)).Append($" Lv.{run.LevelOf(slot)}");
                }
                head += ups;
            }
            _header.text = head;
            _partyTitle.text = $"파티 ({run.PartySize} / {CardPool.MaxPartySize})   ·   골드 {run.Gold}";
            _toMapLabel.text = run.Loot.Count > 0 ? $"지도로 (방을 고르면 못 넣은 아이템 {run.Loot.Count}개는 버려짐)" : "지도로";

            UpdateRewards(run);
            UpdateMembers(run);
            UpdateDeck(run);
        }

        // ───────── 보상 ─────────

        void UpdateRewards(RunData run)
        {
            bool taken = run.RewardTaken;
            for (int i = 0; i < RewardSlots; i++)
            {
                var slot = _rewards[i];
                var card = i < run.PendingRewards.Count ? run.PendingRewards[i] : null;
                slot.Card = card;
                slot.Frame.gameObject.SetActive(card != null);
                if (card == null) continue;

                bool mine = taken && run.Deck.Count > 0 && run.Deck[^1] == card;
                bool chosen = !taken && i == _selected;
                slot.Kind.text = card.Kind == CardKind.Recruit ? "동료 영입" : "패시브";
                slot.Title.text = card.Title;
                slot.Desc.text = mine ? $"{card.Description}\n(받음)" : card.Description;
                slot.Body.color = card.Kind == CardKind.Recruit ? recruitColor : cardColor;
                slot.Frame.color = mine || chosen ? accent : taken ? new Color(0f, 0f, 0f, 0.6f) : new Color(1f, 1f, 1f, 0.3f);
                float a = taken && !mine ? 0.4f : 1f;
                slot.Title.color = new Color(1f, 1f, 1f, a);
                slot.Desc.color = new Color(1f, 1f, 1f, 0.8f * a);
                slot.Button.interactable = !taken;
            }
            _confirm.interactable = !taken && _selected >= 0 && _selected < run.PendingRewards.Count;
            _confirm.GetComponentInChildren<Text>().text = taken ? "보상 받음" : _selected >= 0 ? "선택 확정" : "카드를 고르세요";
        }

        void Select(int index) => _selected = index;

        void Confirm()
        {
            var run = RunState.Current;
            if (_selected < 0 || _selected >= run.PendingRewards.Count) return;
            run.TakeReward(run.PendingRewards[_selected]);
            _selected = -1;
        }

        // ───────── 파티 ─────────

        // 정비 화면엔 캐릭터가 없으므로 캐릭터 정의(레벨 성장 포함) + 덱·장비 보너스로 전투 때와 같은 계산을 해서 보여 준다
        void UpdateMembers(RunData run)
        {
            for (int i = 0; i < _members.Length; i++)
            {
                var card = _members[i];
                bool exists = i < run.PartySize;
                if (card.Root.activeSelf != exists) card.Root.SetActive(exists);
                if (!exists) continue;

                string recipe = run.Members[i];
                var def = _catalog != null ? _catalog.Get(recipe) : CharacterDefinition.Default;
                var mods = run.BuildModifiers(i);   // 덱 + 이 파티원 장비
                int level = run.LevelOf(i);
                var leveled = PrimaryStats.AtLevel(def.Stats, def.Growth, level);
                var st = new StatusComponent();
                var d = new DerivedStatsComponent();
                StatFormula.Apply(leveled + mods.StatBonus, ref st, ref d);

                float ratio = i < run.PartyHp.Count ? run.PartyHp[i] : 1f;
                bool fallen = ratio <= 0f;
                card.Name.text = $"{i + 1}. {NameOf(run, i)}";
                card.Body.color = fallen ? new Color(0.14f, 0.12f, 0.14f, 1f) : cardColor;
                int xp = run.XpOf(i), need = RunData.XpToNext(level);
                card.XpFill.anchorMax = new Vector2(Mathf.Clamp01((float)xp / need), 1f);
                card.XpText.text = $"Lv.{level}   경험치 {xp} / {need}";
                card.Stats.text =
                    (fallen ? "쓰러짐 (휴식 방에서 회복)\n" : $"체력 {Mathf.RoundToInt(st.MaxHp * ratio)} / {st.MaxHp}\n") +
                    $"힘 {Stat(leveled.Str, mods.StatBonus.Str)}  민첩 {Stat(leveled.Agi, mods.StatBonus.Agi)}\n" +
                    $"지능 {Stat(leveled.Int, mods.StatBonus.Int)}  정신 {Stat(leveled.Wil, mods.StatBonus.Wil)}\n" +
                    $"공격 {st.Damage}  공속 {st.AttackSpeed:0.00}\n" +
                    $"방어 {d.Armor:0}  이속 {st.MovementSpeed:0.0}\n" +
                    $"마나 {d.MaxMana:0} (+{d.ManaRegen:0.0}/초)\n" +
                    $"체력회복 {d.HpRegen:0.0}/초\n" +
                    $"마법저항 {d.MagicResist:0}\n" +
                    $"넉백·CC저항 {d.KnockbackResist * 100f:0}%·{d.CcResist * 100f:0}%";
                card.Stats.color = fallen ? new Color(1f, 1f, 1f, 0.45f) : Color.white;
            }
        }

        string NameOf(RunData run, int slot)
        {
            string recipe = slot < run.Members.Count ? run.Members[slot] : null;
            return _catalog != null ? _catalog.DisplayName(recipe) : recipe;
        }

        // 기본값에 보너스가 있으면 "8 (+3)"처럼
        static string Stat(int baseValue, int bonus) => bonus != 0 ? $"{baseValue + bonus} (+{bonus})" : baseValue.ToString();

        void UpdateDeck(RunData run)
        {
            var counts = new Dictionary<string, int>();
            var order = new List<CardDef>();
            foreach (var c in run.Deck)
            {
                if (!counts.ContainsKey(c.Id)) { counts[c.Id] = 0; order.Add(c); }
                counts[c.Id]++;
            }
            var sb = new System.Text.StringBuilder("덱: ");
            for (int i = 0; i < order.Count; i++)
                sb.Append(i == 0 ? "" : ", ").Append(order[i].Title).Append(counts[order[i].Id] > 1 ? $" ×{counts[order[i].Id]}" : "");
            _deck.text = order.Count > 0 ? sb.ToString() : "덱: 아직 카드가 없습니다";
        }

        // ───────── 장비 칸·보관함·주운 아이템 ─────────

        /// <summary>판 데이터(장비 칸·보관함·주운 아이템)대로 아이템 그림을 전부 새로 만든다.</summary>
        void RebuildItems()
        {
            foreach (var v in _views) if (v) Destroy(v.gameObject);
            _views.Clear();
            var run = RunState.Current;

            // 격자마다 판 데이터 연결(파티원 수가 바뀌었을 수 있음)
            for (int i = 0; i < _gear.Length; i++)
            {
                bool exists = i < run.PartySize;
                _gear[i].Block.SetActive(exists);
                _gear[i].Inv = exists ? run.GearOf(i) : null;
                if (exists) _gear[i].Label.text = $"{i + 1}. {NameOf(run, i)}  Lv.{run.LevelOf(i)}";
            }
            _stash.Inv = run.Stash;

            foreach (var g in AllGrids())
            {
                if (g.Inv == null) continue;
                foreach (var p in g.Inv.Items)
                {
                    var v = NewItemView(p.Def, p, p.Rotated, g.Rect);
                    v.Source = g.Inv;
                    ((RectTransform)v.transform).anchoredPosition = CellToGrid(g.Inv, p.X, p.Y, p.W, p.H);
                }
            }

            // 주운 아이템은 왼쪽부터 나란히(칸 크기 그대로)
            float x = 10f;
            foreach (var def in run.Loot)
            {
                var v = NewItemView(def, null, false, _lootArea);
                var rt = (RectTransform)v.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
                rt.anchoredPosition = new Vector2(x + def.W * Cell * 0.5f, 0f);
                x += def.W * Cell + 10f;
            }
        }

        InventoryItemView NewItemView(ItemDef def, PlacedItem placed, bool rotated, RectTransform parent)
        {
            var img = NewImage(parent, LootRenderer.RarityColor(def.Rarity));
            img.raycastTarget = true;
            var go = img.gameObject;
            go.name = def.Name;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);

            // 등급 색 바탕 위에 아이콘(없으면 이름 글자)
            var back = NewImage(go.transform, new Color(0.06f, 0.06f, 0.09f, 0.85f));
            var brt = back.rectTransform;
            brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
            brt.offsetMin = new Vector2(3f, 3f); brt.offsetMax = new Vector2(-3f, -3f);
            var sprite = ItemIconTable.Icon(def.Id);
            if (sprite)
            {
                var icon = NewImage(back.transform, Color.white);
                icon.sprite = sprite;
                icon.preserveAspect = true;
                var irt = icon.rectTransform;
                irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
                irt.offsetMin = new Vector2(4f, 4f); irt.offsetMax = new Vector2(-4f, -4f);
            }
            else
            {
                var label = NewText(back.transform, def.Name, 16);
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                Stretch(label.rectTransform);
            }

            var v = go.AddComponent<InventoryItemView>();
            v.Owner = this;
            v.Def = def;
            v.Placed = placed;
            v.Rotated = rotated;
            ResizeView(v);
            _views.Add(v);
            return v;
        }

        public void ResizeView(InventoryItemView v)
        {
            int w = v.Rotated ? v.Def.H : v.Def.W, h = v.Rotated ? v.Def.W : v.Def.H;
            ((RectTransform)v.transform).sizeDelta = new Vector2(w * Cell - 4f, h * Cell - 4f);
        }

        IEnumerable<GridView> AllGrids()
        {
            foreach (var g in _gear) yield return g;
            yield return _stash;
        }

        // 격자 칸 (x, y)(왼쪽 위, y 아래로 증가)에 w×h 아이템을 놓을 때의 중심 위치(격자 기준, 가운데 원점)
        static Vector2 CellToGrid(int cols, int rows, int x, int y, int w, int h)
            => new(-cols * Cell * 0.5f + (x + w * 0.5f) * Cell,
                    rows * Cell * 0.5f - (y + h * 0.5f) * Cell);

        static Vector2 CellToGrid(Inventory inv, int x, int y, int w, int h) => CellToGrid(inv.Cols, inv.Rows, x, y, w, h);

        public void BeginDrag(InventoryItemView v)
        {
            v.transform.SetParent(_dragLayer, true);   // 다른 패널 위로
            v.transform.SetAsLastSibling();
            var rt = (RectTransform)v.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);   // 끄는 동안 포인터 위치 계산 기준(층 가운데)
            ShowInfo(v.Def);
        }

        public void Drop(InventoryItemView v, Vector2 screen)
        {
            var run = RunState.Current;
            var cam = (Camera)null;   // Screen Space Overlay
            int w = v.Rotated ? v.Def.H : v.Def.W, h = v.Rotated ? v.Def.W : v.Def.H;

            GridView target = null;
            foreach (var g in AllGrids())
                if (g.Inv != null && g.Block.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(g.Rect, screen, cam)) { target = g; break; }

            if (target != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(target.Rect, screen, cam, out var local))
            {
                // 포인터를 아이템 가운데로 보고 왼쪽 위 칸을 구한다
                var inv = target.Inv;
                int cx = Mathf.RoundToInt((local.x + inv.Cols * Cell * 0.5f) / Cell - w * 0.5f);
                int cy = Mathf.RoundToInt((inv.Rows * Cell * 0.5f - local.y) / Cell - h * 0.5f);
                if (v.Source == inv) inv.Move(v.Placed, cx, cy, v.Rotated);   // 같은 격자 안에서 옮기기
                else if (inv.CanPlace(v.Def, cx, cy, v.Rotated))
                {
                    if (v.Source != null) v.Source.Remove(v.Placed);          // 다른 격자에서 옮겨 옴
                    else run.Loot.Remove(v.Def);                             // 주운 아이템에서
                    inv.Place(v.Def, cx, cy, v.Rotated);
                }
            }
            else if (RectTransformUtility.RectangleContainsScreenPoint(_discard, screen, cam))
            {
                if (v.Source != null) v.Source.Remove(v.Placed);
                else run.Loot.Remove(v.Def);
            }
            else if (v.Source != null && RectTransformUtility.RectangleContainsScreenPoint(_lootArea, screen, cam))
            {
                v.Source.Remove(v.Placed);   // 격자에서 꺼내 주운 아이템 칸으로
                run.Loot.Add(v.Def);
            }
            // 그 밖(놓을 수 없는 곳)은 원래 자리로: 데이터를 안 바꾸고 다시 그리면 된다
            RebuildItems();
        }

        public void ShowInfo(ItemDef def)
        {
            if (!_info) return;
            _info.text = def == null
                ? "아이템에 마우스를 올리면 효과가 보입니다.\n장비 칸에 끼운 파티원에게만 효과가 있고, 보관함은 보관만 합니다.\n끌어서 옮기기 · 끄는 중 우클릭: 회전"
                : $"{def.Name}\n[{ItemDef.RarityLabel(def.Rarity)} · {def.W}×{def.H}칸]\n\n{def.Description}";
        }

        void Leave() => RunState.Current.LeaveCamp();   // 같은 씬에서 바로 지도 화면으로

        // ───────── 화면 만들기 ─────────

        void Build()
        {
            _root = new GameObject("CampCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _root.transform.SetParent(transform, false);
            var canvas = _root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 210;
            var scaler = _root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var bg = NewImage(_root.transform, new Color(0.04f, 0.04f, 0.08f, 1f));
            bg.raycastTarget = true;
            Stretch(bg.rectTransform);

            Place(NewText(_root.transform, "정비", 52).rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(600f, 64f));
            _header = NewText(_root.transform, "", 26);
            _header.color = accent;
            Place(_header.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -92f), new Vector2(1200f, 36f));

            // 왼쪽: 보상
            var reward = Panel(new Vector2(-480f, 255f), new Vector2(900f, 290f), "보상 (카드를 고른 뒤 확정)");
            for (int i = 0; i < RewardSlots; i++)
                _rewards[i] = BuildRewardSlot(reward, i, new Vector2((i - 1) * 285f, -12f), new Vector2(265f, 170f));
            _confirm = NewButton(reward, "선택 확정", new Vector2(0f, -120f), new Vector2(260f, 40f), accent, Confirm);

            // 왼쪽: 파티
            var party = Panel(new Vector2(-480f, -170f), new Vector2(900f, 530f), "파티");
            _partyTitle = party.GetComponentInChildren<Text>();
            for (int i = 0; i < _members.Length; i++)
                _members[i] = BuildMemberCard(party, new Vector2(-330f + i * 220f, -10f), new Vector2(212f, 410f));
            _deck = NewText(party, "", 18);
            _deck.alignment = TextAnchor.MiddleLeft;
            _deck.horizontalOverflow = HorizontalWrapMode.Wrap;
            Place(_deck.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(860f, 40f));

            // 오른쪽: 파티원별 장비 칸
            var gear = Panel(new Vector2(480f, 205f), new Vector2(900f, 370f), "장비 (끼운 파티원에게만 적용)");
            for (int i = 0; i < _gear.Length; i++)
            {
                var block = new GameObject($"Gear{i + 1}", typeof(RectTransform)).GetComponent<RectTransform>();
                block.SetParent(gear, false);
                Place(block, new Vector2(0.5f, 0.5f), new Vector2(-330f + i * 220f, -25f), new Vector2(210f, 240f));
                var label = NewText(block, "", 20);
                label.color = accent;
                Band(label.rectTransform, 1f, 0f, 30f);
                var g = BuildGrid(block, new Vector2(0f, -20f), Inventory.GearCols, Inventory.GearRows);
                g.Block = block.gameObject;
                g.Label = label;
                g.Slot = i;
                _gear[i] = g;
            }

            // 오른쪽: 보관함 + 아이템 정보
            var stash = Panel(new Vector2(480f, -95f), new Vector2(900f, 210f), "보관함 (효과 없음)");
            _stash = BuildGrid(stash, new Vector2(-260f, -20f), Inventory.StashCols, Inventory.StashRows);
            _stash.Block = _stash.Rect.gameObject;
            _stash.Slot = -1;
            _info = NewText(stash, "", 19);
            _info.alignment = TextAnchor.UpperLeft;
            _info.horizontalOverflow = HorizontalWrapMode.Wrap;
            Place(_info.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(180f, -20f), new Vector2(480f, 150f));

            // 오른쪽: 주운 아이템 + 버리기
            var loot = Panel(new Vector2(480f, -330f), new Vector2(900f, 220f), "주운 아이템 (끌어서 장비 칸이나 보관함에)");
            _lootArea = NewImage(loot, new Color(0f, 0f, 0f, 0.35f)).rectTransform;
            _lootArea.GetComponent<Image>().raycastTarget = true;
            Place(_lootArea, new Vector2(0.5f, 0.5f), new Vector2(-95f, -20f), new Vector2(660f, 155f));
            var discard = NewImage(loot, new Color(0.25f, 0.12f, 0.12f, 1f));
            discard.raycastTarget = true;
            _discard = discard.rectTransform;
            Place(_discard, new Vector2(0.5f, 0.5f), new Vector2(345f, -20f), new Vector2(150f, 155f));
            var dl = NewText(_discard, "버리기\n(여기에 놓기)", 22);
            Stretch(dl.rectTransform);

            // 지도로
            var toMap = NewButton(_root.transform, "지도로", new Vector2(0f, -508f), new Vector2(520f, 52f), accent, Leave);
            _toMapLabel = toMap.GetComponentInChildren<Text>();

            // 끄는 아이템이 모든 패널 위에 보이게 하는 층(맨 마지막 자식)
            _dragLayer = new GameObject("DragLayer", typeof(RectTransform)).GetComponent<RectTransform>();
            _dragLayer.SetParent(_root.transform, false);
            Stretch(_dragLayer);

            _root.SetActive(false);
        }

        GridView BuildGrid(RectTransform parent, Vector2 pos, int cols, int rows)
        {
            var rect = new GameObject("Grid", typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            Place(rect, new Vector2(0.5f, 0.5f), pos, new Vector2(cols * Cell, rows * Cell));
            var back = NewImage(rect, new Color(0f, 0f, 0f, 0.5f));
            back.raycastTarget = true;   // 드롭 판정 영역
            Stretch(back.rectTransform);
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < cols; x++)
                {
                    var c = NewImage(rect, cellColor);
                    Place(c.rectTransform, new Vector2(0.5f, 0.5f), CellToGrid(cols, rows, x, y, 1, 1), new Vector2(Cell - 4f, Cell - 4f));
                }
            return new GridView { Rect = rect };
        }

        Button NewButton(Transform parent, string text, Vector2 pos, Vector2 size, Color color, UnityEngine.Events.UnityAction onClick)
        {
            var img = NewImage(parent, color);
            img.raycastTarget = true;
            Place(img.rectTransform, new Vector2(0.5f, 0.5f), pos, size);
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);
            var t = NewText(img.transform, text, 24);
            t.color = new Color(0.1f, 0.1f, 0.15f, 1f);
            Stretch(t.rectTransform);
            return btn;
        }

        MemberCard BuildMemberCard(RectTransform parent, Vector2 pos, Vector2 size)
        {
            var card = new MemberCard();
            card.Body = NewImage(parent, cardColor);
            card.Root = card.Body.gameObject;
            Place(card.Body.rectTransform, new Vector2(0.5f, 0.5f), pos, size);
            card.Name = NewText(card.Body.rectTransform, "", 22);
            card.Name.color = accent;
            Band(card.Name.rectTransform, 1f, -8f, 30f);
            // 경험치 바: 노란 채움 + "Lv.2  40 / 100"
            var xpBack = NewImage(card.Body.rectTransform, new Color(0f, 0f, 0f, 0.6f));
            Band(xpBack.rectTransform, 1f, -42f, 24f);
            card.XpFill = NewImage(xpBack.rectTransform, new Color(0.85f, 0.68f, 0.18f, 1f)).rectTransform;
            card.XpFill.anchorMin = Vector2.zero; card.XpFill.anchorMax = new Vector2(0f, 1f);
            card.XpFill.offsetMin = card.XpFill.offsetMax = Vector2.zero;
            card.XpText = NewText(xpBack.rectTransform, "", 16);
            var xrt = card.XpText.rectTransform;
            xrt.anchorMin = Vector2.zero; xrt.anchorMax = Vector2.one;
            xrt.offsetMin = xrt.offsetMax = Vector2.zero;

            card.Stats = NewText(card.Body.rectTransform, "", 17);
            card.Stats.alignment = TextAnchor.UpperLeft;
            card.Stats.lineSpacing = 1.1f;
            var srt = card.Stats.rectTransform;
            srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one;
            srt.offsetMin = new Vector2(12f, 8f); srt.offsetMax = new Vector2(-6f, -74f);
            card.Root.SetActive(false);
            return card;
        }

        RewardSlot BuildRewardSlot(RectTransform parent, int index, Vector2 pos, Vector2 size)
        {
            var slot = new RewardSlot();
            slot.Frame = NewImage(parent, Color.white);
            Place(slot.Frame.rectTransform, new Vector2(0.5f, 0.5f), pos, size);
            slot.Body = NewImage(slot.Frame.rectTransform, cardColor);
            var brt = slot.Body.rectTransform;
            brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
            brt.offsetMin = new Vector2(5f, 5f); brt.offsetMax = new Vector2(-5f, -5f);
            slot.Body.raycastTarget = true;

            slot.Kind = NewText(brt, "", 18);
            slot.Kind.color = accent;
            Band(slot.Kind.rectTransform, 1f, -8f, 26f);
            slot.Title = NewText(brt, "", 24);
            Band(slot.Title.rectTransform, 1f, -38f, 34f);
            slot.Desc = NewText(brt, "", 19);
            slot.Desc.alignment = TextAnchor.UpperCenter;
            slot.Desc.horizontalOverflow = HorizontalWrapMode.Wrap;
            var drt = slot.Desc.rectTransform;
            drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one;
            drt.offsetMin = new Vector2(10f, 8f); drt.offsetMax = new Vector2(-10f, -78f);

            slot.Button = slot.Frame.gameObject.AddComponent<Button>();
            slot.Button.targetGraphic = slot.Body;
            int i = index;
            slot.Button.onClick.AddListener(() => Select(i));
            return slot;
        }

        // 부모 위쪽에 붙는 가로 띠(top = 위에서 내려온 거리, 음수)
        static void Band(RectTransform rt, float anchorY, float top, float height)
        {
            rt.anchorMin = new Vector2(0f, anchorY); rt.anchorMax = new Vector2(1f, anchorY); rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(8f, top - height); rt.offsetMax = new Vector2(-8f, top);
        }

        RectTransform Panel(Vector2 pos, Vector2 size, string title)
        {
            var panel = NewImage(_root.transform, panelColor);
            var rt = panel.rectTransform;
            Place(rt, new Vector2(0.5f, 0.5f), pos, size);
            var t = NewText(rt, title, 26);
            t.color = accent;
            t.alignment = TextAnchor.UpperLeft;
            var trt = t.rectTransform;
            trt.anchorMin = new Vector2(0f, 1f); trt.anchorMax = new Vector2(1f, 1f); trt.pivot = new Vector2(0.5f, 1f);
            trt.offsetMin = new Vector2(24f, -50f); trt.offsetMax = new Vector2(-24f, -12f);
            return rt;
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
            t.raycastTarget = false;
            t.color = Color.white;
            return t;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }
    }
}
