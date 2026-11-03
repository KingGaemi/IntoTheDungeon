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
    public sealed partial class CampHud : MonoBehaviour, IWorldInjectable
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
            public Image Body, Portrait;
            public Text Name, Stats, XpText;
            public RectTransform XpFill;
            public GameObject Empty;   // 아직 동료가 없는 칸(점선 + 「+」)
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
        RectTransform _lootArea, _discard, _dragLayer, _rewardPanel;
        int _itemsVersion;
        Button _sellAll;
        GameObject _confirmBox;
        Text _confirmText;
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
            if (!show) { _wasShown = false; if (_confirmBox.activeSelf) _confirmBox.SetActive(false); return; }
            if (!_wasShown) { _wasShown = true; _selected = -1; RebuildItems(); ShowInfo(null); if (_shopMessage) _shopMessage.text = ""; }
            // 상점에서 산 아이템·영입한 동료가 보이게(끄는 중이 아닐 때)
            if (_itemsVersion != run.InventoryVersion && _dragLayer.childCount == 0) RebuildItems();

            var head = run.InShopRoom
                ? $"스테이지 {run.Stage} · {run.CurrentLayer + 1}층 상점   ·   산 아이템은 주운 아이템 칸에 들어갑니다"
                : run.Current != null
                ? $"스테이지 {run.Stage} · {run.CurrentLayer + 1}층 {Encounters.Label(run.Current.Type)} 클리어   ·   경험치 +{run.LastXp}   ·   골드 +{run.LastGold}   ·   전투 후 회복 +{Mathf.RoundToInt(run.LastHeal * 100f)}%"
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
            string next = run.StageCleared ? $"스테이지 {run.Stage + 1}로" : "지도로";
            _toMapLabel.text = run.Loot.Count > 0 ? $"{next} (방을 고르면 못 넣은 아이템 {run.Loot.Count}개는 버려짐)" : next;

            bool rewards = run.PendingRewards.Count > 0;   // 지금은 전투 보상 카드가 없어 늘 숨김
            if (_rewardPanel.gameObject.activeSelf != rewards) _rewardPanel.gameObject.SetActive(rewards);
            UpdateRewards(run);
            UpdateMembers(run);
            UpdateShop(run);
            _sellAll.interactable = run.Loot.Count > 0;
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
                if (card.Empty.activeSelf == exists) card.Empty.SetActive(!exists);
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
                var portrait = PortraitTable.Of(recipe);
                if (card.Portrait.sprite != portrait) card.Portrait.sprite = portrait;
                card.Portrait.enabled = portrait;
                card.Portrait.color = fallen ? new Color(0.45f, 0.45f, 0.45f, 1f) : Color.white;
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
                    $"넉백·CC저항 {d.KnockbackResist * 100f:0}%·{d.CcResist * 100f:0}%\n" +
                    $"평타: {(def.Ranged ? "원거리" : def.Cleave || mods.Cleave ? "범위" : "한 명")}\n" +
                    $"<color={StopPowerUi.Hex}><b>필살기 저지력 {IntoTheDungeon.Features.Attack.PatternBreak.DefaultStopPower + mods.StopPowerAdd}{(mods.StopPowerAdd > 0 ? $" (+{mods.StopPowerAdd})" : "")}</b></color>\n" +
                    $"기본 스킬: {IntoTheDungeon.Features.Attack.BasicSkills.Name(def.BasicSkill)}";
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
            _itemsVersion = run.InventoryVersion;

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

            // 저지력 장비: 오른쪽 위 구석에 청록 망치 표시(가방·보관함에서 한눈에)
            if (def.Description != null && def.Description.Contains("저지력"))
            {
                var mark = NewImage(back.transform, StopPowerUi.Color);
                mark.sprite = StopPowerUi.Icon;
                var mrt = mark.rectTransform;
                mrt.anchorMin = mrt.anchorMax = mrt.pivot = new Vector2(1f, 1f);
                mrt.anchoredPosition = new Vector2(-1f, -1f);
                mrt.sizeDelta = new Vector2(20f, 20f);
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
                if (v.Source == inv && inv.Move(v.Placed, cx, cy, v.Rotated)) { }   // 같은 격자 안에서 옮기기
                else if (v.Source != inv && inv.CanPlace(v.Def, cx, cy, v.Rotated))
                {
                    if (v.Source != null) v.Source.Remove(v.Placed);          // 다른 격자에서 옮겨 옴
                    else run.Loot.Remove(v.Def);                             // 주운 아이템에서
                    inv.Place(v.Def, cx, cy, v.Rotated);
                }
                else TrySwap(run, v, inv, cx, cy);                           // 아이템 하나와 겹치면 서로 자리 바꾸기
            }
            else if (RectTransformUtility.RectangleContainsScreenPoint(_discard, screen, cam))
            {
                // 팔기: 원래 값의 절반
                if (v.Source != null) v.Source.Remove(v.Placed);
                else run.Loot.Remove(v.Def);
                int gold = run.Sell(v.Def);
                if (_shopMessage) _shopMessage.text = $"{v.Def.Name} 판매 +{gold} 골드";
                RebuildItems();
                ShowInfo(null, $"{v.Def.Name}을(를) 팔았습니다. +{gold} 골드");
                return;
            }
            else if (v.Source != null && RectTransformUtility.RectangleContainsScreenPoint(_lootArea, screen, cam))
            {
                v.Source.Remove(v.Placed);   // 격자에서 꺼내 주운 아이템 칸으로
                run.Loot.Add(v.Def);
            }
            // 그 밖(놓을 수 없는 곳)은 원래 자리로: 데이터를 안 바꾸고 다시 그리면 된다
            RebuildItems();
        }

        /// <summary>
        /// 놓을 자리에 아이템이 딱 하나 있으면 서로 바꾼다: 끌던 아이템은 그 자리로, 원래 있던 아이템은 끌던 아이템이 있던 자리로
        /// (안 맞으면 반대 방향·그 격자의 빈자리 → 놓으려던 격자의 빈자리). 주운 아이템 칸에서 끌어 왔으면 원래 아이템은 주운 아이템 칸으로.
        /// 어디에도 못 놓으면 아무것도 바꾸지 않는다.
        /// </summary>
        bool TrySwap(RunData run, InventoryItemView v, Inventory inv, int cx, int cy)
        {
            var hits = inv.Overlapping(v.Def, cx, cy, v.Rotated, v.Placed);
            if (hits == null || hits.Count != 1) return false;
            var other = hits[0];

            inv.Remove(other);
            v.Source?.Remove(v.Placed);
            var placed = inv.Place(v.Def, cx, cy, v.Rotated);
            if (placed != null)
            {
                if (v.Source == null) { run.Loot.Remove(v.Def); run.Loot.Add(other.Def); return true; }
                var back = v.Source.PlaceNear(other.Def, v.Placed.X, v.Placed.Y, other.Rotated);
                if (back == null && v.Source != inv) back = inv.PlaceNear(other.Def, other.X, other.Y, other.Rotated);
                if (back != null) return true;
                inv.Remove(placed);
            }
            // 되돌리기
            inv.Items.Add(other);
            if (v.Source != null) v.Source.Items.Add(v.Placed);
            return false;
        }

        /// <summary>
        /// 우클릭 빠른 장착: 주운 아이템·보관함 아이템은 빈자리가 있는 파티원 장비 칸에(살아 있는 파티원 먼저, 1번부터),
        /// 장비 칸에 낀 아이템은 보관함으로(꽉 찼으면 주운 아이템 칸으로) 뺀다.
        /// </summary>
        public void QuickEquip(InventoryItemView v)
        {
            var run = RunState.Current;
            bool equipped = false;
            foreach (var g in _gear) if (g.Inv != null && g.Inv == v.Source) equipped = true;

            if (equipped)
            {
                v.Source.Remove(v.Placed);
                if (run.Stash.PlaceNear(v.Def, 0, 0, v.Rotated) == null) run.Loot.Add(v.Def);
            }
            else
            {
                PlacedItem placed = null;
                for (int pass = 0; pass < 2 && placed == null; pass++)   // 0: 살아 있는 파티원, 1: 쓰러진 파티원
                    for (int i = 0; i < run.PartySize && placed == null; i++)
                    {
                        bool alive = i >= run.PartyHp.Count || run.PartyHp[i] > 0f;
                        if (alive == (pass == 0)) placed = run.GearOf(i).PlaceNear(v.Def, 0, 0, v.Rotated);
                    }
                if (placed == null) { ShowInfo(v.Def, "장비 칸에 빈자리가 없습니다."); return; }
                if (v.Source != null) v.Source.Remove(v.Placed);
                else run.Loot.Remove(v.Def);
            }
            RebuildItems();
            ShowInfo(null);
        }

        void ShowInfo(ItemDef def, string note)
        {
            ShowInfo(def);
            if (_info) _info.text += $"\n\n{note}";
        }

        public void ShowInfo(ItemDef def)
        {
            if (!_info) return;
            _info.text = def == null
                ? "마우스 올리기: 효과 보기\n끌기: 옮기기(위에 놓으면 맞바꿈)\n끄는 중 우클릭: 회전\n우클릭: 빠른 장착 / 빼기\n장비 칸만 효과, 보관함은 보관만"
                : $"{def.Name}\n[{ItemDef.RarityLabel(def.Rarity)} · {def.W}×{def.H}칸 · 판매가 {ShopPool.SellPrice(def)} 골드]\n\n{StopPowerUi.Highlight(def.Description)}";
        }

        void Leave() => RunState.Current.LeaveCamp();   // 같은 씬에서 바로 지도 화면으로

        // ───────── 전부 판매(확인창) ─────────

        /// <summary>주운 아이템 칸의 아이템을 모두 판다. 바로 팔지 않고 개수·받을 골드를 보여 주는 확인창을 띄운다.</summary>
        void AskSellAll()
        {
            var run = RunState.Current;
            if (run.Loot.Count == 0) return;
            int gold = 0;
            foreach (var def in run.Loot) gold += ShopPool.SellPrice(def);
            _confirmText.text = $"주운 아이템 {run.Loot.Count}개를 모두 팝니다.\n+{gold} 골드\n\n(장비 칸·보관함에 넣은 아이템은 팔지 않습니다)";
            _confirmBox.SetActive(true);
        }

        void SellAll()
        {
            _confirmBox.SetActive(false);
            var run = RunState.Current;
            int count = run.Loot.Count, gold = 0;
            foreach (var def in run.Loot.ToArray())
            {
                run.Loot.Remove(def);
                gold += run.Sell(def);
            }
            RebuildItems();
            ShowInfo(null, $"주운 아이템 {count}개를 팔았습니다. +{gold} 골드");
            if (_shopMessage) _shopMessage.text = $"{count}개 판매 +{gold} 골드";
        }

        void BuildConfirm()
        {
            var dim = NewImage(_root.transform, new Color(0f, 0f, 0f, 0.6f));
            dim.raycastTarget = true;   // 뒤를 못 누르게
            Stretch(dim.rectTransform);
            _confirmBox = dim.gameObject;
            var box = NewImage(dim.transform, panelColor);
            Place(box.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620f, 300f));
            var frame = box.gameObject.AddComponent<Outline>();
            frame.effectColor = accent;
            frame.effectDistance = new Vector2(3f, -3f);
            var title = NewText(box.rectTransform, "전부 판매", 32);
            title.color = accent;
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(560f, 44f));
            _confirmText = NewText(box.rectTransform, "", 24);
            _confirmText.horizontalOverflow = HorizontalWrapMode.Wrap;
            Place(_confirmText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(560f, 140f));
            NewButton(box.rectTransform, "판매", new Vector2(-120f, -105f), new Vector2(200f, 50f), new Color(0.85f, 0.55f, 0.35f, 1f), SellAll);
            NewButton(box.rectTransform, "취소", new Vector2(120f, -105f), new Vector2(200f, 50f), new Color(0.6f, 0.62f, 0.7f, 1f), () => _confirmBox.SetActive(false));
            _confirmBox.SetActive(false);
        }

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
            var reward = _rewardPanel = Panel(new Vector2(-480f, 255f), new Vector2(900f, 290f), "보상 (카드를 고른 뒤 확정)");
            for (int i = 0; i < RewardSlots; i++)
                _rewards[i] = BuildRewardSlot(reward, i, new Vector2((i - 1) * 285f, -12f), new Vector2(265f, 170f));
            _confirm = NewButton(reward, "선택 확정", new Vector2(0f, -120f), new Vector2(260f, 40f), accent, Confirm);

            // 왼쪽: 파티
            var party = Panel(new Vector2(-480f, -240f), new Vector2(900f, 420f), "파티");
            _partyTitle = party.GetComponentInChildren<Text>();
            for (int i = 0; i < _members.Length; i++)
                _members[i] = BuildMemberCard(party, new Vector2(-330f + i * 220f, -2f), new Vector2(212f, 320f));
            _deck = NewText(party, "", 18);
            _deck.alignment = TextAnchor.MiddleLeft;
            _deck.horizontalOverflow = HorizontalWrapMode.Wrap;
            Place(_deck.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(860f, 30f));

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
            _stash = BuildGrid(stash, new Vector2(-130f, -20f), Inventory.StashCols, Inventory.StashRows);
            _stash.Block = _stash.Rect.gameObject;
            _stash.Slot = -1;
            _info = NewText(stash, "", 16);
            _info.alignment = TextAnchor.UpperLeft;
            _info.horizontalOverflow = HorizontalWrapMode.Wrap;
            Place(_info.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(315f, -20f), new Vector2(250f, 160f));

            // 오른쪽: 주운 아이템 + 버리기
            var loot = Panel(new Vector2(480f, -330f), new Vector2(900f, 220f), "주운 아이템 (끌어서 장비 칸이나 보관함에)");
            _lootArea = NewImage(loot, new Color(0f, 0f, 0f, 0.35f)).rectTransform;
            _lootArea.GetComponent<Image>().raycastTarget = true;
            Place(_lootArea, new Vector2(0.5f, 0.5f), new Vector2(-95f, -20f), new Vector2(660f, 155f));
            var discard = NewImage(loot, new Color(0.25f, 0.12f, 0.12f, 1f));
            discard.raycastTarget = true;
            _discard = discard.rectTransform;
            Place(_discard, new Vector2(0.5f, 0.5f), new Vector2(345f, -2f), new Vector2(150f, 112f));
            var dl = NewText(_discard, "팔기\n(여기에 놓으면\n절반 값 골드)", 20);
            Stretch(dl.rectTransform);
            _sellAll = NewButton(loot, "전부 판매", new Vector2(345f, -84f), new Vector2(150f, 36f), new Color(0.85f, 0.55f, 0.35f, 1f), AskSellAll);

            BuildShop();

            // 지도로
            var toMap = NewButton(_root.transform, "지도로", new Vector2(0f, -508f), new Vector2(520f, 52f), accent, Leave);
            _toMapLabel = toMap.GetComponentInChildren<Text>();

            // 끄는 아이템이 모든 패널 위에 보이게 하는 층(맨 마지막 자식)
            _dragLayer = new GameObject("DragLayer", typeof(RectTransform)).GetComponent<RectTransform>();
            _dragLayer.SetParent(_root.transform, false);
            Stretch(_dragLayer);

            BuildConfirm();
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
            card.Empty = EmptySlotUi.Build(parent, _font, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size, "정예·보스 보상이나\n상점에서 동료 영입", 72);
            card.Body = NewImage(parent, cardColor);
            card.Root = card.Body.gameObject;
            Place(card.Body.rectTransform, new Vector2(0.5f, 0.5f), pos, size);
            // 초상화(왼쪽 위 네모)
            var frame = NewImage(card.Body.rectTransform, new Color(0f, 0f, 0f, 0.55f));
            var prt = frame.rectTransform;
            prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0f, 1f);
            prt.anchoredPosition = new Vector2(8f, -8f);
            prt.sizeDelta = new Vector2(58f, 58f);
            card.Portrait = NewImage(prt, Color.white);
            card.Portrait.preserveAspect = true;
            var irt = card.Portrait.rectTransform;
            irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
            irt.offsetMin = new Vector2(2f, 2f); irt.offsetMax = new Vector2(-2f, -2f);

            card.Name = NewText(card.Body.rectTransform, "", 22);
            card.Name.color = accent;
            Band(card.Name.rectTransform, 1f, -8f, 30f);
            card.Name.rectTransform.offsetMin = new Vector2(72f, card.Name.rectTransform.offsetMin.y);   // 초상화 오른쪽
            // 경험치 바: 노란 채움 + "Lv.2  40 / 100"
            var xpBack = NewImage(card.Body.rectTransform, new Color(0f, 0f, 0f, 0.6f));
            Band(xpBack.rectTransform, 1f, -42f, 24f);
            xpBack.rectTransform.offsetMin = new Vector2(72f, xpBack.rectTransform.offsetMin.y);
            card.XpFill = NewImage(xpBack.rectTransform, new Color(0.85f, 0.68f, 0.18f, 1f)).rectTransform;
            card.XpFill.anchorMin = Vector2.zero; card.XpFill.anchorMax = new Vector2(0f, 1f);
            card.XpFill.offsetMin = card.XpFill.offsetMax = Vector2.zero;
            card.XpText = NewText(xpBack.rectTransform, "", 16);
            var xrt = card.XpText.rectTransform;
            xrt.anchorMin = Vector2.zero; xrt.anchorMax = Vector2.one;
            xrt.offsetMin = xrt.offsetMax = Vector2.zero;

            card.Stats = NewText(card.Body.rectTransform, "", 14);
            card.Stats.alignment = TextAnchor.UpperLeft;
            card.Stats.lineSpacing = 1f;
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
