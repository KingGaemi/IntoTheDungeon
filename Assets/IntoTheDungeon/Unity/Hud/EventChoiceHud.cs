using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Features.Battle;
using IntoTheDungeon.Features.Run;

namespace IntoTheDungeon.Unity.Hud
{
    /// <summary>
    /// 「?」방·휴식 방 선택지 모달(지도 200·정비 210 위, 캔버스 215). RunData.PendingEvent가 있으면 덮개로 뒤를 막고 카드(≤4장)를 그린다.
    /// 카드 = 큰 아이콘 → 비용 줄(빨강 숫자) → 보상 줄(연두·금 숫자) → 라벨 한두 단어. 글은 툴팁에만. 고르면 RunData.ChooseEvent.
    /// </summary>
    [AutoHud]
    public sealed class EventChoiceHud : MonoBehaviour, IWorldInjectable
    {
        const int SortingOrder = 215;
        const int MaxCards = 4;
        const float CardW = 220f, CardH = 280f, CardGap = 240f;
        const float RowH = 30f, ChipGap = 12f;

        BattleProgress _progress;
        GameObject _root;
        RectTransform _panel;
        Text _title;
        readonly List<GameObject> _cards = new();
        readonly List<(int option, Button button, CanvasGroup group)> _live = new();
        RunData _builtFor;
        EventDef _builtDef;
        Dictionary<string, Sprite> _gemIcons;   // GemIcons 시트(잼 Id → 스프라이트), 씬마다 다시 읽음
        static Sprite s_gem;                    // 빈 소켓(폴백, Resources 에셋이라 정적 보관해도 안전)

        public void Init(IWorld world)
        {
            world.TryGet(out _progress);
            if (!_root) Build();
        }

        void Build()
        {
            var canvas = UiKit.NewCanvas(transform, "EventChoiceCanvas", SortingOrder);
            _root = canvas.gameObject;

            var dim = UiKit.NewImage(_root.transform, UiKit.Dim, raycast: true, name: "Dim");   // 고르기 전엔 지도·정비를 못 누르게
            UiKit.Stretch(dim.rectTransform);

            var panel = UiKit.Panel(UiKit.NewImage(dim.transform, UiKit.PanelFill, name: "Panel"), rim: UiKit.Gold);
            UiKit.Place(panel.rectTransform, new Vector2(0f, 20f), new Vector2(1000f, 440f));
            _panel = panel.rectTransform;

            _title = UiKit.Soft(UiKit.NewText(_panel, "", UiKit.TitleSize, UiKit.Gold));
            UiKit.Place(_title.rectTransform, new Vector2(0f, 160f), new Vector2(900f, 60f));

            _root.SetActive(false);
        }

        void Update()
        {
            if (_progress == null || !_root) return;
            var run = RunState.Current;
            var def = run?.PendingEvent;
            bool show = _progress.Phase == BattlePhase.Map && def != null;
            if (_root.activeSelf != show) _root.SetActive(show);
            if (!show)
            {
                if (_builtDef != null) Clear();
                return;
            }
            if (_builtFor != run || _builtDef != def) Rebuild(run, def);

            // 고를 수 있는지는 매 프레임 다시 본다(소지금·체력 검사라 값싸다). 바뀔 때만 적용
            for (int i = 0; i < _live.Count; i++)
            {
                var (option, button, group) = _live[i];
                if (!button) continue;
                bool can = option < def.Options.Count && def.Options[option].Usable(run);
                if (button.interactable != can)
                {
                    button.interactable = can;
                    group.alpha = can ? 1f : 0.4f;
                }
            }
        }

        void Clear()
        {
            foreach (var go in _cards)
                if (go) { go.SetActive(false); Destroy(go); }
            _cards.Clear();
            _live.Clear();
            _builtFor = null;
            _builtDef = null;
        }

        void Rebuild(RunData run, EventDef def)
        {
            Clear();
            _builtFor = run;
            _builtDef = def;
            _title.text = def.Title;

            var shown = new List<int>();
            for (int i = 0; i < def.Options.Count && shown.Count < MaxCards; i++)
                if (def.Options[i].Visible(run)) shown.Add(i);

            int n = shown.Count;
            for (int k = 0; k < n; k++)
            {
                int option = shown[k];
                var opt = def.Options[option];
                bool can = opt.Usable(run);

                var body = UiKit.NewImage(_panel, UiKit.CardFill, raycast: true, name: "Card");
                UiKit.Place(body.rectTransform, new Vector2((k - (n - 1) * 0.5f) * CardGap, -20f), new Vector2(CardW, CardH));
                var btn = body.gameObject.AddComponent<Button>();
                UiKit.Button(btn, body, UiKit.CardFill, rim: UiKit.RimDim);
                btn.onClick.AddListener(() => Choose(option));
                var group = body.gameObject.AddComponent<CanvasGroup>();
                group.alpha = can ? 1f : 0.4f;
                btn.interactable = can;

                // 등장 트윈은 안쪽 컨테이너에(바탕 스케일은 ButtonFx가 쓴다)
                var content = new GameObject("Content", typeof(RectTransform));
                content.transform.SetParent(body.transform, false);
                var crt = (RectTransform)content.transform;
                UiKit.Stretch(crt);

                var big = UiKit.NewImage(crt, Color.white, name: "Icon");
                big.sprite = IconOf(opt.Icon, out var tint);
                big.color = tint;
                big.preserveAspect = true;
                UiKit.Place(big.rectTransform, new Vector2(0f, 80f), new Vector2(64f, 64f));
                if (!big.sprite) big.enabled = false;

                Row(crt, opt.CostOf(run), 0f, cost: true);
                Row(crt, opt.GainOf(run), -50f, cost: false);

                var label = UiKit.Soft(UiKit.NewText(crt, opt.Label, UiKit.BodySize));
                UiKit.Place(label.rectTransform, new Vector2(0f, -110f), new Vector2(CardW - 16f, 30f));

                string tipTitle = opt.Label, tip = opt.Tip ?? "";
                UiTooltip.Bind(body.gameObject, () => Tooltips.Simple(tipTitle, tip));

                crt.localScale = Vector3.one * 0.9f;   // 지연이 있으면 트윈이 시작 전 값을 안 잡아 주므로 미리
                UiTween.Scale(crt, Vector3.one * 0.9f, Vector3.one, 0.18f, Ease.OutBack, k * 0.06f);

                _cards.Add(body.gameObject);
                _live.Add((option, btn, group));
            }
        }

        /// <summary>토큰 줄 하나: "종류[.등급]:값;…" → [아이콘][값] 칩들을 가운데 정렬. 비용 줄은 빨강 숫자, 보상 줄은 연두(유물·잼은 금, 등급은 등급색).</summary>
        void Row(RectTransform parent, string tokens, float y, bool cost)
        {
            if (string.IsNullOrEmpty(tokens)) return;
            var chips = new List<RectTransform>();
            var widths = new List<float>();
            foreach (var raw in tokens.Split(';'))
            {
                var p = raw.Trim();
                if (p.Length == 0) continue;
                int c = p.IndexOf(':');
                string kind = c < 0 ? p : p.Substring(0, c);
                string value = c < 0 ? "" : p.Substring(c + 1);
                string mod = "";
                int dot = kind.IndexOf('.');
                if (dot >= 0) { mod = kind.Substring(dot + 1); kind = kind.Substring(0, dot); }

                var icon = IconOf(kind, out var tint);
                var color = cost ? UiKit.Negative : GainColor(kind, mod);
                var chip = UiKit.IconText(parent, icon, value, UiKit.NumberSize, color, out var label);
                UiKit.Soft(label);
                var iconTr = chip.Find("Icon");
                if (iconTr) iconTr.GetComponent<Image>().color = tint;

                float w = (icon ? UiKit.NumberSize + 8f : 0f) + TextWidth(value);
                chips.Add(chip);
                widths.Add(w);
            }
            if (chips.Count == 0) return;
            float total = (chips.Count - 1) * ChipGap;
            foreach (var w in widths) total += w;
            float x = (CardW - total) * 0.5f;   // 왼쪽 가장자리 기준
            for (int i = 0; i < chips.Count; i++)
            {
                UiKit.Place(chips[i], new Vector2(0f, 0.5f), new Vector2(x, y), new Vector2(widths[i], RowH));
                x += widths[i] + ChipGap;
            }
        }

        /// <summary>대략 글자 폭(굵은 24px: 숫자·기호 13, 한글 24). 넘침은 허용이라 어림으로 충분.</summary>
        static float TextWidth(string s)
        {
            float w = 0f;
            foreach (var ch in s) w += ch < 128 ? 13f : 24f;
            return w;
        }

        static Color GainColor(string kind, string mod)
        {
            var rarity = RarityOf(mod);
            switch (kind)
            {
                case "relic": return rarity.HasValue ? UiKit.Rarity(rarity.Value) : UiKit.Gold;
                case "item": return rarity.HasValue ? UiKit.Rarity(rarity.Value) : UiKit.Positive;
                case "gem": return UiKit.Gold;
                case "dice": return UiKit.TextMain;   // 확률은 좋지도 나쁘지도 않다
                default: return UiKit.Positive;
            }
        }

        static ItemRarity? RarityOf(string mod) => mod switch
        {
            "common" => ItemRarity.Common,
            "uncommon" => ItemRarity.Uncommon,
            "rare" => ItemRarity.Rare,
            "epic" => ItemRarity.Epic,
            _ => null,
        };

        /// <summary>토큰 → 그림. 저주는 유물 아이콘을 빨강(부정)으로.</summary>
        Sprite IconOf(string kind, out Color tint)
        {
            tint = Color.white;
            switch (kind)
            {
                case null: case "": return null;
                case "hp": return UiIcons.Of(UiIcons.Hp);
                case "heal": return UiIcons.Of(UiIcons.Heal);
                case "revive": return UiIcons.Of(UiIcons.Revive);
                case "gold": return RelicIcons.Of("coin");
                case "relic": return UiIcons.Of(UiIcons.Relic);
                case "curse": tint = UiKit.Negative; return UiIcons.Of(UiIcons.Relic);
                case "item": return UiIcons.Of(UiIcons.Item);
                case "xp": return UiIcons.Of(UiIcons.Xp);
                case "trait": return UiIcons.Of(UiIcons.LvUp);
                case "fight": return UiIcons.Of(UiIcons.Atk);
                case "atk": return UiIcons.Of(UiIcons.Atk);
                case "pass": return UiIcons.Of(UiIcons.Spd);
                case "dice": return UiIcons.Of(UiIcons.Dice);
                case "skull": return UiIcons.Of(UiIcons.Skull);
                case "shrine": return UiIcons.Of(UiIcons.Shrine);
                case "gem": return GemIcon();
                default: return UiIcons.Of(kind);
            }
        }

        /// <summary>잼 그림: 끼고 있는 첫 잼(휴식 연마 대상) → 없으면 첫 잼 종류 → GemIcons가 없으면 빈 소켓.</summary>
        Sprite GemIcon()
        {
            if (_gemIcons == null)
            {
                _gemIcons = new Dictionary<string, Sprite>();
                foreach (var s in Resources.LoadAll<Sprite>("GemIcons")) _gemIcons[s.name] = s;
            }
            var meta = MetaProgress.Current;
            string id = null;
            for (int i = 0; i < meta.SlotCount && id == null; i++) id = meta.InSlot(i);
            if (id == null && GemPool.All.Length > 0) id = GemPool.All[0].Id;
            if (id != null && _gemIcons.TryGetValue(id, out var sp) && sp) return sp;
            if (!s_gem) s_gem = Resources.Load<Sprite>("GemSocket");
            return s_gem;
        }

        void Choose(int option)
        {
            var run = RunState.Current;
            if (run == null || run.PendingEvent == null) return;
            if (run.ChooseEvent(option)) UiTooltip.Hide();   // 다음 프레임에 PendingEvent가 비어 모달이 닫힌다
        }
    }
}
