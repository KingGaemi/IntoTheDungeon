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
    /// 이기면 "정비하러 가기"(보상·덱 정비 → 지도), 보스를 깼거나 지면 "새 던전"(새 판). 둘 다 씬을 다시 불러온다.
    /// </summary>
    public sealed class BattleStatusHud : MonoBehaviour, IWorldInjectable
    {
        [SerializeField] Color victoryColor = new(1f, 0.82f, 0.3f, 1f);
        [SerializeField] Color defeatColor = new(0.85f, 0.25f, 0.25f, 1f);

        BattleProgress _progress;
        Text _status, _banner, _retryLabel;
        GameObject _retry;

        public void Init(IWorld world)
        {
            world.TryGet(out _progress);
            if (!_status) Build();
        }

        void Update()
        {
            if (_progress == null || !_status) return;
            var p = _progress;

            var run = RunState.Current;
            string room = run.Current != null ? $"{run.CurrentLayer + 1}층 {Encounters.Label(run.Current.Type)}   ·   " : "";
            _status.text = p.Wave > 0 && p.Phase != BattlePhase.Map ? $"{room}웨이브 {p.Wave} / {p.TotalWaves}   ·   남은 적 {p.EnemiesAlive}" : "";

            string banner = "";
            Color color = Color.white;
            switch (p.Phase)
            {
                case BattlePhase.Intro: banner = "전투 시작!"; break;
                case BattlePhase.Fighting: if (p.PhaseTime < 1.2f) banner = $"웨이브 {p.Wave}"; break;
                case BattlePhase.Interlude: banner = "웨이브 클리어!"; break;
                case BattlePhase.Victory:
                    banner = run.Phase == RunPhase.Cleared ? "던전 클리어!" : "방 클리어!";
                    color = victoryColor;
                    break;
                case BattlePhase.Defeat: banner = "패배"; color = defeatColor; break;
            }
            _retryLabel.text = p.Phase == BattlePhase.Victory && run.Phase == RunPhase.Camp ? "정비하러 가기" : "새 던전";
            _banner.text = banner;
            _banner.color = color;
            if (_retry.activeSelf != p.IsOver) _retry.SetActive(p.IsOver);
        }

        void Retry()
        {
            var run = RunState.Current;
            if (run.Phase is RunPhase.Cleared or RunPhase.Failed) RunState.StartNew();   // 판이 끝났으면 새 지도
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);         // 다시 불러오면 정비 또는 지도부터
        }

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
