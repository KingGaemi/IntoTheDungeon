using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Features.Battle;
using IntoTheDungeon.Features.Run;

namespace IntoTheDungeon.Unity.Hud
{
    /// <summary>
    /// 전투 진행 표시(임시 UI, 코드로 만든다): 위쪽 가운데 웨이브·남은 적, 화면 가운데 큰 알림
    /// ("전투 시작" / "웨이브 클리어" / "방 클리어!" / "던전 클리어!" / "패배"), 끝나면 버튼:
    /// 이기면 "정비하러 가기"(보상·덱 정비 → 지도), 보스를 깼거나 지면 "새 던전"(새 판). 둘 다 화면이 어두워진 뒤 씬을 다시 불러온다.
    /// 버튼은 떨어진 아이템이 모두 가방에 들어간 뒤 나온다.
    /// </summary>
    public sealed class BattleStatusHud : MonoBehaviour, IWorldInjectable
    {
        [SerializeField] Color victoryColor = new(1f, 0.82f, 0.3f, 1f);
        [SerializeField] Color defeatColor = new(0.85f, 0.25f, 0.25f, 1f);

        [Header("방 클리어 → 정비 화면")]
        [Tooltip("아이템이 모두 가방에 들어간 뒤 이만큼 있다가 버튼이 나온다")]
        [SerializeField] float buttonDelay = 0.5f;
        [SerializeField] float fadeOutTime = 1.0f;
        [SerializeField] float fadeInTime = 0.8f;

        // 씬을 다시 불러온 뒤 어두운 화면에서 밝아지게(정비·지도 화면이 천천히 나타남)
        static bool s_fadeInNext;

        IWorld _world;
        BattleProgress _progress;
        Text _status, _banner, _retryLabel;
        GameObject _retry, _toTitle;
        Image _fade;
        float _fadeAlpha, _settled;
        bool _leaving;

        public void Init(IWorld world)
        {
            _world = world;
            world.TryGet(out _progress);
            if (!_status) Build();
            if (s_fadeInNext) { s_fadeInNext = false; _fadeAlpha = 1f; }
        }

        void Update()
        {
            if (_progress == null || !_status) return;
            var p = _progress;

            var run = RunState.Current;
            string room = run.Current != null ? $"스테이지 {run.Stage} · {run.CurrentLayer + 1}층 {Encounters.Label(run.Current.Type)}   ·   " : "";
            _status.text = p.Wave > 0 && p.Phase != BattlePhase.Map ? $"{room}웨이브 {p.Wave} / {p.TotalWaves}   ·   남은 적 {p.EnemiesAlive}   ·   골드 {run.Gold}" : "";

            string banner = "";
            Color color = Color.white;
            switch (p.Phase)
            {
                case BattlePhase.Intro: banner = "전투 시작!"; break;
                case BattlePhase.Fighting: if (p.PhaseTime < 1.2f) banner = $"웨이브 {p.Wave}"; break;
                case BattlePhase.Interlude: banner = "웨이브 클리어!"; break;
                case BattlePhase.Victory:
                    banner = run.Phase == RunPhase.Cleared ? "던전 클리어!" : run.StageCleared ? $"스테이지 {run.Stage} 클리어!" : "방 클리어!";
                    color = victoryColor;
                    break;
                case BattlePhase.Defeat: banner = "패배"; color = defeatColor; break;
            }
            _retryLabel.text = p.Phase == BattlePhase.Victory && run.Phase == RunPhase.Camp ? "정비하러 가기" : "새 던전";
            _banner.text = banner;
            _banner.color = color;
            // 버튼은 아이템이 모두 가방에 들어간 뒤 잠깐 있다가 나온다(누르면 화면이 어두워지며 넘어감)
            bool flying = _world != null && LootDropSystem.AnyFlying(_world.EntityManager);
            _settled = p.IsOver && !flying ? _settled + Time.deltaTime : 0f;
            bool showButtons = p.IsOver && _settled >= buttonDelay && !_leaving;
            if (_retry.activeSelf != showButtons) _retry.SetActive(showButtons);
            bool runOver = showButtons && run.Phase is RunPhase.Cleared or RunPhase.Failed;   // 판이 끝났을 때만 타이틀로
            if (_toTitle.activeSelf != runOver) _toTitle.SetActive(runOver);
            UpdateFade();
        }

        // 화면 덮개: 떠날 땐 점점 어두워지고 다 어두워지면 씬을 다시 불러옴, 들어올 땐 점점 밝아짐
        void UpdateFade()
        {
            if (_leaving)
            {
                _fadeAlpha = Mathf.MoveTowards(_fadeAlpha, 1f, Time.unscaledDeltaTime / fadeOutTime);   // 배속과 무관하게
                if (_fadeAlpha >= 1f) { s_fadeInNext = true; _leaving = false; Reload(); }
            }
            else _fadeAlpha = Mathf.MoveTowards(_fadeAlpha, 0f, Time.unscaledDeltaTime / fadeInTime);
            _fade.color = new Color(0f, 0f, 0f, _fadeAlpha);
            _fade.raycastTarget = _fadeAlpha > 0.01f;   // 바뀌는 동안 클릭 막기
            if (_fade.enabled != _fadeAlpha > 0f) _fade.enabled = _fadeAlpha > 0f;
        }

        void ToTitle()
        {
            RunState.StartNew();
            SceneManager.LoadScene(IntoTheDungeon.Unity.Title.TitleMenu.SceneName);
        }

        void Retry()
        {
            var run = RunState.Current;
            if (run.Phase is RunPhase.Cleared or RunPhase.Failed) RunState.StartNew();   // 판이 끝났으면 새 지도
            _leaving = true;                                                          // 어두워진 뒤 다시 불러옴
        }

        // 다시 불러오면 정비 또는 지도부터
        static void Reload() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

        void Build()
        {
            var canvasGo = new GameObject("BattleCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            _status = NewText("Status", canvasGo.transform, font, 34);
            Place(_status.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(900f, 60f));

            _banner = NewText("Banner", canvasGo.transform, font, 96);
            Place(_banner.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 140f), new Vector2(1200f, 140f));

            // 다시 하기 버튼(끝났을 때만)
            _retry = new GameObject("Retry", typeof(RectTransform), typeof(Image), typeof(Button));
            _retry.transform.SetParent(canvasGo.transform, false);
            var img = _retry.GetComponent<Image>();
            img.color = new Color(0.16f, 0.22f, 0.45f, 0.95f);
            Place((RectTransform)_retry.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(320f, 90f));
            var btn = _retry.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(Retry);
            var label = NewText("Label", _retry.transform, font, 40);
            var lrt = label.rectTransform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = lrt.offsetMax = Vector2.zero;
            label.text = "다음 방으로";
            _retryLabel = label;
            _retry.SetActive(false);

            // 타이틀로(판이 끝났을 때만, 다시 하기 아래)
            _toTitle = new GameObject("ToTitle", typeof(RectTransform), typeof(Image), typeof(Button));
            _toTitle.transform.SetParent(canvasGo.transform, false);
            var timg = _toTitle.GetComponent<Image>();
            timg.color = new Color(0.12f, 0.12f, 0.18f, 0.95f);
            Place((RectTransform)_toTitle.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -120f), new Vector2(320f, 90f));
            var tbtn = _toTitle.GetComponent<Button>();
            tbtn.targetGraphic = timg;
            tbtn.onClick.AddListener(ToTitle);
            var tlabel = NewText("Label", _toTitle.transform, font, 40);
            var trt = tlabel.rectTransform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
            tlabel.text = "타이틀로";
            _toTitle.SetActive(false);

            // 화면 전환 덮개(정비 화면 캔버스보다 위)
            var fadeGo = new GameObject("FadeCanvas", typeof(Canvas), typeof(GraphicRaycaster));
            fadeGo.transform.SetParent(transform, false);
            var fadeCanvas = fadeGo.GetComponent<Canvas>();
            fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            fadeCanvas.sortingOrder = 500;
            _fade = new GameObject("Fade", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            var frt = _fade.rectTransform;
            frt.SetParent(fadeGo.transform, false);
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one; frt.offsetMin = frt.offsetMax = Vector2.zero;
            _fade.color = Color.clear;
            _fade.enabled = false;
        }

        static Text NewText(string name, Transform parent, Font font, int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Outline));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = font;
            t.fontSize = fontSize;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.raycastTarget = false;
            t.color = Color.white;
            go.GetComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);   // 배경 위에서도 읽히게
            return t;
        }

        static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }
    }
}
