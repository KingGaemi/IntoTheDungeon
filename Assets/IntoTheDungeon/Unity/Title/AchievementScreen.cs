using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using IntoTheDungeon.Features.Run;
using IntoTheDungeon.Unity.Hud;

namespace IntoTheDungeon.Unity.Title
{
    /// <summary>
    /// 타이틀 화면 「업적」: 업적 칸을 격자로 늘어놓는다. 이룬 건 그림·이름이 보이고 금테,
    /// 못 이룬 건 그림이 검은 그림자, 동료는 이름까지 「???」로 가린다. 열 때마다 MetaProgress로 다시 그린다.
    /// </summary>
    public sealed class AchievementScreen : MonoBehaviour
    {
        const int Columns = 6;
        static readonly Vector2 TileSize = new(220f, 250f);

        System.Action _onBack;
        Font _font;
        GameObject _root;
        Text _count;
        RectTransform _grid;
        Dictionary<string, Sprite> _gemSprites;

        public bool Visible => _root && _root.activeSelf;

        public void Build(Transform canvasRoot, Font font, System.Action onBack)
        {
            _font = font;
            _onBack = onBack;
            _root = new GameObject("Achievements", typeof(RectTransform));
            _root.transform.SetParent(canvasRoot, false);
            Stretch((RectTransform)_root.transform);
            var root = _root.transform;

            Stretch(NewImage(root, new Color(0.02f, 0.02f, 0.05f, 1f)).rectTransform);
            var title = NewText(root, "업적", 66);
            title.color = new Color(1f, 0.82f, 0.3f, 1f);
            Place(title.rectTransform, new Vector2(0f, 430f), new Vector2(900f, 90f));
            _count = NewText(root, "", 30);
            _count.color = new Color(1f, 1f, 1f, 0.6f);
            Place(_count.rectTransform, new Vector2(0f, 370f), new Vector2(600f, 40f));

            var grid = new GameObject("Grid", typeof(RectTransform));
            grid.transform.SetParent(root, false);
            _grid = (RectTransform)grid.transform;
            Place(_grid, new Vector2(0f, 0f), new Vector2(Columns * (TileSize.x + 20f), 640f));

            var back = NewImage(root, new Color(0.16f, 0.16f, 0.24f, 1f));
            back.raycastTarget = true;
            Place(back.rectTransform, new Vector2(0f, -440f), new Vector2(260f, 70f));
            var btn = back.gameObject.AddComponent<Button>();
            UiKit.Button(btn, back, new Color(0.16f, 0.16f, 0.24f, 1f), UiKit.Rim);
            btn.onClick.AddListener(() => _onBack?.Invoke());
            Stretch(NewText(back.transform, "뒤로", 38).rectTransform);
            _root.SetActive(false);
        }

        public void Show(bool on)
        {
            _root.SetActive(on);
            if (on) Redraw();
        }

        void Update()
        {
            if (Visible && Input.GetKeyDown(KeyCode.Escape)) _onBack?.Invoke();
        }

        void Redraw()
        {
            for (int i = _grid.childCount - 1; i >= 0; i--) Destroy(_grid.GetChild(i).gameObject);
            var meta = MetaProgress.Current;
            var all = Achievements.All;
            _count.text = $"{Achievements.DoneCount(meta)} / {all.Count}";
            int rows = (all.Count + Columns - 1) / Columns;
            for (int i = 0; i < all.Count; i++)
            {
                int row = i / Columns, col = i % Columns;
                int inRow = row == rows - 1 ? all.Count - row * Columns : Columns;   // 마지막 줄은 가운데로
                var pos = new Vector2((col - (inRow - 1) * 0.5f) * (TileSize.x + 20f), ((rows - 1) * 0.5f - row) * (TileSize.y + 20f));
                Tile(all[i], all[i].Done(meta), pos);
            }
        }

        void Tile(AchievementDef a, bool done, Vector2 pos)
        {
            var body = NewImage(_grid, UiKit.CardFill);
            Place(body.rectTransform, pos, TileSize);
            UiKit.Card(body);
            UiKit.SetRim(body, done ? UiKit.Gold : new Color(0.2f, 0.2f, 0.25f, 1f));

            var art = NewImage(body.transform, done ? Color.white : new Color(0f, 0f, 0f, 0.85f));   // 못 이룬 건 검은 그림자
            art.sprite = a.Recipe != null ? PortraitTable.Of(a.Recipe) : GemSprite(a.Gem);
            art.preserveAspect = true;
            art.enabled = art.sprite;
            Place(art.rectTransform, new Vector2(0f, 30f), new Vector2(140f, 140f));

            bool hidden = !done && a.Recipe != null;   // 못 만난 동료는 이름도 가린다
            var name = NewText(body.transform, hidden ? "???" : a.Title, 28);
            name.color = done ? Color.white : new Color(1f, 1f, 1f, 0.45f);
            Place(name.rectTransform, new Vector2(0f, -72f), new Vector2(TileSize.x - 16f, 36f));
            var cond = NewText(body.transform, a.Condition, 20);
            cond.color = done ? new Color(1f, 0.82f, 0.3f, 0.9f) : new Color(1f, 1f, 1f, 0.35f);
            Place(cond.rectTransform, new Vector2(0f, -104f), new Vector2(TileSize.x - 16f, 28f));
        }

        Sprite GemSprite(string id)
        {
            if (id == null) return null;
            if (_gemSprites == null)
            {
                _gemSprites = new Dictionary<string, Sprite>();
                foreach (var sp in Resources.LoadAll<Sprite>("GemIcons")) _gemSprites[sp.name] = sp;
            }
            return _gemSprites.TryGetValue(id, out var s) ? s : null;
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
