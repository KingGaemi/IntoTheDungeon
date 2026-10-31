using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using IntoTheDungeon.Features.Run;
using IntoTheDungeon.Features.Settings;

namespace IntoTheDungeon.Unity.Title
{
    /// <summary>
    /// 타이틀 씬의 시작 메뉴(임시 UI, 코드로 만든다): 새 게임 · 설정 · 종료.
    /// 설정: 피격 흔들림 세기(0~150%), 머리 위 디버그 글자 표시. 값은 GameSettings(PlayerPrefs)에 바로 저장된다.
    /// </summary>
    public sealed class TitleMenu : MonoBehaviour
    {
        public const string SceneName = "Title";
        [SerializeField] string gameSceneName = "NewScene";

        [SerializeField] Color background = new(0.05f, 0.05f, 0.09f, 1f);
        [SerializeField] Color buttonColor = new(0.16f, 0.22f, 0.45f, 1f);
        [SerializeField] Color accent = new(1f, 0.82f, 0.3f, 1f);

        Font _font;
        GameObject _menu, _settings;
        Text _shakeValue, _overlayLabel;

        void Awake() => Build();

        void NewGame()
        {
            RunState.StartNew();
            SceneManager.LoadScene(gameSceneName);
        }

        void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void ShowSettings(bool show)
        {
            _settings.SetActive(show);
            _menu.SetActive(!show);
            if (show) RefreshSettings();
        }

        void RefreshSettings()
        {
            float s = GameSettings.ShakeScale;
            _shakeValue.text = s <= 0f ? "끔" : $"{Mathf.RoundToInt(s * 100f)}%";
            _overlayLabel.text = GameSettings.DebugOverlay ? "디버그 표시: 켜짐" : "디버그 표시: 꺼짐";
        }

        void Build()
        {
            if (!FindFirstObjectByType<EventSystem>())
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            var canvasGo = new GameObject("TitleCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var root = canvasGo.transform;

            Stretch(NewImage(root, background).rectTransform);

            var title = NewText(root, "Into The Dungeon", 110);
            title.color = accent;
            Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 250f), new Vector2(1400f, 140f));
            var sub = NewText(root, "파티를 꾸려 던전 끝까지", 34);
            sub.color = new Color(1f, 1f, 1f, 0.6f);
            Place(sub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 160f), new Vector2(1000f, 50f));

            // 메인 메뉴
            _menu = NewGroup(root);
            NewButton(_menu.transform, "새 게임", new Vector2(0f, 0f), NewGame);
            NewButton(_menu.transform, "설정", new Vector2(0f, -110f), () => ShowSettings(true));
            NewButton(_menu.transform, "종료", new Vector2(0f, -220f), Quit);

            // 설정
            _settings = NewGroup(root);
            var label = NewText(_settings.transform, "피격 흔들림 세기", 34);
            Place(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-150f, 40f), new Vector2(400f, 50f));
            _shakeValue = NewText(_settings.transform, "", 34);
            _shakeValue.color = accent;
            Place(_shakeValue.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(330f, 40f), new Vector2(160f, 50f));
            var slider = NewSlider(_settings.transform, new Vector2(0f, -20f), new Vector2(760f, 30f));
            slider.minValue = 0f;
            slider.maxValue = GameSettings.MaxShakeScale;
            slider.value = GameSettings.ShakeScale;
            slider.onValueChanged.AddListener(v => { GameSettings.ShakeScale = Mathf.Round(v * 20f) / 20f; RefreshSettings(); });   // 5% 단위

            var overlay = NewButton(_settings.transform, "", new Vector2(0f, -120f), () => { GameSettings.DebugOverlay = !GameSettings.DebugOverlay; RefreshSettings(); });
            _overlayLabel = overlay.GetComponentInChildren<Text>();
            NewButton(_settings.transform, "돌아가기", new Vector2(0f, -240f), () => ShowSettings(false));
            _settings.SetActive(false);
        }

        GameObject NewGroup(Transform parent)
        {
            var go = new GameObject("Group", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform);
            return go;
        }

        Button NewButton(Transform parent, string text, Vector2 pos, UnityEngine.Events.UnityAction onClick)
        {
            var img = NewImage(parent, buttonColor);
            img.raycastTarget = true;
            Place(img.rectTransform, new Vector2(0.5f, 0.5f), pos, new Vector2(420f, 86f));
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);
            var t = NewText(img.transform, text, 38);
            Stretch(t.rectTransform);
            return btn;
        }

        Slider NewSlider(Transform parent, Vector2 pos, Vector2 size)
        {
            var go = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            go.transform.SetParent(parent, false);
            Place((RectTransform)go.transform, new Vector2(0.5f, 0.5f), pos, size);

            var bg = NewImage(go.transform, new Color(1f, 1f, 1f, 0.15f));
            Stretch(bg.rectTransform);

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(go.transform, false);
            Stretch((RectTransform)fillArea.transform);
            var fill = NewImage(fillArea.transform, accent);
            var frt = fill.rectTransform;
            frt.anchorMin = Vector2.zero; frt.anchorMax = new Vector2(0f, 1f); frt.offsetMin = frt.offsetMax = Vector2.zero;

            var handleArea = new GameObject("Handle Area", typeof(RectTransform));
            handleArea.transform.SetParent(go.transform, false);
            Stretch((RectTransform)handleArea.transform);
            var handle = NewImage(handleArea.transform, Color.white);
            handle.raycastTarget = true;
            var hrt = handle.rectTransform;
            hrt.anchorMin = new Vector2(0f, 0f); hrt.anchorMax = new Vector2(0f, 1f);
            hrt.sizeDelta = new Vector2(24f, 16f);

            var slider = go.GetComponent<Slider>();
            slider.fillRect = frt;
            slider.handleRect = hrt;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            return slider;
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
