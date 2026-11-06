using UnityEngine;
using UnityEngine.UI;
using IntoTheDungeon.Features.Run;
using IntoTheDungeon.Unity.Hud;

namespace IntoTheDungeon.Unity.Title
{
    /// <summary>
    /// 새 게임 첫 단계: 시작 캐릭터 고르기(해금된 동료만). 고른 캐릭터 혼자 시작하고, 2층 물음표 방에서 무작위 동료를 만난다.
    /// 못 만난 동료는 검은 그림자·「???」로 잠겨 있다. 마지막으로 고른 캐릭터를 기억한다(PlayerPrefs).
    /// 「다음」(Enter) → 봉인석 고르기, 「뒤로」(Esc) → 메인 메뉴.
    /// </summary>
    public sealed class CharacterSelectScreen : MonoBehaviour
    {
        const string PrefKey = "StarterLast";
        static readonly Vector2 TileSize = new(260f, 320f);

        System.Action<string> _onNext;
        System.Action _onBack;
        Font _font;
        GameObject _root;
        Text _name;
        readonly System.Collections.Generic.List<(Image body, string recipe, bool open)> _tiles = new();
        string _selected;

        public bool Visible => _root && _root.activeSelf;

        public void Build(Transform canvasRoot, Font font, System.Action<string> onNext, System.Action onBack)
        {
            _font = font;
            _onNext = onNext;
            _onBack = onBack;
            _root = new GameObject("CharacterSelect", typeof(RectTransform));
            _root.transform.SetParent(canvasRoot, false);
            Stretch((RectTransform)_root.transform);
            var root = _root.transform;

            Stretch(NewImage(root, new Color(0.02f, 0.02f, 0.05f, 1f)).rectTransform);
            var title = NewText(root, "캐릭터", 66);
            title.color = new Color(1f, 0.82f, 0.3f, 1f);
            Place(title.rectTransform, new Vector2(0f, 430f), new Vector2(900f, 90f));
            _name = NewText(root, "", 44);
            Place(_name.rectTransform, new Vector2(0f, -230f), new Vector2(900f, 60f));

            MakeButton(root, "다음", new Vector2(0f, -340f), new Vector2(420f, 92f), new Color(0.32f, 0.2f, 0.5f, 1f), Next);
            MakeButton(root, "뒤로", new Vector2(-560f, -440f), new Vector2(260f, 70f), new Color(0.16f, 0.16f, 0.24f, 1f), () => _onBack?.Invoke());
            _root.SetActive(false);
        }

        public void Show(bool on)
        {
            _root.SetActive(on);
            if (!on) return;
            var meta = MetaProgress.Current;
            _selected = PlayerPrefs.GetString(PrefKey, CardPool.DefaultStarter);
            if (!meta.AllyUnlocked(_selected)) _selected = CardPool.DefaultStarter;
            Redraw();
        }

        void Redraw()
        {
            foreach (var t in _tiles) if (t.body) Destroy(t.body.gameObject);
            _tiles.Clear();
            var meta = MetaProgress.Current;
            var all = CardPool.Recruits;
            for (int i = 0; i < all.Length; i++)
            {
                string recipe = all[i].RecruitRecipe;
                bool open = meta.AllyUnlocked(recipe);
                var pos = new Vector2((i - (all.Length - 1) * 0.5f) * (TileSize.x + 30f), 60f);
                var body = NewImage(_root.transform, UiKit.CardFill);
                Place(body.rectTransform, pos, TileSize);
                UiKit.Card(body);
                if (open)
                {
                    body.raycastTarget = true;
                    var b = body.gameObject.AddComponent<Button>();
                    b.transition = Selectable.Transition.None;
                    b.onClick.AddListener(() => { _selected = recipe; Refresh(); });
                }
                var art = NewImage(body.transform, open ? Color.white : new Color(0f, 0f, 0f, 0.85f));
                art.sprite = PortraitTable.Of(recipe);
                art.preserveAspect = true;
                art.enabled = art.sprite;
                Place(art.rectTransform, new Vector2(0f, 20f), new Vector2(220f, 220f));
                var label = NewText(body.transform, open ? NameOf(all[i]) : "???", 30);
                label.color = open ? Color.white : new Color(1f, 1f, 1f, 0.4f);
                Place(label.rectTransform, new Vector2(0f, -128f), new Vector2(TileSize.x - 16f, 40f));
                _tiles.Add((body, recipe, open));
            }
            Refresh();
        }

        void Refresh()
        {
            foreach (var (body, recipe, open) in _tiles)
                UiKit.SetRim(body, recipe == _selected ? UiKit.Gold : open ? UiKit.RimDim : new Color(0.2f, 0.2f, 0.25f, 1f));
            var card = CardPool.RecruitOf(_selected);
            _name.text = card != null ? NameOf(card) : "";
        }

        void Next()
        {
            PlayerPrefs.SetString(PrefKey, _selected);
            PlayerPrefs.Save();
            _onNext?.Invoke(_selected);
        }

        void Update()
        {
            if (!Visible) return;
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Next();
            else if (Input.GetKeyDown(KeyCode.Escape)) _onBack?.Invoke();
            else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) Step(-1);
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) Step(1);
        }

        // 해금된 캐릭터 사이로 옮긴다(잠긴 칸은 건너뜀)
        void Step(int dir)
        {
            int cur = _tiles.FindIndex(t => t.recipe == _selected);
            for (int k = 1; k <= _tiles.Count; k++)
            {
                int i = ((cur + dir * k) % _tiles.Count + _tiles.Count) % _tiles.Count;
                if (_tiles[i].open) { _selected = _tiles[i].recipe; Refresh(); return; }
            }
        }

        static string NameOf(CardDef card) => card.Title.Replace("동료 영입: ", "");

        Button MakeButton(Transform parent, string text, Vector2 pos, Vector2 size, Color color, UnityEngine.Events.UnityAction onClick)
        {
            var img = NewImage(parent, color);
            img.raycastTarget = true;
            Place(img.rectTransform, pos, size);
            var btn = img.gameObject.AddComponent<Button>();
            UiKit.Button(btn, img, color, UiKit.Rim);
            btn.onClick.AddListener(onClick);
            Stretch(NewText(img.transform, text, 38).rectTransform);
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
