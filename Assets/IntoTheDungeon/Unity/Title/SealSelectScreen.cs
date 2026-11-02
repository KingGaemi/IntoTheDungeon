using UnityEngine;
using UnityEngine.UI;
using IntoTheDungeon.Features.Run;
using IntoTheDungeon.Unity.Hud;

namespace IntoTheDungeon.Unity.Title
{
    /// <summary>
    /// 새 게임 직전 봉인석 고르기(판 도중엔 못 바꾼다): 가운데 봉인석 문양, 오른쪽에 단계·효과.
    /// 좌우 화살표(키보드 ←→)나 룬을 눌러 단계를 정하고, 「출발」(Enter)을 누르면 봉인석이 타오른 뒤 하얗게 번지며 던전으로 들어간다.
    /// 마지막으로 고른 단계를 기억한다(PlayerPrefs).
    /// </summary>
    public sealed class SealSelectScreen : MonoBehaviour
    {
        const string PrefKey = "SealTierLast";
        const float StartTime = 1.5f;

        System.Action<int> _onStart;
        System.Action _onBack;
        Font _font;
        GameObject _root;
        SealEmblem _emblem;
        Text _tierText, _nameText, _stats, _hint;
        Image _white;
        Button _left, _right, _start, _back;
        int _tier;
        bool _starting;
        float _startT, _textPop;

        public bool Visible => _root && _root.activeSelf;

        public void Build(Transform canvasRoot, Font font, System.Action<int> onStart, System.Action onBack)
        {
            _font = font;
            _onStart = onStart;
            _onBack = onBack;

            _root = new GameObject("SealSelect", typeof(RectTransform));
            _root.transform.SetParent(canvasRoot, false);
            Stretch((RectTransform)_root.transform);
            var root = _root.transform;

            Stretch(NewImage(root, new Color(0.02f, 0.02f, 0.05f, 1f)).rectTransform);

            var title = NewText(root, "봉인석", 66);
            title.color = new Color(0.92f, 0.88f, 1f, 1f);
            Place(title.rectTransform, new Vector2(0f, 430f), new Vector2(900f, 90f));
            var sub = NewText(root, "봉인을 풀수록 던전의 몹이 강해지고, 그만큼 보상이 커집니다", 26);
            sub.color = new Color(1f, 1f, 1f, 0.55f);
            Place(sub.rectTransform, new Vector2(0f, 370f), new Vector2(1400f, 40f));

            _emblem = new SealEmblem(root, 6f, big: true);
            _emblem.Rect.anchoredPosition = new Vector2(-300f, -10f);
            for (int i = 0; i < 10; i++)
            {
                int idx = i;
                Clickable(_emblem.RuneHit(i), () => Set(_tier == idx + 1 ? idx : idx + 1));   // 룬을 누르면 그 단계(이미 그 단계면 하나 내림)
            }
            Clickable(_emblem.CoreHit, () => Set(0));

            // 오른쪽: 단계·이름·효과
            _tierText = NewText(root, "", 92);
            Place(_tierText.rectTransform, new Vector2(470f, 150f), new Vector2(420f, 120f));
            _left = Arrow(root, "<", new Vector2(210f, 150f), () => Set(_tier - 1));
            _right = Arrow(root, ">", new Vector2(730f, 150f), () => Set(_tier + 1));
            _nameText = NewText(root, "", 40);
            Place(_nameText.rectTransform, new Vector2(470f, 60f), new Vector2(600f, 60f));
            _stats = NewText(root, "", 27);
            _stats.alignment = TextAnchor.MiddleCenter;
            _stats.lineSpacing = 1.25f;
            Place(_stats.rectTransform, new Vector2(470f, -90f), new Vector2(760f, 170f));

            _start = MakeButton(root, "출발", new Vector2(470f, -300f), new Vector2(420f, 92f), new Color(0.32f, 0.2f, 0.5f, 1f), StartRun);
            _back = MakeButton(root, "뒤로", new Vector2(-300f, -440f), new Vector2(260f, 70f), new Color(0.16f, 0.16f, 0.24f, 1f), () => _onBack?.Invoke());
            _hint = NewText(root, "←→ 단계   ·   룬을 눌러도 됩니다   ·   Enter 출발", 22);
            _hint.color = new Color(1f, 1f, 1f, 0.4f);
            Place(_hint.rectTransform, new Vector2(470f, -380f), new Vector2(800f, 36f));

            _white = NewImage(root, new Color(1f, 1f, 1f, 0f));
            Stretch(_white.rectTransform);
            _root.SetActive(false);
        }

        public void Show(bool on)
        {
            _root.SetActive(on);
            if (!on) return;
            _starting = false;
            _startT = 0f;
            _white.color = new Color(1f, 1f, 1f, 0f);
            _tier = Mathf.Clamp(PlayerPrefs.GetInt(PrefKey, 0), 0, SealStone.MaxTier);
            _emblem.SetTier(_tier, false);
            Refresh();
        }

        void Set(int tier)
        {
            if (_starting) return;
            tier = Mathf.Clamp(tier, 0, SealStone.MaxTier);
            if (tier == _tier) return;
            _tier = tier;
            _emblem.SetTier(tier, true);
            _textPop = 0f;
            Refresh();
        }

        void StartRun()
        {
            if (_starting) return;
            _starting = true;
            _startT = 0f;
            PlayerPrefs.SetInt(PrefKey, _tier);
            PlayerPrefs.Save();
            _emblem.Ignite();
            _left.interactable = _right.interactable = _start.interactable = _back.interactable = false;
        }

        void Refresh()
        {
            var col = SealArt.TierColor(_tier);
            _tierText.text = _tier == 0 ? "0단계" : $"{_tier}단계";
            _tierText.color = _tier == 0 ? new Color(0.75f, 0.78f, 0.88f) : Color.Lerp(col, Color.white, 0.25f);
            _nameText.text = _tier == 0 ? "봉인됨 — 기본 난이도" : $"「{SealArt.TierTitle(_tier)}」";
            _nameText.color = _tier == 0 ? new Color(1f, 1f, 1f, 0.6f) : col;
            _stats.text = SealArt.Describe(_tier);
            _left.interactable = _tier > 0;
            _right.interactable = _tier < SealStone.MaxTier;
            _start.GetComponent<Image>().color = Color.Lerp(new Color(0.22f, 0.24f, 0.36f), col * 0.6f, 0.4f + 0.06f * _tier);
        }

        void Update()
        {
            if (!Visible) return;
            float dt = Time.unscaledDeltaTime;
            _emblem.Tick(dt);

            _textPop = Mathf.Min(1f, _textPop + dt / 0.3f);
            float pop = 1f + 0.25f * Mathf.Sin(_textPop * Mathf.PI) * (1f - _textPop);
            _tierText.rectTransform.localScale = Vector3.one * pop;

            if (_starting)
            {
                _startT += dt;
                float w = Mathf.Clamp01((_startT - 0.75f) / (StartTime - 0.75f));
                _white.color = new Color(1f, 1f, 1f, w * w);
                if (_startT >= StartTime) { _starting = false; _onStart?.Invoke(_tier); }
                return;
            }

            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) Set(_tier - 1);
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) Set(_tier + 1);
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) StartRun();
            if (Input.GetKeyDown(KeyCode.Escape)) _onBack?.Invoke();
        }

        static void Clickable(Image img, UnityEngine.Events.UnityAction onClick)
        {
            img.raycastTarget = true;
            var b = img.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(onClick);
        }

        Button Arrow(Transform parent, string text, Vector2 pos, UnityEngine.Events.UnityAction onClick)
            => MakeButton(parent, text, pos, new Vector2(76f, 96f), new Color(0.14f, 0.13f, 0.22f, 1f), onClick, 54);

        Button MakeButton(Transform parent, string text, Vector2 pos, Vector2 size, Color color, UnityEngine.Events.UnityAction onClick, int fontSize = 38)
        {
            var img = NewImage(parent, color);
            img.raycastTarget = true;
            Place(img.rectTransform, pos, size);
            var outline = img.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0.15f);
            outline.effectDistance = new Vector2(2f, -2f);
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);
            var t = NewText(img.transform, text, fontSize);
            Stretch(t.rectTransform);
            return btn;
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

        static void Place(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }
    }
}
