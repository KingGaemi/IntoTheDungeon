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

        // 히트스톱: 패턴을 끊는 순간 등 아주 잠깐 거의 멈춘 듯 느리게(실제 시간 기준, 배속과 무관)
        const float HitStopScale = 0.04f;
        static float s_hitStopUntil;
        public static void HitStop(float seconds) => s_hitStopUntil = Mathf.Max(s_hitStopUntil, Time.unscaledTime + seconds);

        // 슬로우(끊기 성공 직후 전용): 멈칫(히트스톱)이 풀린 뒤 seconds(실제 시간, 최대 MaxSlowMo) 동안 배속 × scale.
        // PatternBroken/Shattered 직후에만 호출(InterruptFxRenderer.BreakImpact 한 곳, BreakSlowMo 스위치) — 예고·충전·StopFailed 호출 금지. 배속 보조성 연출은 금지 규칙.
        const float MaxSlowMo = 0.15f;
        static float s_slowFrom, s_slowUntil, s_slowScale = 1f;
        public static void SlowMo(float seconds, float scale)
        {
            float from = Mathf.Max(Time.unscaledTime, s_hitStopUntil);
            s_slowFrom = from;
            s_slowUntil = Mathf.Max(s_slowUntil, from + Mathf.Clamp(seconds, 0f, MaxSlowMo));
            s_slowScale = Mathf.Clamp(scale, 0.05f, 1f);
        }

        BattleProgress _progress;
        GameObject _button;
        Image _image;
        Text _label;
        int _index;

        public void Init(IWorld world)
        {
            world.TryGet(out _progress);
            s_hitStopUntil = 0f; s_slowFrom = s_slowUntil = 0f; s_slowScale = 1f;   // 씬 재로드: 지난 멈칫·슬로우 잔류 제거
            try { _index = Mathf.Clamp(PlayerPrefs.GetInt(PrefKey, 0), 0, Speeds.Length - 1); } catch { _index = 0; }
            if (!_button) Build();
        }

        void Update()
        {
            if (_progress == null || !_button) return;
            bool battle = _progress.Phase != BattlePhase.Map;
            if (_button.activeSelf != battle) _button.SetActive(battle);
            if (battle && Input.GetKeyDown(toggleKey)) Next();

            bool stopped = battle && Time.unscaledTime < s_hitStopUntil;
            bool slowed = battle && !stopped && Time.unscaledTime >= s_slowFrom && Time.unscaledTime < s_slowUntil;   // 끊기 직후 슬로우(SlowMo)
            Time.timeScale = !battle ? 1f : stopped ? HitStopScale : (slowed ? s_slowScale : 1f) * Speeds[_index];
            _label.text = $"×{Speeds[_index]}  <size=18><color=#FFFFFF99>Tab</color></size>";
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
            UiKit.Button(btn, _image, speedColors[0]);
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
