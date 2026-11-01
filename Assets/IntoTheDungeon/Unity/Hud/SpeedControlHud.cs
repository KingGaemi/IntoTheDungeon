using UnityEngine;
using UnityEngine.UI;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Features.Battle;

namespace IntoTheDungeon.Unity.Hud
{
    /// <summary>
    /// 전투 배속(1·2·3배): 오른쪽 위 버튼을 누르거나 Tab 키로 차례로 바뀐다. 고른 배속은 기억해 두고 다음 방에도 이어진다.
    /// 전투 중(지도·정비 화면이 아닐 때)에만 Time.timeScale에 반영하고, 지도·정비 화면에선 1배로 둔다.
    /// </summary>
    public sealed class SpeedControlHud : MonoBehaviour, IWorldInjectable
    {
        const string PrefKey = "BattleSpeed";
        static readonly int[] Speeds = { 1, 2, 3 };

        [SerializeField] KeyCode toggleKey = KeyCode.Tab;
        [SerializeField] Color[] speedColors =
        {
            new(0.16f, 0.22f, 0.45f, 0.95f),
            new(0.55f, 0.42f, 0.12f, 0.95f),
            new(0.65f, 0.2f, 0.15f, 0.95f)
        };

        BattleProgress _progress;
        GameObject _button;
        Image _image;
        Text _label;
        int _index;

        public void Init(IWorld world)
        {
            world.TryGet(out _progress);
            try { _index = Mathf.Clamp(PlayerPrefs.GetInt(PrefKey, 0), 0, Speeds.Length - 1); } catch { _index = 0; }
            if (!_button) Build();
        }

        void Update()
        {
            if (_progress == null || !_button) return;
            bool battle = _progress.Phase != BattlePhase.Map;
            if (_button.activeSelf != battle) _button.SetActive(battle);
            if (battle && Input.GetKeyDown(toggleKey)) Next();

            Time.timeScale = battle ? Speeds[_index] : 1f;
            _label.text = $"{Speeds[_index]}배속 (Tab)";
            _image.color = speedColors[Mathf.Min(_index, speedColors.Length - 1)];
        }

        void OnDestroy() => Time.timeScale = 1f;   // 씬을 나가거나 다시 불러올 때 원래대로

        void Next()
        {
            _index = (_index + 1) % Speeds.Length;
            try { PlayerPrefs.SetInt(PrefKey, _index); } catch { }
        }

        void Build()
        {
            var canvasGo = new GameObject("SpeedCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 92;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            _button = new GameObject("Speed", typeof(RectTransform), typeof(Image), typeof(Button));
            _button.transform.SetParent(canvasGo.transform, false);
            var rt = (RectTransform)_button.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-30f, -30f);
            rt.sizeDelta = new Vector2(210f, 64f);
            _image = _button.GetComponent<Image>();
            var btn = _button.GetComponent<Button>();
            btn.targetGraphic = _image;
            btn.onClick.AddListener(Next);

            var go = new GameObject("Label", typeof(RectTransform), typeof(Text), typeof(Outline));
            go.transform.SetParent(_button.transform, false);
            var lrt = (RectTransform)go.transform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = lrt.offsetMax = Vector2.zero;
            _label = go.GetComponent<Text>();
            _label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _label.fontSize = 30;
            _label.fontStyle = FontStyle.Bold;
            _label.alignment = TextAnchor.MiddleCenter;
            _label.color = Color.white;
            _label.raycastTarget = false;
        }
    }
}
