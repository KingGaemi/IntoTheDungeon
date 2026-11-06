using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Features.Battle;
using IntoTheDungeon.Features.Run;
using IntoTheDungeon.Features.Character;
using IntoTheDungeon.Features.Stats;
using IntoTheDungeon.Features.Status;
using Atk = IntoTheDungeon.Features.Attack;

namespace IntoTheDungeon.Unity.Hud
{
    /// <summary>
    /// 전투에서 이긴 뒤의 정비 화면(코드로 만든다).
    /// 왼쪽: 보상 카드 3장(유물·아이템·금화·저주, 고른 뒤 「확정」 또는 「건너뛰기」=금화), 파티원 카드(체력/마나/경험치 바·능력치 6칸·특성 아이콘 줄·필살기 행).
    /// 오른쪽: 파티원별 장비 칸(4×3, 끼운 파티원에게만 적용), 파티 보관함(6×3, 효과 없음), 이번 전투에서 주운 아이템.
    /// 아이템은 칸 사이를 끌어서 옮기고(끄는 중 우클릭 = 회전), 팔기 칸에 놓으면 판다. 방을 고르면 못 넣은 아이템은 사라진다.
    /// 숫자·설명은 마우스를 올리면 공용 툴팁(UiTooltip)으로.
    /// </summary>
    public sealed partial class CampHud : MonoBehaviour, IWorldInjectable
    {
        const int RewardSlots = 3;
        const float Cell = 50f;   // 격자 한 칸(픽셀)

        [SerializeField] Color panelColor = new(0.12f, 0.14f, 0.22f, 1f);
        [SerializeField] Color cardColor = new(0.2f, 0.24f, 0.36f, 1f);
        [SerializeField] Color accent = UiKit.Accent;
        [SerializeField] Color recruitColor = new(0.25f, 0.45f, 0.6f, 1f);
        [SerializeField] Color cellColor = new(0.08f, 0.09f, 0.14f, 1f);
        static readonly Color CursedFill = new(0.22f, 0.11f, 0.14f, 1f);   // 저주 카드 바탕(진홍 = 부정)

        sealed class RewardSlot
        {
            public Image Frame, Body, Glow;
            public Text Kind, Title, Desc;
            public Button Button;
            public CardDef Card;
            public Image Icon;             // 아이템 아이콘·금화 동전·동료 초상(유물은 Medal)
            public RelicMedallion Medal;   // 유물: 둥근 금테 메달
            public CanvasGroup Group;      // 등장 페이드·확정 뒤 흐리기
        }

        sealed class MemberCard
        {
            public GameObject Root;
            public Image Body, Portrait, HpFill, ManaFill, SkillIcon, LevelBadge, StopIcon;
            public Text Name, Stats, XpText, SkillName, StopBadge, LevelText;
            public RectTransform XpFill;
            public GameObject Empty;   // 아직 동료가 없는 칸(점선 + 「+」)
            public readonly StatCell[] Cells = new StatCell[StatCount];
            public GameObject Fallen;  // 쓰러짐 표시(카드 위)
            public readonly GameObject[] TraitBoxes = new GameObject[TraitSlots];
            public readonly Image[] TraitIcons = new Image[TraitSlots];
            public readonly Image[] TraitBacks = new Image[TraitSlots];
            public readonly TraitDef[] TraitDefs = new TraitDef[TraitSlots];   // 툴팁용(마우스를 올릴 때 읽음)
            public GameObject StopRoot;   // 필살기 행의 망치 배지(저지력 0이면 숨김)
            public int Slot;
        }

        // 능력치 칸: 아이콘 + 숫자(보너스가 있으면 초록). 이름·설명은 마우스를 올리면 툴팁
        sealed class StatCell
        {
            public Image Icon;
            public Text Value;
            public string Name, Tip;
        }
        const int StatCount = 6;      // 카드엔 힘·민첩·지능·정신력·방어·저지력, 나머지는 초상 툴팁
        const int TraitSlots = 12;    // 특성 아이콘 줄(6 × 2줄)
        const float ManaBarFull = 200f;   // (임시값) 마나 바가 가득 차 보이는 최대 마나

        // 정비 화면에서 다시 계산한 파티원 한 명의 수치(전투 때와 같은 공식)
        struct MemberView
        {
            public CharacterDefinition Def;
            public PartyModifiers Mods;
            public PrimaryStats Leveled;
            public StatusComponent St;
            public DerivedStatsComponent D;
            public int Level;
            public float Ratio;
            public bool Fallen;
            public SkillPreset Preset;
            public Atk.SpecialSkillComponent Skill;   // 유물·특성·장비 보정을 입힌 값
        }

        readonly RewardSlot[] _rewards = new RewardSlot[RewardSlots];
        Button _skip;
        Text _skipLabel, _confirmLabel;
        RectTransform _skipChip;
        int _lastSelected = -1;     // 선택 스케일 연출용
        int _takenIndex = -1;       // 이번 화면에서 확정한 카드(금화·아이템은 이걸로 「받음」 판정)
        CardDef _animatedFor;       // 등장 연출을 끝낸 보상 묶음(첫 카드로 식별)
        bool _rewardsTakenShown;
        float _fadeUntil;           // 이 시각까지는 트윈이 알파를 움직인다(그 뒤엔 Update가 직접 맞춤)

        Text _title;

        // 맨 위 결과 줄: 아이콘 + 숫자 칩(경험치·골드·회복·레벨 업·끊기·무피해)
        sealed class Chip
        {
            public GameObject Root;
            public Text Value;
        }
        Chip _xpChip, _goldChip, _healChip, _lvChip, _breakChip, _flawlessChip;
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
        Text _partyTitle, _header, _info, _toMapLabel;
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
            bool show = run != null && _progress.Phase == BattlePhase.Map && run.Phase == RunPhase.Camp;
            if (_root.activeSelf != show) _root.SetActive(show);
            if (!show) { _wasShown = false; if (_confirmBox.activeSelf) _confirmBox.SetActive(false); return; }
            if (!_wasShown) { _wasShown = true; _selected = -1; RebuildItems(); ShowInfo(null); if (_shopMessage) _shopMessage.text = ""; }
            // 상점에서 산 아이템·영입한 동료가 보이게(끄는 중이 아닐 때)
            if (_itemsVersion != run.InventoryVersion && _dragLayer.childCount == 0) RebuildItems();

            // 제목: 어디서 무엇을 깼는지만, 나머지는 아이콘 칩으로
            _title.text = run.Current != null
                ? $"스테이지 {run.Stage} · {run.CurrentLayer + 1}층 {(run.InRestRoom && run.Current.Type == RoomType.Rest ? "휴식" : run.InShopRoom ? "상점" : Encounters.Label(run.Current.Type))}"
                : "정비";
            bool eventRoom = run.Current != null && run.Current.Type == RoomType.Event;   // 물음표 방에서 동료를 처음 만났으면 「새 동료 해금」
            bool result = !run.InShopRoom && !run.InRestRoom && run.Current != null;
            _header.text = eventRoom ? run.LastMessage : "";
            SetChip(_xpChip, result, $"+{run.LastXp}");
            SetChip(_goldChip, result, $"+{run.LastGold}");
            SetChip(_healChip, result, $"+{Mathf.RoundToInt(run.LastHeal * 100f)}%");
            var ups = new System.Text.StringBuilder();
            for (int i = 0; i < run.LastLevelUps.Count; i++)
            {
                int slot = run.LastLevelUps[i];
                ups.Append(i == 0 ? "" : ", ").Append(NameOf(run, slot)).Append($" Lv{run.LevelOf(slot)}");
            }
            SetChip(_lvChip, result && run.LastLevelUps.Count > 0, ups.ToString());
            var stats = run.Stats;   // 끊기·무피해 칩(집계는 result-records 트랙 — 값이 없으면 안 보임)
            SetChip(_breakChip, result && stats != null && stats.LastBattleBreaks > 0, $"+{(stats != null ? stats.LastBattleBreaks : 0)}");
            SetChip(_flawlessChip, result && stats != null && stats.LastBattleFlawless, "무피해");
            _partyTitle.text = $"파티  <size=20>{run.PartySize}/{CardPool.MaxPartySize}</size>";
            string next = run.StageCleared ? $"스테이지 {run.Stage + 1}로" : "지도로";
            _toMapLabel.text = run.Loot.Count > 0 ? $"{next} (방을 고르면 못 넣은 아이템 {run.Loot.Count}개는 버려짐)" : next;

            bool rewards = run.PendingRewards.Count > 0;   // 전투를 깨면 카드 3장 중 1장
            if (_rewardPanel.gameObject.activeSelf != rewards) _rewardPanel.gameObject.SetActive(rewards);
            UpdateRewards(run);
            UpdateMembers(run);
            UpdateShop(run);
            // 보상을 고르는 중이거나 휴식 방이면 상점 패널(보상 패널과 같은 자리)을 숨긴다
            bool hideShop = (rewards && !run.RewardTaken) || run.InRestRoom;
            if (_shopPanel && _shopPanel.activeSelf == hideShop) _shopPanel.SetActive(!hideShop);
            _sellAll.interactable = run.Loot.Count > 0;
            UpdateTraits(run);
        }

        // ───────── 보상 ─────────

        void UpdateRewards(RunData run)
        {
            bool taken = run.RewardTaken;
            var first = run.PendingRewards.Count > 0 ? run.PendingRewards[0] : null;
            if (first != _animatedFor)
            {
                // 새 보상 묶음: 선택·확정 기억을 지우고 순차 등장
                _animatedFor = first;
                _takenIndex = -1;
                _lastSelected = -1;
                _selected = -1;
                _rewardsTakenShown = taken;
                if (first != null) PlayRewardEntrance(run);
            }
            if (taken != _rewardsTakenShown) { _rewardsTakenShown = taken; _fadeUntil = 0f; }   // 저장에서 불러온 「받음」 등: 알파를 바로 맞춘다
            bool tweening = Time.unscaledTime < _fadeUntil;

            for (int i = 0; i < RewardSlots; i++)
            {
                var slot = _rewards[i];
                var card = i < run.PendingRewards.Count ? run.PendingRewards[i] : null;
                slot.Card = card;
                if (slot.Frame.gameObject.activeSelf != (card != null)) slot.Frame.gameObject.SetActive(card != null);
                if (card == null) continue;

                bool mine = taken && IsMine(run, card, i);
                bool chosen = !taken && i == _selected;
                Color idleRim, fill = cardColor;
                switch (card.Kind)
                {
                    case CardKind.Item:
                    {
                        var def = ItemPool.Get(card.ItemIndex);
                        var rc = UiKit.Rarity(card.Rarity);
                        slot.Kind.text = $"아이템 · {ItemDef.RarityLabel(card.Rarity)}";
                        slot.Kind.color = rc;
                        slot.Title.text = card.Title;
                        slot.Title.fontSize = 24;
                        slot.Desc.text = def != null
                            ? $"{StopPowerUi.Highlight(def.Description)}\n<size=15><color=#D9D5CCAA>{def.W}×{def.H}</color></size>"
                            : StopPowerUi.Highlight(card.Description);
                        var sprite = def != null ? ItemIconTable.Icon(def.Id) : null;
                        SetIcon(slot, sprite, Color.white);
                        if (slot.Medal.Relic != null) slot.Medal.Set(null);
                        idleRim = rc;
                        break;
                    }
                    case CardKind.Gold:
                        slot.Kind.text = "금화";
                        slot.Kind.color = UiKit.Gold;
                        slot.Title.text = $"+{card.Amount}";
                        slot.Title.fontSize = 34;
                        slot.Desc.text = "";
                        SetIcon(slot, RelicIcons.Of("coin"), Color.white);
                        if (slot.Medal.Relic != null) slot.Medal.Set(null);
                        idleRim = new Color(UiKit.Gold.r, UiKit.Gold.g, UiKit.Gold.b, 0.6f);
                        break;
                    case CardKind.Relic:
                        slot.Kind.text = card.Cursed ? $"저주 · {ItemDef.RarityLabel(card.Rarity)}" : $"유물 · {ItemDef.RarityLabel(card.Rarity)}";
                        slot.Kind.color = card.Cursed ? UiKit.Negative : UiKit.Rarity(card.Rarity);
                        slot.Title.text = card.Title;
                        slot.Title.fontSize = 24;
                        slot.Desc.text = StopPowerUi.Highlight(card.Description);
                        SetIcon(slot, null, Color.white);
                        if (slot.Medal.Relic != card) slot.Medal.Set(card);
                        idleRim = card.Cursed ? UiKit.Cursed : new Color(UiKit.Rim.r, UiKit.Rim.g, UiKit.Rim.b, 0.7f);   // 유물은 금테(등급은 메달 보석 점), 저주는 진홍
                        if (card.Cursed) fill = CursedFill;
                        break;
                    default:   // 동료 영입(예전 저장 호환)
                        slot.Kind.text = "동료 영입";
                        slot.Kind.color = accent;
                        slot.Title.text = card.Title;
                        slot.Title.fontSize = 24;
                        slot.Desc.text = StopPowerUi.Highlight(card.Description);
                        SetIcon(slot, PortraitTable.Of(card.RecruitRecipe), Color.white);
                        if (slot.Medal.Relic != null) slot.Medal.Set(null);
                        idleRim = new Color(1f, 1f, 1f, 0.3f);
                        fill = recruitColor;
                        break;
                }
                slot.Body.color = fill;
                UiKit.SetRim(slot.Body, mine || chosen ? accent : taken ? new Color(0.2f, 0.2f, 0.25f, 1f) : idleRim);
                bool glow = chosen || mine;
                if (slot.Glow && slot.Glow.gameObject.activeSelf != glow) slot.Glow.gameObject.SetActive(glow);
                if (!tweening) slot.Group.alpha = taken && !mine ? 0.4f : 1f;
                slot.Button.interactable = !taken;
            }

            // 선택이 바뀌면 고른 카드만 살짝 키운다
            if (_selected != _lastSelected)
            {
                if (_lastSelected >= 0 && _lastSelected < RewardSlots) UiTween.Scale(_rewards[_lastSelected].Body.rectTransform, _rewards[_lastSelected].Body.rectTransform.localScale, Vector3.one, 0.12f);
                if (_selected >= 0 && _selected < RewardSlots && !taken) UiTween.Scale(_rewards[_selected].Body.rectTransform, _rewards[_selected].Body.rectTransform.localScale, new Vector3(1.04f, 1.04f, 1f), 0.15f, Ease.OutBack);
                _lastSelected = _selected;
            }

            _confirm.interactable = !taken && _selected >= 0 && _selected < run.PendingRewards.Count;
            _confirmLabel.text = taken ? "보상 받음" : "확정";
            _skip.interactable = !taken;
            _skipLabel.text = run.SkipGold().ToString();
        }

        bool IsMine(RunData run, CardDef card, int index) => card.Kind switch
        {
            CardKind.Relic => run.Relics.Contains(card),
            CardKind.Recruit => run.Deck.Count > 0 && run.Deck[^1] == card,
            _ => index == _takenIndex,   // 금화·아이템은 받은 흔적이 안 남으므로 이번 화면에서 고른 칸만
        };

        static void SetIcon(RewardSlot slot, Sprite sprite, Color tint)
        {
            if (slot.Icon.sprite != sprite) slot.Icon.sprite = sprite;
            slot.Icon.color = tint;
            bool on = sprite;
            if (slot.Icon.enabled != on) slot.Icon.enabled = on;
        }

        /// <summary>카드 3장이 0.08초 간격으로 0.9→1 튀어 오르며 나타난다(unscaled).</summary>
        void PlayRewardEntrance(RunData run)
        {
            for (int i = 0; i < RewardSlots; i++)
            {
                var slot = _rewards[i];
                if (i >= run.PendingRewards.Count) continue;
                float delay = i * 0.08f;
                var rt = slot.Frame.rectTransform;
                rt.localScale = new Vector3(0.9f, 0.9f, 1f);
                slot.Body.rectTransform.localScale = Vector3.one;
                slot.Group.alpha = 0f;
                UiTween.Scale(rt, new Vector3(0.9f, 0.9f, 1f), Vector3.one, 0.28f, Ease.OutBack, delay);
                UiTween.Fade(slot.Group, 0f, 1f, 0.2f, delay);
            }
            _fadeUntil = Time.unscaledTime + 0.08f * RewardSlots + 0.3f;
        }

        TooltipContent RewardTooltip(CardDef card)
        {
            if (card == null) return null;
            switch (card.Kind)
            {
                case CardKind.Relic: return Tooltips.Relic(card);
                case CardKind.Item: return Tooltips.Item(ItemPool.Get(card.ItemIndex));
                case CardKind.Gold: return Tooltips.Simple("금화", $"+{card.Amount}", UiKit.Gold, RelicIcons.Of("coin"));
                default: return Tooltips.Simple(card.Title, card.Description, accent, PortraitTable.Of(card.RecruitRecipe));
            }
        }

        void Select(int index)
        {
            var run = RunState.Current;
            if (run == null || run.RewardTaken) return;
            _selected = index;
        }

        void Confirm()
        {
            var run = RunState.Current;
            if (run == null || _selected < 0 || _selected >= run.PendingRewards.Count) return;
            int chosen = _selected;
            run.TakeReward(run.PendingRewards[chosen]);
            _takenIndex = chosen;
            _selected = -1;
            _lastSelected = -1;
            _rewardsTakenShown = run.RewardTaken;
            // 고른 카드는 펀치, 나머지는 흐려진다
            for (int i = 0; i < RewardSlots; i++)
            {
                var slot = _rewards[i];
                if (!slot.Frame.gameObject.activeSelf) continue;
                UiTween.Kill(slot.Body.rectTransform);   // 선택 스케일 트윈이 남아 있으면 끊고 1로
                slot.Body.rectTransform.localScale = Vector3.one;
                if (i == chosen) UiTween.Punch(slot.Frame.rectTransform, 0.1f, 0.3f);
                else UiTween.Fade(slot.Group, slot.Group.alpha, 0.4f, 0.25f);
            }
            _fadeUntil = Time.unscaledTime + 0.3f;
        }

        /// <summary>카드를 안 고르고 금화로 받는다(RunData.SkipReward). 카드 3장이 모두 흐려진다.</summary>
        void Skip()
        {
            var run = RunState.Current;
            if (run == null || run.RewardTaken) return;
            run.SkipReward();
            _takenIndex = -1;
            _selected = -1;
            _lastSelected = -1;
            _rewardsTakenShown = run.RewardTaken;
            UiTween.Punch(_skipChip, 0.15f, 0.3f);
            for (int i = 0; i < RewardSlots; i++)
            {
                var slot = _rewards[i];
                if (!slot.Frame.gameObject.activeSelf) continue;
                UiTween.Kill(slot.Body.rectTransform);
                slot.Body.rectTransform.localScale = Vector3.one;
                UiTween.Fade(slot.Group, slot.Group.alpha, 0.4f, 0.25f);
            }
            _fadeUntil = Time.unscaledTime + 0.3f;
        }

        // ───────── 파티 ─────────

        /// <summary>정비 화면엔 캐릭터가 없으므로 캐릭터 정의(레벨 성장 포함) + 유물·특성·장비 보너스로 전투 때와 같은 계산을 한다.</summary>
        bool Compute(RunData run, int slot, out MemberView v)
        {
            v = default;
            if (run == null || slot < 0 || slot >= run.PartySize) return false;
            string recipe = run.Members[slot];
            v.Def = _catalog != null ? _catalog.Get(recipe) : CharacterDefinition.Default;
            v.Mods = run.BuildModifiers(slot);   // 유물 + 잼 + 이 파티원 장비·특성
            v.Level = run.LevelOf(slot);
            v.Leveled = PrimaryStats.AtLevel(v.Def.Stats, v.Def.Growth, v.Level);
            v.St = new StatusComponent();
            v.D = new DerivedStatsComponent();
            StatFormula.Apply(v.Leveled + v.Mods.StatBonus, ref v.St, ref v.D);
            v.Ratio = slot < run.PartyHp.Count ? run.PartyHp[slot] : 1f;
            v.Fallen = v.Ratio <= 0f;
            // 표시 필살기: 교체 특성을 골랐고 교체 필살기가 따로 있으면 그것
            v.Preset = v.Mods.AltSpecial && v.Def.AltSkill != v.Def.Skill ? v.Def.AltSkill : v.Def.Skill;
            v.Skill = Atk.SpecialSkillComponent.Of(v.Preset);
            v.Skill.StopPower = v.Skill.StopPowerBase > 0 ? v.Skill.StopPowerBase + v.Mods.StopPowerAdd : 0;   // 전투(BattleFlowSystem)와 같은 식
            v.Skill.Cooldown *= v.Mods.SkillCooldownMul;
            float baseMul = v.Skill.DamageMulBase > 0f ? v.Skill.DamageMulBase : v.Skill.DamageMultiplier;
            v.Skill.DamageMultiplier = baseMul * v.Mods.SkillDamageMul;
            v.Skill.Magic = v.Skill.MagicBase || v.Mods.MagicSkills;
            return true;
        }

        void UpdateMembers(RunData run)
        {
            for (int i = 0; i < _members.Length; i++)
            {
                var card = _members[i];
                bool exists = Compute(run, i, out var v);
                if (card.Root.activeSelf != exists) card.Root.SetActive(exists);
                if (card.Empty.activeSelf == exists) card.Empty.SetActive(!exists);
                if (!exists) continue;

                card.Name.text = NameOf(run, i);
                var portrait = PortraitTable.Of(run.Members[i]);
                if (card.Portrait.sprite != portrait) card.Portrait.sprite = portrait;
                card.Portrait.enabled = portrait;
                card.Portrait.color = v.Fallen ? new Color(0.45f, 0.45f, 0.45f, 1f) : Color.white;
                card.Body.color = v.Fallen ? new Color(0.1f, 0.09f, 0.11f, 1f) : UiKit.CardFill;
                if (card.Fallen.activeSelf != v.Fallen) card.Fallen.SetActive(v.Fallen);
                card.LevelText.text = v.Level.ToString();

                // 바 셋: 체력(비율, 30% 아래면 빨강 = 위험), 마나(폭 = 최대 마나 / 200), 경험치
                float hp = Mathf.Clamp01(v.Ratio);
                card.HpFill.rectTransform.anchorMax = new Vector2(hp, 1f);
                card.HpFill.color = hp <= 0.3f ? UiKit.Negative : UiKit.Hp;   // (임시값) 30%
                card.ManaFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp(v.D.MaxMana / ManaBarFull, 0.05f, 1f), 1f);
                int xp = run.XpOf(i), need = RunData.XpToNext(v.Level);
                card.XpFill.anchorMax = new Vector2(Mathf.Clamp01((float)xp / need), 1f);
                card.XpText.text = $"{xp}/{need}";

                // 능력치 6칸(힘·민첩·지능·정신력·방어·저지력). 나머지는 초상 툴팁
                var sb = v.Mods.StatBonus;
                int stop = v.Skill.StopPower;
                SetStat(card, 0, "힘", $"{v.Leveled.Str + sb.Str}", sb.Str > 0, StatTip(v.Leveled.Str, sb.Str, "공격력·체력·체력 회복"));
                SetStat(card, 1, "민첩", $"{v.Leveled.Agi + sb.Agi}", sb.Agi > 0, StatTip(v.Leveled.Agi, sb.Agi, "공격 속도·이동 속도"));
                SetStat(card, 2, "지능", $"{v.Leveled.Int + sb.Int}", sb.Int > 0, StatTip(v.Leveled.Int, sb.Int, "마나·마나 회복·마법 공격력"));
                SetStat(card, 3, "정신력", $"{v.Leveled.Wil + sb.Wil}", sb.Wil > 0, StatTip(v.Leveled.Wil, sb.Wil, "방어력·마법 저항·넉백 저항·CC 저항"));
                SetStat(card, 4, "방어력", $"{v.D.Armor:0}", false, $"{v.D.Armor:0}\n물리 피해 감소");
                SetStat(card, 5, "저지력", $"{stop}", v.Mods.StopPowerAdd > 0 && stop > 0,
                    stop > 0 ? $"필살기 저지력 {stop}{(v.Mods.StopPowerAdd > 0 ? $" (+{v.Mods.StopPowerAdd})" : "")}\n적 패턴의 요구 저지력 이상이면 끊는다" : "필살기 저지력 없음");
                card.Cells[5].Value.color = stop > 0 ? StopPowerUi.Color : UiKit.TextDim;

                // 특성 아이콘 줄
                var owned = run.TraitsOf(i);
                for (int k = 0; k < TraitSlots; k++)
                {
                    var def = k < owned.Count ? TraitPool.Find(owned[k]) : null;
                    card.TraitDefs[k] = def;
                    bool on = def != null;
                    if (card.TraitBoxes[k].activeSelf != on) card.TraitBoxes[k].SetActive(on);
                    if (!on) continue;
                    var sprite = TraitIcons.For(def);
                    if (card.TraitIcons[k].sprite != sprite) card.TraitIcons[k].sprite = sprite;
                    card.TraitIcons[k].enabled = sprite;
                    card.TraitBacks[k].color = def.Rare ? WithAlpha(UiKit.Rarity(ItemRarity.Epic), 0.35f)
                                             : def.SkillSwap ? WithAlpha(UiKit.Gold, 0.3f)
                                             : new Color(0f, 0f, 0f, 0.3f);
                }

                // 필살기 행: 아이콘 + 이름 + 망치 배지(저지력, 0이면 없음)
                var skillSprite = SkillIcons.Of(v.Preset);
                if (card.SkillIcon.sprite != skillSprite) card.SkillIcon.sprite = skillSprite;
                card.SkillIcon.enabled = skillSprite;
                card.SkillName.text = Atk.SkillNames.Of(v.Preset);
                card.SkillName.color = v.Skill.Magic ? UiKit.Magic : UiKit.TextMain;
                if (card.StopRoot.activeSelf != stop > 0) card.StopRoot.SetActive(stop > 0);
                if (stop > 0) card.StopBadge.text = stop.ToString();
                card.Stats.text = Atk.BasicSkills.Name(v.Def.BasicSkill);   // 기본 스킬 이름만 짧게
                card.Stats.color = new Color(1f, 1f, 1f, v.Fallen ? 0.35f : 0.55f);
            }
        }

        static Color WithAlpha(Color c, float a) => new(c.r, c.g, c.b, a);

        string NameOf(RunData run, int slot)
        {
            string recipe = slot < run.Members.Count ? run.Members[slot] : null;
            return _catalog != null ? _catalog.DisplayName(recipe) : recipe;
        }

        static string StatTip(int baseValue, int bonus, string effect)
            => $"{baseValue + bonus}{(bonus != 0 ? $" (기본 {baseValue} +{bonus})" : "")}\n{effect}";

        static readonly Color BonusColor = new(0.55f, 1f, 0.55f, 1f);

        void SetStat(MemberCard card, int index, string name, string value, bool bonus, string tip)
        {
            var c = card.Cells[index];
            c.Name = name;
            c.Value.text = value;
            c.Value.color = bonus ? BonusColor : UiKit.TextMain;
            c.Tip = tip;
        }

        /// <summary>초상 툴팁: 카드에 안 실은 능력치 8개(체력·회복·공격·공속·이속·마나는 숫자 줄, 마법 저항·저항은 글).</summary>
        TooltipContent MemberTooltip(int slot)
        {
            var run = RunState.Current;
            if (!Compute(run, slot, out var v)) return null;
            int cur = Mathf.RoundToInt(v.St.MaxHp * Mathf.Clamp01(v.Ratio));
            var c = new TooltipContent
            {
                Title = NameOf(run, slot), TitleColor = accent,
                Icon = PortraitTable.Of(run.Members[slot]),
                Kind = $"Lv.{v.Level} · {Atk.BasicSkills.Name(v.Def.BasicSkill)}",
                Rim = UiKit.Rim
            };
            c.Stats.Add((UiIcons.Of(UiIcons.Hp), $"{cur}/{v.St.MaxHp}", v.Fallen ? UiKit.Negative : UiKit.Hp));
            c.Stats.Add((UiIcons.Of(UiIcons.Regen), $"{v.D.HpRegen:0.0}/초", UiKit.Positive));
            c.Stats.Add((UiIcons.Of(UiIcons.Atk), $"{v.St.Damage}", UiKit.TextMain));
            c.Stats.Add((UiIcons.Of(UiIcons.Aspd), $"{v.St.AttackSpeed:0.00}", UiKit.TextMain));
            c.Stats.Add((UiIcons.Of(UiIcons.Spd), $"{v.St.MovementSpeed:0.0}", UiKit.TextMain));
            c.Stats.Add((UiIcons.Of(UiIcons.Mana), $"{v.D.MaxMana:0} +{v.D.ManaRegen:0.0}", UiKit.Mana));
            string swing = v.Def.Ranged ? "원거리" : v.Def.Cleave || v.Mods.Cleave ? "범위" : "한 명";
            c.Body = $"마법 저항 {v.D.MagicResist:0} · 넉백 저항 {v.D.KnockbackResist * 100f:0}% · CC 저항 {v.D.CcResist * 100f:0}%\n평타 {swing}"
                   + (v.Fallen ? $"\n<color={StopPowerUi.ShortHex}>쓰러짐</color>" : "");
            return c;
        }

        TooltipContent BarTooltip(int slot, int bar)
        {
            var run = RunState.Current;
            if (!Compute(run, slot, out var v)) return null;
            switch (bar)
            {
                case 0:
                    int cur = Mathf.RoundToInt(v.St.MaxHp * Mathf.Clamp01(v.Ratio));
                    return Tooltips.Simple("체력", $"{cur} / {v.St.MaxHp}", v.Fallen ? UiKit.Negative : UiKit.Hp, UiIcons.Of(UiIcons.Hp));
                case 1:
                    return Tooltips.Simple("마나", $"{v.D.MaxMana:0} · +{v.D.ManaRegen:0.0}/초", UiKit.Mana, UiIcons.Of(UiIcons.Mana));
                default:
                    int xp = run.XpOf(slot), need = RunData.XpToNext(v.Level);
                    return Tooltips.Simple("경험치", $"Lv.{v.Level} · {xp} / {need}", UiKit.Gold, UiIcons.Of(UiIcons.Xp));
            }
        }

        TooltipContent SkillTooltip(int slot)
        {
            if (!Compute(RunState.Current, slot, out var v)) return null;
            return Tooltips.Skill(in v.Skill, v.Preset);
        }

        TooltipContent TraitTooltip(MemberCard card, int k)
        {
            var def = card.TraitDefs[k];
            var c = Tooltips.Trait(def);
            if (c != null && !c.Icon) c.Icon = TraitIcons.For(def);
            return c;
        }

        TooltipContent StatTooltip(StatCell cell)
        {
            var c = Tooltips.Stat(cell.Name, cell.Tip, cell.Icon.sprite);
            c.IconTint = cell.Icon.color;
            return c;
        }

        Chip BuildChip(RectTransform parent, Sprite icon, Color color)
        {
            var bg = UiKit.Card(NewImage(parent, UiKit.CardFill));
            var hl = bg.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.padding = new RectOffset(10, 14, 4, 4);
            hl.spacing = 6f;
            hl.childAlignment = TextAnchor.MiddleLeft;
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = hl.childForceExpandHeight = false;
            var ic = NewImage(bg.transform, Color.white);
            ic.sprite = icon;
            var le = ic.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = le.preferredHeight = 28f;
            var t = UiKit.Soft(NewText(bg.transform, "", 24));
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            bg.transform.Find("Rim").gameObject.AddComponent<LayoutElement>().ignoreLayout = true;   // 테두리는 칩 전체를 덮게
            return new Chip { Root = bg.gameObject, Value = t };
        }

        static void SetChip(Chip chip, bool on, string text)
        {
            if (chip.Root.activeSelf != on) chip.Root.SetActive(on);
            if (on) chip.Value.text = text;
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

            // 강화 단계: 왼쪽 아래 금색 숫자
            if (def.Level > 0)
            {
                var lv = NewText(back.transform, $"+{def.Level}", 20);
                lv.color = UiKit.Gold;
                lv.alignment = TextAnchor.LowerLeft;
                lv.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);
                Place(lv.rectTransform, new Vector2(0f, 0f), new Vector2(3f, 1f), new Vector2(40f, 24f));
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
            GlowEnhanceTargets(v);
        }

        public void Drop(InventoryItemView v, Vector2 screen)
        {
            var run = RunState.Current;
            var cam = (Camera)null;   // Screen Space Overlay
            if (TryOpenEnhance(v, screen)) { RebuildItems(); return; }   // 같은 아이템 위: 강화 창(끌던 아이템은 일단 제자리로)
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
                ? ""
                : $"{def.Name}\n[{ItemDef.RarityLabel(def.Rarity)} · {def.W}×{def.H}칸 · 판매가 {ShopPool.SellPrice(def)} 골드]\n\n{StopPowerUi.Highlight(def.Description)}";
        }

        // 같은 씬에서 바로 지도 화면으로. 스테이지를 깬 뒤면 다음 던전을 먼저 고른다
        void Leave()
        {
            var run = RunState.Current;
            if (run.StageCleared) OpenDungeonChoice();
            else run.LeaveCamp();
        }

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
            var box = UiKit.Panel(NewImage(dim.transform, panelColor), rim: accent);
            Place(box.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620f, 300f));
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

            _title = UiKit.Soft(NewText(_root.transform, "정비", 44));
            _title.color = UiKit.Gold;
            Place(_title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(900f, 56f));
            _header = NewText(_root.transform, "", 22);
            _header.color = UiKit.TextDim;
            Place(_header.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -86f), new Vector2(1200f, 36f));
            // 결과 칩 줄(가운데 정렬)
            var chips = new GameObject("Chips", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            chips.transform.SetParent(_root.transform, false);
            var crt = (RectTransform)chips.transform;
            Place(crt, new Vector2(0.5f, 1f), new Vector2(0f, -84f), new Vector2(0f, 40f));
            var hl = chips.GetComponent<HorizontalLayoutGroup>();
            hl.spacing = 12f;
            hl.childAlignment = TextAnchor.MiddleCenter;
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = hl.childForceExpandHeight = false;
            chips.GetComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            _xpChip = BuildChip(crt, UiIcons.Of(UiIcons.Xp), new Color(0.75f, 0.9f, 1f, 1f));
            _goldChip = BuildChip(crt, RelicIcons.Of("coin"), UiKit.Gold);
            _healChip = BuildChip(crt, UiIcons.Of(UiIcons.Heal), new Color(0.6f, 1f, 0.6f, 1f));
            _lvChip = BuildChip(crt, UiIcons.Of(UiIcons.LvUp), new Color(0.6f, 1f, 0.6f, 1f));
            _breakChip = BuildChip(crt, StopPowerUi.Icon, UiKit.Stop);
            _flawlessChip = BuildChip(crt, UiIcons.Of(UiIcons.Def), UiKit.Positive);   // (임시) 방패 아이콘

            // 왼쪽: 보상
            var reward = _rewardPanel = Panel(new Vector2(-480f, 255f), new Vector2(900f, 290f), "보상");
            for (int i = 0; i < RewardSlots; i++)
                _rewards[i] = BuildRewardSlot(reward, i, new Vector2((i - 1) * 285f, -12f), new Vector2(265f, 170f));
            _confirm = NewButton(reward, "확정", new Vector2(0f, -120f), new Vector2(260f, 40f), accent, Confirm);
            _confirmLabel = _confirm.GetComponentInChildren<Text>();
            // 건너뛰기: 카드 대신 금화(동전 + 금액)
            _skip = UiKit.NewButton(reward, "건너뛰기", new Vector2(300f, -120f), new Vector2(200f, 40f), new Color(0.3f, 0.3f, 0.36f, 1f), Skip, 18);
            var skipText = _skip.GetComponentInChildren<Text>();
            skipText.color = UiKit.TextMain;
            skipText.alignment = TextAnchor.MiddleLeft;
            skipText.rectTransform.offsetMin = new Vector2(14f, 4f);
            _skipChip = UiKit.IconText(_skip.transform, RelicIcons.Of("coin"), "", 22, UiKit.Gold, out _skipLabel);
            UiKit.Place(_skipChip, new Vector2(1f, 0.5f), new Vector2(-10f, 0f), new Vector2(82f, 28f));
            UiTooltip.Bind(_skip.gameObject, () =>
            {
                var run = RunState.Current;
                return run == null ? null : Tooltips.Simple("건너뛰기", $"+{run.SkipGold()}", UiKit.Gold, RelicIcons.Of("coin"));
            });

            // 왼쪽: 파티
            var party = Panel(new Vector2(-480f, -240f), new Vector2(900f, 420f), "파티");
            _partyTitle = party.GetComponentInChildren<Text>();
            for (int i = 0; i < _members.Length; i++)
                _members[i] = BuildMemberCard(party, i, new Vector2(-330f + i * 220f, -2f), new Vector2(212f, 320f));


            // 오른쪽: 파티원별 장비 칸
            var gear = Panel(new Vector2(480f, 205f), new Vector2(900f, 370f), "장비");
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
            var stash = Panel(new Vector2(480f, -95f), new Vector2(900f, 210f), "보관함");
            _stash = BuildGrid(stash, new Vector2(-130f, -20f), Inventory.StashCols, Inventory.StashRows);
            _stash.Block = _stash.Rect.gameObject;
            _stash.Slot = -1;
            _info = NewText(stash, "", 16);
            _info.alignment = TextAnchor.UpperLeft;
            _info.horizontalOverflow = HorizontalWrapMode.Wrap;
            Place(_info.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(315f, -20f), new Vector2(250f, 160f));

            // 오른쪽: 주운 아이템 + 버리기
            var loot = Panel(new Vector2(480f, -330f), new Vector2(900f, 220f), "주운 아이템");
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

            BuildTraits();
            BuildEnhance();
            BuildDungeon();
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
            UiKit.Button(btn, img, color);
            btn.onClick.AddListener(onClick);
            var t = NewText(img.transform, text, 24);
            t.color = new Color(0.1f, 0.08f, 0.05f, 1f);
            Stretch(t.rectTransform);
            return btn;
        }

        // 카드 안 가로 띠의 왼쪽 끝을 초상 오른쪽으로 민다
        static void SetLeft(RectTransform rt, float left) => rt.offsetMin = new Vector2(left, rt.offsetMin.y);

        MemberCard BuildMemberCard(RectTransform parent, int slot, Vector2 pos, Vector2 size)
        {
            var card = new MemberCard { Slot = slot };
            card.Empty = EmptySlotUi.Build(parent, _font, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size, null, 72);
            card.Body = UiKit.Card(NewImage(parent, cardColor));
            card.Root = card.Body.gameObject;
            Place(card.Body.rectTransform, new Vector2(0.5f, 0.5f), pos, size);
            var body = card.Body.rectTransform;

            // 초상화(왼쪽 위 네모 60) + 왼쪽 아래 원형 레벨 배지. 초상에 마우스를 올리면 나머지 능력치 툴팁
            var frame = NewImage(body, new Color(0f, 0f, 0f, 0.55f));
            var prt = frame.rectTransform;
            prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0f, 1f);
            prt.anchoredPosition = new Vector2(8f, -8f);
            prt.sizeDelta = new Vector2(60f, 60f);
            card.Portrait = NewImage(prt, Color.white);
            card.Portrait.preserveAspect = true;
            var irt = card.Portrait.rectTransform;
            irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
            irt.offsetMin = new Vector2(2f, 2f); irt.offsetMax = new Vector2(-2f, -2f);
            UiTooltip.Bind(frame.gameObject, () => MemberTooltip(slot));

            var badgeRim = NewImage(body, new Color(0.1f, 0.08f, 0.05f, 1f));
            badgeRim.sprite = UiKit.CircleSprite;
            Place(badgeRim.rectTransform, new Vector2(0f, 1f), new Vector2(0f, -50f), new Vector2(28f, 28f));
            card.LevelBadge = NewImage(badgeRim.rectTransform, UiKit.Gold);
            card.LevelBadge.sprite = UiKit.CircleSprite;
            var lrt = card.LevelBadge.rectTransform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(2f, 2f); lrt.offsetMax = new Vector2(-2f, -2f);
            card.LevelText = NewText(badgeRim.rectTransform, "", 14);
            card.LevelText.color = new Color(0.1f, 0.08f, 0.05f, 1f);
            Stretch(card.LevelText.rectTransform);

            card.Name = NewText(body, "", 22);
            card.Name.color = accent;
            card.Name.alignment = TextAnchor.MiddleLeft;
            Band(card.Name.rectTransform, 1f, -6f, 26f);
            SetLeft(card.Name.rectTransform, 76f);   // 초상화 오른쪽

            // 바 셋(초상 오른쪽): 체력(초록) / 마나(파랑, 폭 = 최대 마나) / 경험치(금, 숫자 작게)
            var hpBack = NewImage(body, new Color(0f, 0f, 0f, 0.6f));
            Band(hpBack.rectTransform, 1f, -34f, 12f);
            SetLeft(hpBack.rectTransform, 76f);
            card.HpFill = NewImage(hpBack.rectTransform, UiKit.Hp);
            FillBar(card.HpFill.rectTransform);
            UiTooltip.Bind(hpBack.gameObject, () => BarTooltip(slot, 0));
            var manaBack = NewImage(body, new Color(0f, 0f, 0f, 0.6f));
            Band(manaBack.rectTransform, 1f, -49f, 8f);
            SetLeft(manaBack.rectTransform, 76f);
            card.ManaFill = NewImage(manaBack.rectTransform, UiKit.Mana);
            FillBar(card.ManaFill.rectTransform);
            UiTooltip.Bind(manaBack.gameObject, () => BarTooltip(slot, 1));
            var xpBack = NewImage(body, new Color(0f, 0f, 0f, 0.6f));
            Band(xpBack.rectTransform, 1f, -60f, 12f);
            SetLeft(xpBack.rectTransform, 76f);
            card.XpFill = NewImage(xpBack.rectTransform, new Color(0.85f, 0.68f, 0.18f, 1f)).rectTransform;
            FillBar(card.XpFill);
            card.XpText = NewText(xpBack.rectTransform, "", 11);
            card.XpText.alignment = TextAnchor.MiddleRight;
            UiKit.Soft(card.XpText);
            var xrt = card.XpText.rectTransform;
            xrt.anchorMin = Vector2.zero; xrt.anchorMax = Vector2.one;
            xrt.offsetMin = Vector2.zero; xrt.offsetMax = new Vector2(-4f, 0f);
            UiTooltip.Bind(xpBack.gameObject, () => BarTooltip(slot, 2));

            // 능력치 칸(2열 × 3줄): 힘·민첩 / 지능·정신력 / 방어·저지력. 숫자만, 이름·설명은 툴팁
            string[] icons = { UiIcons.Str, UiIcons.Agi, UiIcons.Int, UiIcons.Wil, UiIcons.Def, null };
            for (int k = 0; k < StatCount; k++)
            {
                var cell = new StatCell();
                var hit = NewImage(body, new Color(0f, 0f, 0f, k / 2 % 2 == 0 ? 0.18f : 0.08f));   // 줄무늬
                var hrt = hit.rectTransform;
                hrt.anchorMin = hrt.anchorMax = hrt.pivot = new Vector2(0f, 1f);
                hrt.anchoredPosition = new Vector2(8f + (k % 2) * 99f, -78f - (k / 2) * 30f);
                hrt.sizeDelta = new Vector2(97f, 28f);
                cell.Icon = NewImage(hrt, Color.white);
                cell.Icon.sprite = icons[k] != null ? UiIcons.Of(icons[k]) : StopPowerUi.Icon;
                if (icons[k] == null) cell.Icon.color = StopPowerUi.Color;
                var irt2 = cell.Icon.rectTransform;
                irt2.anchorMin = irt2.anchorMax = irt2.pivot = new Vector2(0f, 0.5f);
                irt2.anchoredPosition = new Vector2(4f, 0f);
                irt2.sizeDelta = new Vector2(22f, 22f);
                cell.Value = NewText(hrt, "", 17);
                cell.Value.alignment = TextAnchor.MiddleLeft;
                var vrt = cell.Value.rectTransform;
                vrt.anchorMin = Vector2.zero; vrt.anchorMax = Vector2.one;
                vrt.offsetMin = new Vector2(30f, 0f); vrt.offsetMax = new Vector2(-2f, 0f);
                UiKit.Soft(cell.Value);
                var captured = cell;
                UiTooltip.Bind(hit.gameObject, () => StatTooltip(captured));
                card.Cells[k] = cell;
            }

            // 특성 아이콘 줄(6 × 2줄, 16px 도트를 20px로): 각 칸에 마우스를 올리면 특성 툴팁
            for (int k = 0; k < TraitSlots; k++)
            {
                var box = NewImage(body, new Color(0f, 0f, 0f, 0.3f));
                var brt = box.rectTransform;
                brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0f, 1f);
                brt.anchoredPosition = new Vector2(8f + (k % 6) * 31f, -172f - (k / 6) * 30f);
                brt.sizeDelta = new Vector2(26f, 26f);
                var icon = NewImage(brt, Color.white);
                icon.preserveAspect = true;
                Place(icon.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(20f, 20f));
                int captured = k;
                UiTooltip.Bind(box.gameObject, () => TraitTooltip(card, captured));
                card.TraitBoxes[k] = box.gameObject;
                card.TraitBacks[k] = box;
                card.TraitIcons[k] = icon;
                box.gameObject.SetActive(false);
            }

            // 필살기 행(카드 아래쪽 띠): 아이콘 + 이름 + 망치 배지(저지력). 마우스를 올리면 필살기 툴팁
            var band = NewImage(body, new Color(0f, 0f, 0f, 0.28f));
            var bandRt = band.rectTransform;
            bandRt.anchorMin = new Vector2(0f, 0f); bandRt.anchorMax = new Vector2(1f, 0f); bandRt.pivot = new Vector2(0.5f, 0f);
            bandRt.offsetMin = new Vector2(8f, 28f); bandRt.offsetMax = new Vector2(-8f, 68f);
            card.SkillIcon = NewImage(bandRt, Color.white);
            card.SkillIcon.preserveAspect = true;
            Place(card.SkillIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(6f, 0f), new Vector2(28f, 28f));
            card.SkillName = NewText(bandRt, "", 18);
            card.SkillName.alignment = TextAnchor.MiddleLeft;
            UiKit.Soft(card.SkillName);
            var snrt = card.SkillName.rectTransform;
            snrt.anchorMin = Vector2.zero; snrt.anchorMax = Vector2.one;
            snrt.offsetMin = new Vector2(40f, 0f); snrt.offsetMax = new Vector2(-56f, 0f);
            var pill = NewImage(bandRt, new Color(0f, 0f, 0f, 0.45f));
            UiKit.Style(pill, new Color(0f, 0f, 0f, 0.45f), UiKit.Stop, shadow: false);
            Place(pill.rectTransform, new Vector2(1f, 0.5f), new Vector2(-4f, 0f), new Vector2(50f, 24f));
            card.StopIcon = NewImage(pill.rectTransform, StopPowerUi.Color);
            card.StopIcon.sprite = StopPowerUi.Icon;
            Place(card.StopIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(4f, 0f), new Vector2(16f, 16f));
            card.StopBadge = NewText(pill.rectTransform, "", 15);
            card.StopBadge.color = StopPowerUi.Color;
            card.StopBadge.alignment = TextAnchor.MiddleRight;
            var sbrt = card.StopBadge.rectTransform;
            sbrt.anchorMin = Vector2.zero; sbrt.anchorMax = Vector2.one;
            sbrt.offsetMin = new Vector2(20f, 0f); sbrt.offsetMax = new Vector2(-6f, 0f);
            card.StopRoot = pill.gameObject;
            UiTooltip.Bind(band.gameObject, () => SkillTooltip(slot));

            // 맨 아래: 기본 스킬 이름(작게)
            card.Stats = NewText(body, "", 14);
            card.Stats.alignment = TextAnchor.MiddleCenter;
            var strt = card.Stats.rectTransform;
            strt.anchorMin = new Vector2(0f, 0f); strt.anchorMax = new Vector2(1f, 0f); strt.pivot = new Vector2(0.5f, 0f);
            strt.offsetMin = new Vector2(6f, 6f); strt.offsetMax = new Vector2(-6f, 26f);
            // 쓰러짐 덮개
            var fallenCover = NewImage(body, new Color(0.05f, 0f, 0f, 0.55f));
            Stretch(fallenCover.rectTransform);
            var ft = NewText(fallenCover.rectTransform, "쓰러짐", 30);
            ft.color = new Color(1f, 0.45f, 0.4f, 1f);
            Stretch(ft.rectTransform);
            UiKit.Soft(ft);
            card.Fallen = fallenCover.gameObject;
            card.Fallen.SetActive(false);
            card.Root.SetActive(false);
            return card;
        }

        // 바 채움: 왼쪽부터 anchorMax.x 비율만큼
        static void FillBar(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = new Vector2(0f, 1f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        RewardSlot BuildRewardSlot(RectTransform parent, int index, Vector2 pos, Vector2 size)
        {
            var slot = new RewardSlot();
            slot.Frame = NewImage(parent, new Color(0f, 0f, 0f, 0f));   // 자리·버튼만(테두리는 Body의 둥근 테)
            Place(slot.Frame.rectTransform, new Vector2(0.5f, 0.5f), pos, size);
            slot.Group = slot.Frame.gameObject.AddComponent<CanvasGroup>();
            slot.Body = UiKit.Card(NewImage(slot.Frame.rectTransform, cardColor));
            var brt = slot.Body.rectTransform;
            brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
            brt.offsetMin = new Vector2(5f, 5f); brt.offsetMax = new Vector2(-5f, -5f);
            slot.Body.raycastTarget = true;
            slot.Glow = UiKit.Glow(slot.Body, accent, true);   // 선택·받음 강조(평소엔 꺼 둠)
            slot.Glow.gameObject.SetActive(false);

            slot.Icon = NewImage(brt, Color.white);
            slot.Icon.preserveAspect = true;
            var icrt = slot.Icon.rectTransform;
            icrt.anchorMin = icrt.anchorMax = icrt.pivot = new Vector2(0f, 1f);
            icrt.anchoredPosition = new Vector2(8f, -7f);
            icrt.sizeDelta = new Vector2(64f, 64f);
            slot.Icon.enabled = false;
            slot.Medal = RelicMedallion.Build(brt, 58f);
            var mrt = (RectTransform)slot.Medal.transform;
            mrt.anchorMin = mrt.anchorMax = new Vector2(0f, 1f);
            mrt.anchoredPosition = new Vector2(38f, -36f);
            slot.Kind = NewText(brt, "", 18);
            slot.Kind.color = accent;
            Band(slot.Kind.rectTransform, 1f, -8f, 26f);
            slot.Title = NewText(brt, "", 24);
            UiKit.Soft(slot.Title);
            Band(slot.Title.rectTransform, 1f, -38f, 34f);
            slot.Desc = NewText(brt, "", 19);
            slot.Desc.alignment = TextAnchor.UpperCenter;
            slot.Desc.horizontalOverflow = HorizontalWrapMode.Wrap;
            slot.Desc.color = new Color(1f, 1f, 1f, 0.85f);
            var drt = slot.Desc.rectTransform;
            drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one;
            drt.offsetMin = new Vector2(10f, 8f); drt.offsetMax = new Vector2(-10f, -78f);

            slot.Button = slot.Frame.gameObject.AddComponent<Button>();
            UiKit.Button(slot.Button, slot.Body, cardColor, fx: false);   // 호버 밝아짐(스케일은 선택 트윈이 맡음)
            int i = index;
            slot.Button.onClick.AddListener(() => Select(i));
            UiTooltip.Bind(slot.Body.gameObject, () => RewardTooltip(slot.Card));
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
            var panel = UiKit.Panel(NewImage(_root.transform, panelColor));
            var rt = panel.rectTransform;
            Place(rt, new Vector2(0.5f, 0.5f), pos, size);
            var t = UiKit.Soft(NewText(rt, title, 26));
            t.color = UiKit.Gold;
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
