using UnityEngine;
using UnityEngine.UI;
using IntoTheDungeon.Features.Run;

namespace IntoTheDungeon.Unity.Hud
{
    /// <summary>
    /// 레벨업 특성 고르기(정비 화면 위 창): 오른 레벨마다 하나씩, 그 파티원 특성 3개 중 1개를 고른다(건너뛰기 없음).
    /// 필살기 교체는 금테, 희귀 특성(★)은 보라 테로 따로 보인다. 등장은 덮개 페이드 + 창 튀어 오름 + 카드 순차(unscaled).
    /// </summary>
    public sealed partial class CampHud
    {
        GameObject _traitBox;
        Image _traitDim, _traitPanel, _traitPortrait;
        Text _traitTitle;
        readonly (Image body, Image icon, Text title, Text desc, Button button, CanvasGroup group)[] _traitCards
            = new (Image, Image, Text, Text, Button, CanvasGroup)[TraitPool.Choices];
        TraitPick _shownPick;
        const float TraitCardY = -40f;

        void BuildTraits()
        {
            var dim = NewImage(_root.transform, new Color(0f, 0f, 0f, 0.7f));
            dim.raycastTarget = true;   // 고르기 전엔 뒤를 못 누르게
            Stretch(dim.rectTransform);
            _traitBox = dim.gameObject;
            _traitDim = dim;
            var box = UiKit.Panel(NewImage(dim.transform, panelColor), rim: accent);
            _traitPanel = box;
            Place(box.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1040f, 470f));

            var frame = NewImage(box.rectTransform, new Color(0f, 0f, 0f, 0.55f));
            Place(frame.rectTransform, new Vector2(0.5f, 1f), new Vector2(-300f, -24f), new Vector2(72f, 72f));
            _traitPortrait = NewImage(frame.rectTransform, Color.white);
            _traitPortrait.preserveAspect = true;
            Stretch(_traitPortrait.rectTransform);
            _traitTitle = NewText(box.rectTransform, "", 34);
            _traitTitle.color = accent;
            _traitTitle.horizontalOverflow = HorizontalWrapMode.Overflow;
            Place(_traitTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(40f, -42f), new Vector2(700f, 50f));

            for (int i = 0; i < _traitCards.Length; i++)
            {
                int idx = i;
                var body = UiKit.Card(NewImage(box.rectTransform, cardColor));
                body.raycastTarget = true;
                Place(body.rectTransform, new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 330f, TraitCardY), new Vector2(310f, 300f));
                var group = body.gameObject.AddComponent<CanvasGroup>();
                var btn = body.gameObject.AddComponent<Button>();
                UiKit.Button(btn, body, UiKit.CardFill);   // 마우스를 올리면 밝아지고 살짝 커진다(ButtonFx). 바탕은 다른 카드와 같은 CardFill
                btn.onClick.AddListener(() => PickTrait(idx));
                var icon = NewImage(body.rectTransform, Color.white);
                icon.preserveAspect = true;
                Place(icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(36f, 36f));
                icon.enabled = false;
                var title = NewText(body.rectTransform, "", 28);
                title.horizontalOverflow = HorizontalWrapMode.Wrap;
                UiKit.Soft(title);
                Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -64f), new Vector2(280f, 60f));
                var desc = NewText(body.rectTransform, "", 22);
                desc.horizontalOverflow = HorizontalWrapMode.Wrap;
                desc.color = new Color(1f, 1f, 1f, 0.85f);
                Place(desc.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -55f), new Vector2(270f, 170f));
                _traitCards[i] = (body, icon, title, desc, btn, group);
            }
            _traitBox.SetActive(false);
        }

        void UpdateTraits(RunData run)
        {
            var pick = run.PendingTraits.Count > 0 ? run.PendingTraits[0] : null;
            bool show = pick != null;
            bool fresh = false;
            if (_traitBox.activeSelf != show)
            {
                _traitBox.SetActive(show);
                fresh = show;
            }
            if (!show || pick == _shownPick) return;
            _shownPick = pick;
            string recipe = pick.Slot < run.Members.Count ? run.Members[pick.Slot] : null;
            _traitPortrait.sprite = PortraitTable.Of(recipe);
            _traitPortrait.enabled = _traitPortrait.sprite;
            _traitTitle.text = $"{NameOf(run, pick.Slot)}  Lv.{pick.Level}  특성";
            var epic = UiKit.Rarity(ItemRarity.Epic);
            for (int i = 0; i < _traitCards.Length; i++)
            {
                var c = _traitCards[i];
                var def = i < pick.Options.Count ? TraitPool.Find(pick.Options[i]) : null;
                c.body.gameObject.SetActive(def != null);
                if (def == null) continue;
                var sprite = TraitIcons.For(def);
                c.icon.sprite = sprite;
                c.icon.enabled = sprite;
                c.title.text = def.Rare ? $"★ {def.Title}" : def.Title;   // ★ 희귀(보라) — 교체 특성(금)과 구분
                c.title.color = def.Rare ? epic : def.SkillSwap ? UiKit.Gold : Color.white;
                c.desc.text = StopPowerUi.Highlight(def.Description);
                UiKit.SetRim(c.body, def.Rare ? epic : def.SkillSwap ? UiKit.Gold : new Color(1f, 1f, 1f, 0.3f));
            }
            PlayTraitEntrance(fresh);
        }

        /// <summary>덮개 0→0.7(0.15초) + 창 0.92→1(OutBack 0.22초) + 카드 순차(0.08초 간격, 아래서 떠오르며 페이드). 창이 이미 떠 있으면 카드만.</summary>
        void PlayTraitEntrance(bool fresh)
        {
            if (fresh)
            {
                UiTween.Fade(_traitDim, 0f, 0.7f, 0.15f);
                UiTween.Scale(_traitPanel.rectTransform, new Vector3(0.92f, 0.92f, 1f), Vector3.one, 0.22f, Ease.OutBack);
            }
            float lead = fresh ? 0.1f : 0f;
            for (int i = 0; i < _traitCards.Length; i++)
            {
                var c = _traitCards[i];
                if (!c.body.gameObject.activeSelf) continue;
                var rt = c.body.rectTransform;
                var to = new Vector2((i - 1) * 330f, TraitCardY);
                var from = to + new Vector2(0f, -24f);
                float delay = lead + i * 0.08f;
                c.group.alpha = 0f;
                rt.anchoredPosition = from;
                UiTween.Fade(c.group, 0f, 1f, 0.2f, delay);
                UiTween.Move(rt, from, to, 0.25f, Ease.OutCubic, delay);   // 스케일은 ButtonFx가 쓰므로 위치로
            }
        }

        void PickTrait(int index)
        {
            var run = RunState.Current;
            var pick = run.PendingTraits.Count > 0 ? run.PendingTraits[0] : null;
            if (pick == null || index >= pick.Options.Count) return;
            run.TakeTrait(pick, pick.Options[index]);
            _shownPick = null;
        }
    }
}
