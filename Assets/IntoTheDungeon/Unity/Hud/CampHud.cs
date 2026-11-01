using UnityEngine;
using UnityEngine.UI;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Features.Battle;
using IntoTheDungeon.Features.Run;

namespace IntoTheDungeon.Unity.Hud
{
    /// <summary>
    /// 전투에서 이긴 뒤의 정비 화면(임시 UI, 코드로 만든다): 보상 카드 3장 중 1장 고르기 · 파티 상태 · 덱 목록, 그리고 "지도로".
    /// 일반 전투는 패시브 3장, 정예는 동료 영입 1장 + 패시브 2장(CardPool.Offer). 고르지 않고 지도로 가도 된다.
    /// </summary>
    public sealed class CampHud : MonoBehaviour, IWorldInjectable
    {
        const int RewardSlots = 3;

        [SerializeField] Color panelColor = new(0.12f, 0.14f, 0.22f, 1f);
        [SerializeField] Color cardColor = new(0.2f, 0.24f, 0.36f, 1f);
        [SerializeField] Color accent = new(1f, 0.82f, 0.3f, 1f);
        [SerializeField] Color recruitColor = new(0.25f, 0.45f, 0.6f, 1f);

        sealed class RewardSlot
        {
            public Image Frame, Body;
            public Text Kind, Title, Desc;
            public Button Button;
            public CardDef Card;
        }
        readonly RewardSlot[] _rewards = new RewardSlot[RewardSlots];
        Text _deck;
        BattleProgress _progress;
        GameObject _root;
        Text _party, _header;
        Font _font;

        public void Init(IWorld world)
        {
            world.TryGet(out _progress);
            if (!_root) Build();
        }

        void Update()
        {
            if (_progress == null || !_root) return;
            var run = RunState.Current;
            bool show = _progress.Phase == BattlePhase.Map && run.Phase == RunPhase.Camp;
            if (_root.activeSelf != show) _root.SetActive(show);
            if (!show) return;

            _header.text = run.Current != null
                ? $"{run.CurrentLayer + 1}층 {Encounters.Label(run.Current.Type)} 클리어   ·   전투 후 회복 +{Mathf.RoundToInt(run.LastHeal * 100f)}%"
                : "";

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < run.PartyHp.Count; i++)
            {
                float hp = run.PartyHp[i];
                sb.Append($"파티원 {i + 1}   ");
                sb.Append(hp > 0f ? $"체력 {Mathf.RoundToInt(hp * 100f)}%" : "쓰러짐 (휴식 방에서 회복)");
                sb.Append('\n');
            }
            if (run.PartyHp.Count < run.PartySize)
                for (int i = run.PartyHp.Count; i < run.PartySize; i++) sb.Append($"파티원 {i + 1}   체력 100%\n");
            _party.text = $"인원 {run.PartySize} / {CardPool.MaxPartySize}\n" + sb;

            // 보상 카드
            for (int i = 0; i < RewardSlots; i++)
            {
                var slot = _rewards[i];
                var card = i < run.PendingRewards.Count ? run.PendingRewards[i] : null;
                slot.Card = card;
                slot.Frame.gameObject.SetActive(card != null);
                if (card == null) continue;

                bool taken = run.RewardTaken;
                bool mine = taken && run.Deck.Count > 0 && run.Deck[^1] == card;
                slot.Kind.text = card.Kind == CardKind.Recruit ? "동료 영입" : "패시브";
                slot.Title.text = card.Title;
                slot.Desc.text = mine ? $"{card.Description}\n\n(받음)" : card.Description;
                slot.Body.color = card.Kind == CardKind.Recruit ? recruitColor : cardColor;
                slot.Frame.color = mine ? accent : taken ? new Color(0f, 0f, 0f, 0.6f) : new Color(1f, 1f, 1f, 0.35f);
                var a = taken && !mine ? 0.4f : 1f;
                slot.Title.color = new Color(1f, 1f, 1f, a);
                slot.Desc.color = new Color(1f, 1f, 1f, 0.8f * a);
                slot.Button.interactable = !taken;
            }

            // 덱(같은 카드는 묶어서 ×N)
            var counts = new System.Collections.Generic.Dictionary<string, int>();
            var order = new System.Collections.Generic.List<CardDef>();
            foreach (var c in run.Deck)
            {
                if (!counts.ContainsKey(c.Id)) { counts[c.Id] = 0; order.Add(c); }
                counts[c.Id]++;
            }
            var deck = new System.Text.StringBuilder();
            foreach (var c in order)
                deck.Append(c.Title).Append(counts[c.Id] > 1 ? $" ×{counts[c.Id]}" : "").Append("   ").Append(c.Description).Append('\n');
            _deck.text = deck.Length > 0 ? deck.ToString() : "아직 카드가 없습니다";
        }

        void Pick(int index)
        {
            var card = _rewards[index].Card;
            if (card != null) RunState.Current.TakeReward(card);
        }

        void Leave()
        {
            RunState.Current.LeaveCamp();   // 같은 씬에서 바로 지도 화면으로(DungeonMapHud가 이어서 보여 줌)
        }

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

            Place(NewText(_root.transform, "정비", 56).rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(600f, 70f));
            _header = NewText(_root.transform, "", 28);
            _header.color = accent;
            Place(_header.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(900f, 40f));

            // 보상: 카드 3장 중 하나
            var reward = Panel(new Vector2(0f, 150f), new Vector2(1500f, 360f), "보상 (하나 고르기)");
            float cw = 320f, gap = 60f;
            for (int i = 0; i < RewardSlots; i++)
                _rewards[i] = BuildRewardSlot(reward, i, new Vector2((i - 1) * (cw + gap), -20f), new Vector2(cw, 260f));

            // 파티 상태
            var party = Panel(new Vector2(-380f, -260f), new Vector2(740f, 330f), "파티");
            _party = NewText(party, "", 28);
            _party.alignment = TextAnchor.UpperLeft;
            var prt = _party.rectTransform;
            prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one;
            prt.offsetMin = new Vector2(40f, 20f); prt.offsetMax = new Vector2(-40f, -70f);

            // 덱 목록
            var deck = Panel(new Vector2(380f, -260f), new Vector2(740f, 330f), "덱");
            _deck = NewText(deck, "", 24);
            _deck.alignment = TextAnchor.UpperLeft;
            var drt = _deck.rectTransform;
            drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one;
            drt.offsetMin = new Vector2(40f, 20f); drt.offsetMax = new Vector2(-40f, -70f);

            // 지도로
            var go = new GameObject("ToMap", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(_root.transform, false);
            var img = go.GetComponent<Image>();
            img.color = accent;
            Place((RectTransform)go.transform, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(340f, 84f));
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(Leave);
            var label = NewText(go.transform, "지도로", 38);
            label.color = new Color(0.1f, 0.1f, 0.15f, 1f);
            Stretch(label.rectTransform);

            _root.SetActive(false);
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

            slot.Kind = NewText(brt, "", 22);
            slot.Kind.color = accent;
            Band(slot.Kind.rectTransform, 1f, -14f, 34f);
            slot.Title = NewText(brt, "", 30);
            Band(slot.Title.rectTransform, 1f, -56f, 44f);
            slot.Desc = NewText(brt, "", 24);
            slot.Desc.alignment = TextAnchor.UpperCenter;
            var drt = slot.Desc.rectTransform;
            drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one;
            drt.offsetMin = new Vector2(16f, 16f); drt.offsetMax = new Vector2(-16f, -110f);

            slot.Button = slot.Frame.gameObject.AddComponent<Button>();
            slot.Button.targetGraphic = slot.Body;
            int i = index;
            slot.Button.onClick.AddListener(() => Pick(i));
            return slot;
        }

        // 부모 위쪽에 붙는 가로 띠(top = 위에서 내려온 거리, 음수)
        static void Band(RectTransform rt, float anchorY, float top, float height)
        {
            rt.anchorMin = new Vector2(0f, anchorY); rt.anchorMax = new Vector2(1f, anchorY); rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(10f, top - height); rt.offsetMax = new Vector2(-10f, top);
        }

        RectTransform Panel(Vector2 pos, Vector2 size, string title)
        {
            var panel = NewImage(_root.transform, panelColor);
            var rt = panel.rectTransform;
            Place(rt, new Vector2(0.5f, 0.5f), pos, size);
            var t = NewText(rt, title, 32);
            t.color = accent;
            t.alignment = TextAnchor.UpperLeft;
            var trt = t.rectTransform;
            trt.anchorMin = new Vector2(0f, 1f); trt.anchorMax = new Vector2(1f, 1f); trt.pivot = new Vector2(0.5f, 1f);
            trt.offsetMin = new Vector2(30f, -60f); trt.offsetMax = new Vector2(-30f, -16f);
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
