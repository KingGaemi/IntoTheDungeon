using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.Abstractions.Gameplay;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.ECS.Components;
using IntoTheDungeon.Features.Battle;
using IntoTheDungeon.Features.Character;
using IntoTheDungeon.Features.Run;
using IntoTheDungeon.Features.Status;

namespace IntoTheDungeon.Unity.Hud
{
    /// <summary>
    /// 보스 연출 HUD(캔버스 91): 보스 웨이브가 시작되면 위쪽에 이름 띠(1.4초, 양옆 금선 펼침·보스 잼 아이콘), 화면 맨 위에 전용 체력바
    /// (채움·밝은 잔상·10% 눈금·50% 굵은 눈금(BossPhaseComponent, 지나면 빨강)·%·광폭 ×N(EnrageComponent))와 그 아래 충전 게이지
    /// (패턴 힘 모으기 진행 + 요구 저지력: 파티 최고 저지력으로 끊을 수 있으면 청록, 모자라면 빨강). 처치하면 바가 하얗게 번쩍이며 가라앉는다.
    /// 보스 2마리 동시는 첫 마리만. 머리 위 체력바는 HealthBarRenderer가 보스에겐 안 그린다. 연출은 전부 unscaled 시간.
    /// </summary>
    [AutoHud]
    public sealed class BossHud : MonoBehaviour, IWorldInjectable
    {
        public const int SortingOrder = 91;
        // (임시값) 크기·시간
        const float BannerW = 1200f, BannerH = 110f, BannerY = 300f, BannerTime = 1.4f, BannerFade = 0.25f;
        const float BarW = 900f, BarH = 26f, BarY = -80f, ChargeH = 6f;
        const float TrailHold = 0.35f, TrailSpeed = 0.8f, KillFlash = 0.5f;
        static readonly Color TrailColor = new(1f, 0.93f, 0.6f, 1f);
        static readonly Color EnrageColor = new(1f, 0.6f, 0.15f, 1f);
        // 패턴 예고색(BossRenderer와 같게): 강타·돌진 빨강(위험) · 치유 의식 초록 · 부화 분홍 · 마법 보호막 파랑
        static readonly Color WarnColor = new(1f, 0.2f, 0.15f, 1f), HealColor = new(0.35f, 1f, 0.45f, 1f), HatchColor = new(1f, 0.45f, 0.7f, 1f), BarrierColor = new(0.4f, 0.65f, 1f, 1f);

        IWorld _world;
        BattleProgress _progress;
        BattleConfig _config;
        CharacterCatalog _catalog;
        GameObject _root;

        // 이름 띠
        GameObject _banner;
        CanvasGroup _bannerGroup;
        Text _bannerName;
        RectTransform _lineL, _lineR;
        Image _gemL, _gemR;
        float _bannerUntil;
        bool _bannerOut;

        // 체력바
        RectTransform _bar;
        GameObject _barGo;
        RectTransform _fill, _trail;
        Image _flash, _phaseTick;
        Text _name, _pct, _enrage;

        // 충전 게이지
        GameObject _charge, _stopGroup;
        RectTransform _chargeFill;
        Image _chargeFillImg, _stopIcon;
        Text _stopText;

        Dictionary<string, Sprite> _gems;
        readonly List<Party.Member> _party = new(8);
        int _seenWave = -1;
        BattlePhase _seenPhase = BattlePhase.Map;
        bool _bossWave;
        string _bossRecipe;
        Entity _boss = Entity.Null;
        bool _hasBoss, _wasAlive;
        float _shown = -1f, _trailRatio = 1f, _holdUntil, _flashUntil, _diedAt = -1f;
        int _shownPct = -1, _shownStacks = -1, _shownReq = -1;

        public void Init(IWorld world)
        {
            _world = world;
            world.TryGet(out _progress);
            world.TryGet(out _config);
            world.TryGet(out _catalog);
            if (!_root) Build();
        }

        void Update()
        {
            if (_world == null || !_root) return;
            float now = Time.unscaledTime, dt = Time.unscaledDeltaTime;
            var em = _world.EntityManager;
            bool map = _progress == null || _progress.Phase == BattlePhase.Map;

            // 웨이브가 바뀌거나 전투 단계로 다시 들어오면 이번 웨이브가 보스 웨이브인지 본다
            if (_progress != null && (_progress.Wave != _seenWave || (_progress.Phase == BattlePhase.Fighting && _seenPhase != BattlePhase.Fighting)))
            {
                _seenWave = _progress.Wave;
                _bossWave = false;
                _bossRecipe = null;
                if (_progress.Phase == BattlePhase.Fighting && _config != null && _progress.Wave >= 1 && _progress.Wave <= _config.Waves.Count)
                    foreach (var s in _config.Waves[_progress.Wave - 1])
                        if (s.Boss) { _bossWave = true; _bossRecipe = s.Recipe; break; }
                if (!_bossWave) ClearBoss();
            }
            if (_progress != null) _seenPhase = _progress.Phase;
            if (map) { ClearBoss(); return; }

            // 보스 찾기(첫 마리): 스폰은 웨이브 시작 다음 프레임일 수 있어 찾을 때까지 매 프레임 본다
            if (_bossWave && !_hasBoss) FindBoss(em, now);
            UpdateBanner(now);
            if (!_hasBoss) return;

            StatusComponent st = default;
            bool exists = em.Exists(_boss) && em.TryGetComponent(_boss, out st);
            bool alive = exists && st.IsAlive;
            if (_wasAlive && !alive)   // 처치: 흰 플래시 + 바 펀치
            {
                _wasAlive = false;
                _diedAt = now;
                _flashUntil = now + KillFlash;
                UiTween.Punch(_bar, 0.06f, 0.3f);
            }
            bool showBar = alive || (_diedAt >= 0f && now < _diedAt + KillFlash);
            if (_barGo.activeSelf != showBar) _barGo.SetActive(showBar);
            if (!showBar) { _hasBoss = false; _bossWave = false; return; }

            // 체력: 바로 줄고 잔상은 잠깐 버틴 뒤 따라온다
            float ratio = alive && st.MaxHp > 0 ? Mathf.Clamp01((float)st.CurrentHp / st.MaxHp) : 0f;
            if (_shown < 0f) { _shown = ratio; _trailRatio = ratio; }
            if (ratio < _shown - 1e-4f) _holdUntil = now + TrailHold;
            _shown = ratio;
            if (_trailRatio < ratio) _trailRatio = ratio;
            else if (now >= _holdUntil) _trailRatio = Mathf.MoveTowards(_trailRatio, ratio, TrailSpeed * dt);
            _fill.anchorMax = new Vector2(ratio, 1f);
            _trail.anchorMax = new Vector2(_trailRatio, 1f);
            int pct = alive ? Mathf.Max(1, Mathf.CeilToInt(ratio * 100f)) : 0;
            if (pct != _shownPct) { _shownPct = pct; _pct.text = pct + "%"; }

            float fa = now < _flashUntil ? (_flashUntil - now) / KillFlash : 0f;
            if (_flash.enabled != fa > 0f) _flash.enabled = fa > 0f;
            if (fa > 0f) _flash.color = new Color(1f, 1f, 1f, 0.9f * fa);

            // 광폭 ×N(EnrageComponent — patterns-boss가 채움, 없으면 빈 칸)
            EnrageComponent en = default;
            int stacks = alive && em.TryGetComponent(_boss, out en) ? en.Stacks : 0;
            if (stacks != _shownStacks)
            {
                _shownStacks = stacks;
                _enrage.text = stacks >= 1 ? "×" + stacks : "";
                if (stacks >= 1) UiTween.Punch(_enrage.rectTransform, 0.3f, 0.3f);
            }

            // 2페이즈 눈금(BossPhaseComponent 없으면 50% 자리에 고정)
            float threshold = 0.5f;
            bool triggered = false;
            BossPhaseComponent ph = default;
            if (alive && em.TryGetComponent(_boss, out ph) && ph.Threshold > 0f) { threshold = Mathf.Clamp01(ph.Threshold); triggered = ph.Triggered; }
            _phaseTick.rectTransform.anchoredPosition = new Vector2(-BarW * 0.5f + BarW * threshold, 0f);
            _phaseTick.color = triggered ? UiKit.Negative : new Color(1f, 1f, 1f, 0.9f);

            // 충전 게이지: 힘 모으는 동안 예고색으로 차오르고, 왼쪽에 망치 + 요구 저지력(끊을 수 있으면 청록, 모자라면 빨강)
            BossSlamComponent slam = default;
            bool charging = alive && em.TryGetComponent(_boss, out slam) && slam.Charging && !slam.Fired;
            if (_charge.activeSelf != charging) _charge.SetActive(charging);
            if (charging)
            {
                float k = Mathf.Clamp01(slam.Timer / Mathf.Max(0.01f, slam.ChargeTime));
                _chargeFill.anchorMax = new Vector2(k, 1f);
                var c = slam.Kind switch { PatternKind.Heal => HealColor, PatternKind.Hatch => HatchColor, PatternKind.Barrier => BarrierColor, _ => WarnColor };
                float blink = 0.75f + 0.25f * Mathf.Sin(now * (10f + 20f * k));   // 끝으로 갈수록 빨리
                _chargeFillImg.color = new Color(c.r, c.g, c.b, blink);
                bool hasReq = slam.StopRequired > 0;
                if (_stopGroup.activeSelf != hasReq) _stopGroup.SetActive(hasReq);
                if (hasReq)
                {
                    int best = StopPowerUi.BestPartyStop(em, _party);
                    var sc = best >= slam.StopRequired ? UiKit.Stop : UiKit.Negative;
                    if (_shownReq != slam.StopRequired) { _shownReq = slam.StopRequired; _stopText.text = slam.StopRequired.ToString(); }
                    _stopText.color = sc;
                    _stopIcon.color = sc;
                }
            }
        }

        void FindBoss(IEntityManager em, float now)
        {
            foreach (var chunk in em.GetChunks(typeof(BossComponent), typeof(StatusComponent), typeof(TeamComponent)))
            {
                var statuses = chunk.GetComponentArray<StatusComponent>();
                var teams = chunk.GetComponentArray<TeamComponent>();
                var entities = chunk.GetEntities();
                for (int i = 0; i < chunk.Count; i++)
                {
                    if (teams[i].TeamFlag != TeamFlag.Enemy || !statuses[i].IsAlive) continue;
                    _boss = entities[i];
                    _hasBoss = true;
                    _wasAlive = true;
                    _diedAt = -1f;
                    _shown = -1f;
                    _shownPct = _shownStacks = _shownReq = -1;
                    _flashUntil = 0f;
                    string name = _catalog != null ? _catalog.DisplayName(_bossRecipe) : _bossRecipe;
                    if (string.IsNullOrEmpty(name)) name = "보스";
                    _name.text = name;
                    _enrage.text = "";
                    _flash.enabled = false;
                    _barGo.SetActive(true);
                    UiTween.Scale(_bar, new Vector3(0.9f, 0.9f, 1f), Vector3.one, 0.3f, Ease.OutBack);
                    ShowBanner(name, now);
                    return;
                }
            }
        }

        void ShowBanner(string name, float now)
        {
            _bannerName.text = name;
            var run = RunState.Current;
            var gem = GemSprite(Encounters.BossGemOf(run != null ? run.Stage : 1));
            _gemL.sprite = _gemR.sprite = gem;
            _gemL.enabled = _gemR.enabled = gem;
            _banner.SetActive(true);
            _bannerUntil = now + BannerTime;
            _bannerOut = false;
            _lineL.localScale = _lineR.localScale = new Vector3(0f, 1f, 1f);
            UiTween.Fade(_bannerGroup, 0f, 1f, BannerFade);
            UiTween.Scale((RectTransform)_banner.transform, new Vector3(1f, 0.6f, 1f), Vector3.one, 0.3f, Ease.OutBack);
            UiTween.Scale(_lineL, new Vector3(0f, 1f, 1f), Vector3.one, 0.3f, Ease.OutCubic, 0.1f);   // 양옆 금선 펼침
            UiTween.Scale(_lineR, new Vector3(0f, 1f, 1f), Vector3.one, 0.3f, Ease.OutCubic, 0.1f);
            UiTween.Punch(_bannerName.rectTransform, 0.1f, 0.35f);
        }

        void UpdateBanner(float now)
        {
            if (!_banner.activeSelf) return;
            if (!_bannerOut && now >= _bannerUntil - BannerFade)
            {
                _bannerOut = true;
                UiTween.Fade(_bannerGroup, _bannerGroup.alpha, 0f, BannerFade);
            }
            if (now >= _bannerUntil) _banner.SetActive(false);
        }

        void ClearBoss()
        {
            _hasBoss = false;
            _bossWave = false;
            _boss = Entity.Null;
            _wasAlive = false;
            _diedAt = -1f;
            if (_barGo && _barGo.activeSelf) _barGo.SetActive(false);
            if (_banner && _banner.activeSelf) _banner.SetActive(false);
        }

        Sprite GemSprite(string id)
        {
            if (_gems == null)
            {
                _gems = new Dictionary<string, Sprite>();
                foreach (var sp in Resources.LoadAll<Sprite>("GemIcons")) _gems[sp.name] = sp;
            }
            return id != null && _gems.TryGetValue(id, out var s) ? s : null;
        }

        void Build()
        {
            var canvas = UiKit.NewCanvas(transform, "BossCanvas", SortingOrder, raycaster: false);   // 표시 전용(클릭 안 받음)
            _root = canvas.gameObject;
            BuildBanner(canvas.transform);
            BuildBar(canvas.transform);
        }

        void BuildBanner(Transform parent)
        {
            var panel = UiKit.NewImage(parent, UiKit.PanelFill, name: "BossBanner");
            UiKit.Place(panel.rectTransform, new Vector2(0f, BannerY), new Vector2(BannerW, BannerH));
            UiKit.Panel(panel, new Color(UiKit.PanelFill.r, UiKit.PanelFill.g, UiKit.PanelFill.b, 0.92f), UiKit.Gold);
            _banner = panel.gameObject;
            _bannerGroup = _banner.AddComponent<CanvasGroup>();
            _bannerGroup.blocksRaycasts = false;
            _bannerGroup.interactable = false;

            _bannerName = UiKit.NewText(panel.transform, "", 52, UiKit.Gold);
            UiKit.Place(_bannerName.rectTransform, Vector2.zero, new Vector2(600f, 70f));
            UiKit.Soft(_bannerName);

            var lineL = UiKit.NewImage(panel.transform, UiKit.Gold, name: "LineL");
            UiKit.Place(lineL.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-205f, 0f), new Vector2(365f, 3f));
            lineL.rectTransform.pivot = new Vector2(1f, 0.5f);   // 안쪽 끝에서 바깥으로 펼쳐진다
            _lineL = lineL.rectTransform;
            var lineR = UiKit.NewImage(panel.transform, UiKit.Gold, name: "LineR");
            UiKit.Place(lineR.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(205f, 0f), new Vector2(365f, 3f));
            lineR.rectTransform.pivot = new Vector2(0f, 0.5f);
            _lineR = lineR.rectTransform;

            _gemL = UiKit.NewImage(panel.transform, Color.white, name: "GemL");
            _gemL.preserveAspect = true;
            UiKit.Place(_gemL.rectTransform, new Vector2(-175f, 0f), new Vector2(40f, 40f));
            _gemR = UiKit.NewImage(panel.transform, Color.white, name: "GemR");
            _gemR.preserveAspect = true;
            UiKit.Place(_gemR.rectTransform, new Vector2(175f, 0f), new Vector2(40f, 40f));
            _banner.SetActive(false);
        }

        void BuildBar(Transform parent)
        {
            var border = UiKit.NewImage(parent, new Color(0f, 0f, 0f, 0.85f), name: "BossBar");
            UiKit.Place(border.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, BarY), new Vector2(BarW + 4f, BarH + 4f));
            _bar = border.rectTransform;
            _barGo = border.gameObject;

            var back = UiKit.NewImage(_bar, new Color(0.08f, 0.08f, 0.1f, 0.85f), name: "Back");
            UiKit.Stretch(back.rectTransform, 2f);
            var trail = UiKit.NewImage(back.transform, TrailColor, name: "Trail");
            UiKit.Stretch(trail.rectTransform);
            _trail = trail.rectTransform;
            var fill = UiKit.NewImage(back.transform, UiKit.EnemyHp, name: "Fill");
            UiKit.Stretch(fill.rectTransform);
            _fill = fill.rectTransform;

            // 10% 눈금 9개 + 2페이즈 굵은 눈금(기본 50%)
            for (int i = 1; i < 10; i++)
            {
                var tick = UiKit.NewImage(back.transform, new Color(0f, 0f, 0f, 0.55f), name: "Tick");
                UiKit.Place(tick.rectTransform, new Vector2(0f, 0.5f), new Vector2(BarW * i / 10f - 1f, 0f), new Vector2(2f, BarH));
            }
            _phaseTick = UiKit.NewImage(back.transform, new Color(1f, 1f, 1f, 0.9f), name: "PhaseTick");
            UiKit.Place(_phaseTick.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(4f, BarH + 8f));

            _flash = UiKit.NewImage(back.transform, Color.white, name: "Flash");
            UiKit.Stretch(_flash.rectTransform);
            _flash.enabled = false;

            _name = UiKit.NewText(_bar, "", 24, UiKit.TextMain, TextAnchor.MiddleLeft);
            UiKit.Place(_name.rectTransform, new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(400f, BarH));
            UiKit.Soft(_name);
            _pct = UiKit.NewText(_bar, "", 20, UiKit.TextMain, TextAnchor.MiddleRight);
            UiKit.Place(_pct.rectTransform, new Vector2(1f, 0.5f), new Vector2(-10f, 0f), new Vector2(100f, BarH));
            UiKit.Soft(_pct);
            _enrage = UiKit.NewText(_bar, "", 24, EnrageColor, TextAnchor.MiddleLeft);   // 오른쪽 끝 바깥 ×N
            UiKit.Place(_enrage.rectTransform, new Vector2(1f, 0.5f), new Vector2(14f, 0f), new Vector2(80f, BarH + 10f));
            _enrage.rectTransform.pivot = new Vector2(0f, 0.5f);
            UiKit.Soft(_enrage);

            // 충전 게이지(바 바로 아래, 바와 함께 숨고 켜짐)
            var charge = UiKit.NewImage(_bar, new Color(0f, 0f, 0f, 0.7f), name: "Charge");
            UiKit.Place(charge.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, -4f), new Vector2(BarW, ChargeH));
            charge.rectTransform.pivot = new Vector2(0.5f, 1f);
            _charge = charge.gameObject;
            _chargeFillImg = UiKit.NewImage(charge.transform, WarnColor, name: "Fill");
            UiKit.Stretch(_chargeFillImg.rectTransform);
            _chargeFill = _chargeFillImg.rectTransform;
            _chargeFill.anchorMax = new Vector2(0f, 1f);

            var stop = new GameObject("Stop", typeof(RectTransform));
            stop.transform.SetParent(charge.transform, false);
            var srt = (RectTransform)stop.transform;
            UiKit.Place(srt, new Vector2(0f, 0.5f), new Vector2(-8f, 0f), new Vector2(90f, 26f));
            srt.pivot = new Vector2(1f, 0.5f);
            _stopGroup = stop;
            _stopIcon = UiKit.NewImage(stop.transform, UiKit.Stop, name: "Icon");
            _stopIcon.sprite = StopPowerUi.Icon;
            _stopIcon.preserveAspect = true;
            UiKit.Place(_stopIcon.rectTransform, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(22f, 22f));
            _stopText = UiKit.NewText(stop.transform, "", 22, UiKit.Stop, TextAnchor.MiddleRight);
            UiKit.Stretch(_stopText.rectTransform);
            _stopText.rectTransform.offsetMax = new Vector2(-26f, 0f);
            UiKit.Soft(_stopText);
            _charge.SetActive(false);
            _barGo.SetActive(false);
        }
    }
}
