using UnityEngine;
using UnityEngine.UI;
using IntoTheDungeon.Features.Run;

namespace IntoTheDungeon.Unity.Hud
{
    /// <summary>장비 강화 창: 같은 아이템을 겹쳐 놓으면 뜬다. 성공률·금화를 보고 시도하고, 결과를 그 자리에서 보여 준다.</summary>
    public sealed partial class CampHud
    {
        static readonly Color EnhanceFill = new(0.85f, 0.68f, 0.3f, 1f);

        GameObject _enhanceBox;
        Image _enhanceCard, _enhanceIcon, _enhanceMatCard, _enhanceMatIcon;
        Text _enhanceName, _enhanceBonus, _enhanceChance, _enhanceCost, _enhanceResult;
        Button _enhanceGo, _enhanceCancel, _enhanceDone;
        ItemRef _enhanceTarget, _enhanceMaterial;

        void BuildEnhance()
        {
            var dim = UiKit.NewImage(_root.transform, new Color(0f, 0f, 0f, 0.7f), raycast: true);   // 뒤를 못 누르게
            UiKit.Stretch(dim.rectTransform);
            _enhanceBox = dim.gameObject;
            var panel = UiKit.Panel(UiKit.NewImage(dim.transform, panelColor), rim: UiKit.Gold);
            UiKit.Place(panel.rectTransform, Vector2.zero, new Vector2(640f, 430f));
            var box = panel.transform;

            var title = UiKit.NewText(box, "강화", 34, UiKit.Gold);
            UiKit.Place(title.rectTransform, new Vector2(0f, 175f), new Vector2(300f, 46f));

            (_enhanceCard, _enhanceIcon) = EnhanceSlot(box, new Vector2(-200f, 65f), 112f);
            var plus = UiKit.NewText(box, "+", 40, Color.white);
            UiKit.Place(plus.rectTransform, new Vector2(-105f, 65f), new Vector2(40f, 50f));
            (_enhanceMatCard, _enhanceMatIcon) = EnhanceSlot(box, new Vector2(-25f, 65f), 80f);

            _enhanceChance = UiKit.NewText(box, "", 58);
            UiKit.Place(_enhanceChance.rectTransform, new Vector2(160f, 90f), new Vector2(220f, 70f));
            var coin = UiKit.NewImage(box, Color.white);
            coin.sprite = RelicIcons.Of("coin");
            coin.enabled = coin.sprite;
            UiKit.Place(coin.rectTransform, new Vector2(115f, 28f), new Vector2(30f, 30f));
            _enhanceCost = UiKit.NewText(box, "", 28, null, TextAnchor.MiddleLeft);
            UiKit.Place(_enhanceCost.rectTransform, new Vector2(195f, 28f), new Vector2(130f, 36f));

            _enhanceName = UiKit.NewText(box, "", 28, UiKit.Gold);
            UiKit.Place(_enhanceName.rectTransform, new Vector2(0f, -45f), new Vector2(600f, 40f));
            _enhanceBonus = UiKit.NewText(box, "", 24, UiKit.Positive);
            UiKit.Place(_enhanceBonus.rectTransform, new Vector2(0f, -88f), new Vector2(600f, 34f));
            _enhanceResult = UiKit.NewText(box, "", 40);
            UiKit.Place(_enhanceResult.rectTransform, new Vector2(0f, -92f), new Vector2(400f, 50f));

            _enhanceGo = UiKit.NewButton(box, "강화", new Vector2(-120f, -165f), new Vector2(200f, 52f), EnhanceFill, TryEnhance);
            _enhanceCancel = UiKit.NewButton(box, "취소", new Vector2(120f, -165f), new Vector2(200f, 52f), new Color(0.6f, 0.62f, 0.7f, 1f), CloseEnhance);
            _enhanceDone = UiKit.NewButton(box, "확인", new Vector2(0f, -165f), new Vector2(200f, 52f), EnhanceFill, CloseEnhance);
            _enhanceBox.SetActive(false);
        }

        static (Image card, Image icon) EnhanceSlot(Transform parent, Vector2 pos, float size)
        {
            var card = UiKit.Card(UiKit.NewImage(parent, UiKit.CardFill));
            UiKit.Place(card.rectTransform, pos, new Vector2(size, size));
            var icon = UiKit.NewImage(card.transform, Color.white);
            icon.preserveAspect = true;
            UiKit.Stretch(icon.rectTransform, 10f);
            return (card, icon);
        }

        static void SetEnhanceSlot(Image card, Image icon, ItemDef def)
        {
            UiKit.SetRim(card, UiKit.Rarity(def.Rarity));
            icon.sprite = ItemIconTable.Icon(def.Id);
            icon.enabled = icon.sprite;
        }

        /// <summary>끌던 아이템이 같은 아이템 위에 놓였으면 강화 창을 연다.</summary>
        bool TryOpenEnhance(InventoryItemView v, Vector2 screen)
        {
            foreach (var other in _views)
            {
                if (!other || other == v || !ItemEnhance.CanCombine(other.Def, v.Def)) continue;
                if (!RectTransformUtility.RectangleContainsScreenPoint((RectTransform)other.transform, screen, null)) continue;
                _enhanceTarget = new ItemRef(other.Source, other.Placed, other.Def);
                _enhanceMaterial = new ItemRef(v.Source, v.Placed, v.Def);
                ShowEnhanceAsk();
                return true;
            }
            return false;
        }

        // 끄는 동안 합칠 수 있는 아이템을 금빛으로 보여 준다(다 놓으면 RebuildItems가 새로 그려 꺼진다)
        void GlowEnhanceTargets(InventoryItemView v)
        {
            foreach (var other in _views)
                if (other && other != v && ItemEnhance.CanCombine(other.Def, v.Def))
                    UiKit.Glow(other.GetComponent<Image>(), UiKit.Gold, true);
        }

        void ShowEnhanceAsk()
        {
            var target = _enhanceTarget.Item;
            int next = target.Level + 1, cost = ItemEnhance.Cost(target);
            float chance = ItemEnhance.Chance(target);
            bool affordable = RunState.Current.Gold >= cost;

            SetEnhanceSlot(_enhanceCard, _enhanceIcon, target);
            SetEnhanceSlot(_enhanceMatCard, _enhanceMatIcon, _enhanceMaterial.Item);
            _enhanceMatCard.gameObject.SetActive(true);
            _enhanceChance.text = $"{Mathf.RoundToInt(chance * 100f)}%";
            _enhanceChance.color = chance >= 1f ? UiKit.Positive : UiKit.Accent;
            _enhanceCost.text = cost.ToString();
            _enhanceCost.color = affordable ? UiKit.Gold : UiKit.Negative;
            _enhanceName.text = $"{target.Name}  →  +{next}";
            _enhanceBonus.text = ItemEnhance.BonusLabel(target, next);
            _enhanceResult.text = "";

            _enhanceGo.interactable = affordable;
            _enhanceGo.GetComponent<Image>().color = affordable ? EnhanceFill : UiKit.Disabled;
            _enhanceGo.gameObject.SetActive(true);
            _enhanceCancel.gameObject.SetActive(true);
            _enhanceDone.gameObject.SetActive(false);
            _enhanceBox.SetActive(true);
        }

        void TryEnhance()
        {
            var before = _enhanceTarget.Item;   // 주운 아이템 칸이면 ItemRef.Def는 시도 뒤에도 옛 단계라 여기서 잡아 둔다
            var result = RunState.Current.Enhance(_enhanceTarget, _enhanceMaterial);
            if (result is EnhanceResult.Invalid or EnhanceResult.NoGold) { CloseEnhance(); return; }

            bool success = result == EnhanceResult.Success;
            _enhanceMatCard.gameObject.SetActive(false);   // 재료는 어느 쪽이든 사라진다
            _enhanceName.text = (success ? ItemEnhance.Of(before, before.Level + 1) : before).Name;
            _enhanceBonus.text = "";
            _enhanceResult.text = success ? "성공" : "실패";
            _enhanceResult.color = success ? UiKit.Gold : UiKit.Negative;
            UiTween.Punch(_enhanceResult.rectTransform, 0.25f, 0.3f);
            if (success) UiTween.Punch(_enhanceCard.rectTransform, 0.15f, 0.3f);

            _enhanceGo.gameObject.SetActive(false);
            _enhanceCancel.gameObject.SetActive(false);
            _enhanceDone.gameObject.SetActive(true);
        }

        void CloseEnhance()
        {
            _enhanceBox.SetActive(false);
            ShowInfo(null);
        }
    }
}
