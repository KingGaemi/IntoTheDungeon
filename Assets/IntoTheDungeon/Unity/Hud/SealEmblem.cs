using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using IntoTheDungeon.Features.Run;

namespace IntoTheDungeon.Unity.Hud
{
    /// <summary>봉인석 그림(Resources/Seal*.png, 코드로 그린 도트)과 단계별 색·이름.</summary>
    public static class SealArt
    {
        public const int Px = 64;   // 원판 그림 크기(픽셀)

        /// <summary>룬 10개의 중심(원판 중심 기준 픽셀, y 위쪽). 위에서 시작해 시계 방향 = 1단계부터.</summary>
        public static readonly Vector2[] RunePos =
        {
            new(-0.5f, 21.5f), new(11.5f, 17.5f), new(19.5f, 6.5f), new(19.5f, -5.5f), new(11.5f, -16.5f),
            new(-0.5f, -20.5f), new(-12.5f, -16.5f), new(-20.5f, -5.5f), new(-20.5f, 6.5f), new(-12.5f, 17.5f)
        };

        /// <summary>금이 하나씩 더 가는 단계.</summary>
        public static readonly int[] CrackTiers = { 3, 6, 9 };

        static Sprite _stone, _core;
        static Sprite[] _runes, _cracks;

        public static Sprite Stone { get { Load(); return _stone; } }
        public static Sprite Core { get { Load(); return _core; } }
        public static Sprite Rune(int i) { Load(); return _runes[i]; }
        public static Sprite Crack(int lv) { Load(); return _cracks[lv]; }

        static void Load()
        {
            if (_stone) return;
            _stone = Whole("SealStone");
            _core = Whole("SealCore");
            var rt = Resources.Load<Texture2D>("SealRunes");
            _runes = new Sprite[10];
            for (int i = 0; i < 10; i++) _runes[i] = Sprite.Create(rt, new Rect(i * 7, 0, 7, 7), new Vector2(0.5f, 0.5f), 100f);
            var ct = Resources.Load<Texture2D>("SealCracks");
            _cracks = new Sprite[3];
            for (int i = 0; i < 3; i++) _cracks[i] = Sprite.Create(ct, new Rect(i * Px, 0, Px, Px), new Vector2(0.5f, 0.5f), 100f);
        }

        static Sprite Whole(string name)
        {
            var t = Resources.Load<Texture2D>(name);
            return Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>단계 색: 낮으면 차가운 하늘색 → 보라 → 높으면 자홍.</summary>
        public static Color TierColor(int t)
        {
            float k = Mathf.Clamp01((float)t / SealStone.MaxTier);
            var a = new Color(0.55f, 0.8f, 1f);
            var b = new Color(0.72f, 0.45f, 1f);
            var c = new Color(1f, 0.36f, 0.72f);
            return k < 0.5f ? Color.Lerp(a, b, k * 2f) : Color.Lerp(b, c, (k - 0.5f) * 2f);
        }

        public static string TierTitle(int t) => t <= 0 ? "봉인됨" : t <= 3 ? "균열" : t <= 6 ? "각성" : t <= 9 ? "폭주" : "완전 해방";

        public static string Describe(int t) =>
            $"몹 체력 ×{SealStone.EnemyHpMul(t):0.00}    몹 공격력 ×{SealStone.EnemyDamageMul(t):0.00}\n" +
            $"처치 경험치·골드 ×{SealStone.TotalRewardMul(t):0.00}    드랍 확률 ×{SealStone.DropMul(t):0.0}\n" +
            $"좋은 등급 +{SealStone.RareBonus(t) * 100f:0}%p    판 끝 잼 +{SealStone.EndGems(t)}";
    }

    /// <summary>
    /// 봉인석 문양(UI): 돌 원판 + 단계만큼 켜지는 룬 + 수정 핵 + 금 + 후광·빛줄기·불씨.
    /// 단계를 올리면 새 룬이 번쩍 튀어 오르며 불꽃·충격파가 퍼지고 원판이 흔들린다. 시선 끌기용(big)과 배지용(작게, 입자 없음) 둘 다 쓴다.
    /// Tick은 Time.unscaledDeltaTime으로 부른다(전투 배속과 무관).
    /// </summary>
    public sealed class SealEmblem
    {
        sealed class Particle
        {
            public Image Img;
            public Vector2 Pos, Vel;
            public float Age, Life, Size, EndSize, Drag;
            public Color Col;
            public bool Active;
        }

        const float PopTime = 0.45f;

        readonly RectTransform _root, _shakeRoot, _fx, _rayRoot;
        readonly float _s;
        readonly bool _big;
        readonly Image _aura, _stone, _core, _coreGlow, _flash;
        readonly Image[] _rays;
        readonly Image[] _cracks = new Image[3];
        readonly Image[] _runes = new Image[10], _runeGlows = new Image[10];
        readonly float[] _runePop = new float[10];
        readonly float[] _crackPop = new float[3];
        readonly List<Particle> _parts = new();

        int _tier;
        float _time, _shake, _flashA, _ember, _boost, _level;

        public RectTransform Rect => _root;
        public int Tier => _tier;
        public Image RuneHit(int i) => _runeGlows[i];
        public Image CoreHit => _coreGlow;

        public SealEmblem(Transform parent, float pixelScale, bool big)
        {
            _s = pixelScale;
            _big = big;
            for (int i = 0; i < 10; i++) _runePop[i] = PopTime;
            for (int i = 0; i < 3; i++) _crackPop[i] = PopTime;

            _root = NewRect("SealEmblem", parent, Vector2.one * SealArt.Px * _s);
            _shakeRoot = NewRect("Shake", _root, Vector2.one * SealArt.Px * _s);

            _aura = NewImage(_shakeRoot, FxSprites.DotSprite, Vector2.zero, Vector2.one * SealArt.Px * _s * 1.9f);
            if (_big)
            {
                _rayRoot = NewRect("Rays", _shakeRoot, Vector2.zero);
                _rays = new Image[14];
                for (int i = 0; i < _rays.Length; i++)
                {
                    var r = NewImage(_rayRoot, FxSprites.BeamSprite, Vector2.zero, new Vector2(SealArt.Px * _s * 0.22f, SealArt.Px * _s * 1.25f));
                    r.rectTransform.pivot = new Vector2(0.5f, 0f);
                    r.rectTransform.localRotation = Quaternion.Euler(0f, 0f, i * 360f / _rays.Length);
                    _rays[i] = r;
                }
            }
            _stone = NewImage(_shakeRoot, SealArt.Stone, Vector2.zero, Vector2.one * SealArt.Px * _s);
            for (int i = 0; i < 3; i++)
                _cracks[i] = NewImage(_shakeRoot, SealArt.Crack(i), Vector2.zero, Vector2.one * SealArt.Px * _s);
            for (int i = 0; i < 10; i++)
            {
                _runeGlows[i] = NewImage(_shakeRoot, FxSprites.DotSprite, SealArt.RunePos[i] * _s, Vector2.one * 15f * _s);
                _runes[i] = NewImage(_shakeRoot, SealArt.Rune(i), SealArt.RunePos[i] * _s, Vector2.one * 7f * _s);
            }
            _coreGlow = NewImage(_shakeRoot, FxSprites.DotSprite, Vector2.zero, Vector2.one * 30f * _s);
            _core = NewImage(_shakeRoot, SealArt.Core, Vector2.zero, Vector2.one * 16f * _s);
            _flash = NewImage(_shakeRoot, FxSprites.DotSprite, Vector2.zero, Vector2.one * SealArt.Px * _s * 1.5f);
            _fx = NewRect("Fx", _root, Vector2.zero);
            _level = 0f;
        }

        /// <summary>단계를 바꾼다. animate면 켜진 룬마다 번쩍·불꽃·충격파, 꺼진 룬은 재가 흩어진다.</summary>
        public void SetTier(int tier, bool animate)
        {
            tier = Mathf.Clamp(tier, 0, SealStone.MaxTier);
            int old = _tier;
            _tier = tier;
            if (!animate || tier == old) { if (!animate) _level = tier; return; }

            var col = SealArt.TierColor(tier);
            if (tier > old)
            {
                for (int i = old; i < tier; i++)
                {
                    _runePop[i] = 0f;
                    Burst(SealArt.RunePos[i] * _s, col, _big ? 14 : 4, 260f);
                }
                for (int lv = 0; lv < 3; lv++)
                    if (old < SealArt.CrackTiers[lv] && tier >= SealArt.CrackTiers[lv]) { _crackPop[lv] = 0f; _shake += 0.5f; Burst(Vector2.zero, col, _big ? 26 : 6, 420f); }
                Ring(Vector2.zero, col, SealArt.Px * _s * 0.5f, SealArt.Px * _s * (1.6f + 0.06f * tier), 0.6f);
                _shake += 0.35f + 0.04f * tier;
                _flashA = Mathf.Max(_flashA, 0.35f + 0.03f * tier);
            }
            else
            {
                for (int i = tier; i < old; i++)
                    Burst(SealArt.RunePos[i] * _s, new Color(0.45f, 0.45f, 0.55f), _big ? 8 : 3, 90f);   // 꺼진 룬은 잿빛 가루
            }
        }

        /// <summary>출발: 모든 룬이 다시 타오르고 빛이 터진다.</summary>
        public void Ignite()
        {
            var col = SealArt.TierColor(_tier);
            for (int i = 0; i < _tier; i++) _runePop[i] = 0f;
            Burst(Vector2.zero, col, 60, 620f);
            Burst(Vector2.zero, Color.white, 24, 380f);
            for (int k = 0; k < 3; k++) Ring(Vector2.zero, k == 1 ? Color.white : col, SealArt.Px * _s * 0.3f, SealArt.Px * _s * (2.2f + k * 0.7f), 0.7f + k * 0.2f);
            _shake += 1.1f;
            _flashA = 1f;
            _boost = 1f;
        }

        public void Tick(float dt)
        {
            _time += dt;
            _level = Mathf.MoveTowards(_level, _tier, dt * 6f);
            float k = _level / SealStone.MaxTier;
            var col = SealArt.TierColor(_tier);
            float pulse = Mathf.Sin(_time * 2.6f);

            // 흔들림(원판·룬·후광 같이)
            _shake = Mathf.MoveTowards(Mathf.Min(_shake, 1.4f), 0f, dt * 2.2f);
            float amp = _shake * _shake * _s * 3f;
            _shakeRoot.anchoredPosition = amp > 0.01f ? new Vector2(Random.Range(-amp, amp), Random.Range(-amp, amp)) : Vector2.zero;

            // 후광: 단계가 높을수록 크고 진하게 숨 쉰다
            // 배지(작게)는 은은하게: 빛 번짐을 줄이고 맥동도 거의 없이(글자·룬이 잘 보이게)
            float glowMul = _big ? 1f : 0.25f;
            float pulseMul = _big ? 1f : 0.3f;
            pulse *= pulseMul;
            float aura = _tier == 0 ? 0.06f : 0.16f + 0.34f * k;
            _aura.color = WithA(col, (aura * (1f + 0.18f * pulse) + 0.5f * _boost) * glowMul);
            _aura.rectTransform.localScale = Vector3.one * (_big ? 0.85f + 0.3f * k + 0.04f * pulse + 0.6f * _boost : 0.75f);

            // 빛줄기: 느리게 돌고 하나씩 깜빡임
            if (_rays != null)
            {
                _rayRoot.localRotation = Quaternion.Euler(0f, 0f, -_time * (5f + 22f * k + 160f * _boost));
                for (int i = 0; i < _rays.Length; i++)
                {
                    float f = 0.5f + 0.5f * Mathf.Sin(_time * (1.3f + i * 0.17f) + i * 2.1f);
                    float a = _tier == 0 ? 0f : (0.05f + 0.22f * k) * (0.4f + 0.6f * f) + 0.6f * _boost;
                    _rays[i].color = WithA(Color.Lerp(col, Color.white, 0.25f), a);
                    _rays[i].rectTransform.localScale = new Vector3(1f, 0.75f + 0.55f * k + 0.25f * f + 1.2f * _boost, 1f);
                }
            }

            _stone.color = Color.Lerp(Color.white, Color.Lerp(col, Color.white, 0.5f), 0.12f * k);

            // 금: 해당 단계부터 빛나며 갈라짐(새로 생길 땐 하얗게 번쩍)
            for (int lv = 0; lv < 3; lv++)
            {
                bool on = _tier >= SealArt.CrackTiers[lv];
                _cracks[lv].enabled = on;
                if (!on) continue;
                _crackPop[lv] = Mathf.Min(PopTime, _crackPop[lv] + dt);
                float p = 1f - _crackPop[lv] / PopTime;
                float glow = 0.7f + 0.3f * pulseMul * Mathf.Sin(_time * 3.4f + lv * 1.7f);
                _cracks[lv].color = WithA(Color.Lerp(Color.Lerp(col, Color.white, 0.3f), Color.white, p), glow);
            }

            // 룬: 켜진 것만 색이 들어오고 뒤에 은은한 빛, 새로 켜지면 튀어 오름
            for (int i = 0; i < 10; i++)
            {
                bool lit = i < _tier;
                _runePop[i] = Mathf.Min(PopTime, _runePop[i] + dt);
                float pk = _runePop[i] / PopTime;
                float pop = 1f + 0.9f * Mathf.Sin(pk * Mathf.PI) * (1f - pk);
                _runes[i].enabled = lit;
                float g = 0.5f + 0.5f * pulseMul * Mathf.Sin(_time * 3.1f + i * 0.9f);
                if (lit)
                {
                    _runes[i].color = Color.Lerp(Color.Lerp(col, Color.white, 0.35f + 0.15f * g), Color.white, 1f - pk);
                    _runes[i].rectTransform.localScale = Vector3.one * pop;
                }
                _runeGlows[i].color = lit ? WithA(col, (0.35f + 0.25f * g + 0.6f * (1f - pk)) * glowMul) : new Color(0f, 0f, 0f, 0f);
                _runeGlows[i].rectTransform.localScale = Vector3.one * (lit ? 1f + 0.8f * (1f - pk) : 1f);
            }

            // 수정 핵: 봉인 상태면 어둡고, 풀수록 밝게 맥동
            _core.color = _tier == 0 ? new Color(0.38f, 0.38f, 0.48f) : Color.Lerp(col, Color.white, 0.2f + 0.2f * pulse * k);
            _core.rectTransform.localScale = Vector3.one * (1f + 0.05f * k * pulse + 0.3f * _boost);
            _coreGlow.color = WithA(col, _tier == 0 ? 0f : (0.25f + 0.45f * k + 0.12f * pulse + _boost) * glowMul);
            _coreGlow.rectTransform.localScale = Vector3.one * (0.8f + 0.5f * k + 0.1f * pulse + 2.5f * _boost);

            _flashA = Mathf.MoveTowards(_flashA, 0f, dt * 2.4f);
            _flash.color = WithA(Color.Lerp(col, Color.white, 0.6f), _flashA);
            _flash.rectTransform.localScale = Vector3.one * (0.6f + (1f - _flashA) * 0.8f);

            // 불씨: 원판 둘레에서 피어올라 위로 흩날림(단계가 높을수록 많이)
            if (_big && _tier > 0)
            {
                _ember += dt * (2f + 14f * k);
                while (_ember >= 1f)
                {
                    _ember -= 1f;
                    float ang = Random.Range(0f, Mathf.PI * 2f);
                    var pos = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * SealArt.Px * _s * Random.Range(0.35f, 0.55f);
                    Spawn(pos, new Vector2(Random.Range(-18f, 18f), Random.Range(40f, 110f)) * _s / 5f, Color.Lerp(col, Color.white, Random.value * 0.4f),
                          Random.Range(1.4f, 2.6f), _s * Random.Range(1.2f, 2.2f), _s * 0.4f, 0f);
                }
            }

            TickParticles(dt);
            _boost = Mathf.MoveTowards(_boost, 0f, dt * 0.8f);
        }

        void Burst(Vector2 at, Color col, int count, float speed)
        {
            for (int i = 0; i < count; i++)
            {
                float ang = Random.Range(0f, Mathf.PI * 2f);
                float sp = speed * Random.Range(0.35f, 1f) * _s / 5f;
                Spawn(at, new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * sp, Color.Lerp(col, Color.white, Random.value * 0.5f),
                      Random.Range(0.35f, 0.8f), _s * Random.Range(1.5f, 3f), _s * 0.3f, 3.5f);
            }
        }

        void Ring(Vector2 at, Color col, float from, float to, float life)
        {
            var p = Spawn(at, Vector2.zero, col, life, from, to, 0f);
            p.Img.sprite = FxSprites.RingSprite;
        }

        Particle Spawn(Vector2 pos, Vector2 vel, Color col, float life, float size, float endSize, float drag)
        {
            Particle p = null;
            foreach (var q in _parts) if (!q.Active) { p = q; break; }
            if (p == null)
            {
                p = new Particle { Img = NewImage(_fx, FxSprites.DotSprite, Vector2.zero, Vector2.one) };
                _parts.Add(p);
            }
            p.Img.sprite = FxSprites.DotSprite;
            p.Img.enabled = true;
            p.Active = true;
            p.Pos = pos; p.Vel = vel; p.Col = col; p.Age = 0f; p.Life = life; p.Size = size; p.EndSize = endSize; p.Drag = drag;
            return p;
        }

        void TickParticles(float dt)
        {
            foreach (var p in _parts)
            {
                if (!p.Active) continue;
                p.Age += dt;
                if (p.Age >= p.Life) { p.Active = false; p.Img.enabled = false; continue; }
                float t = p.Age / p.Life;
                p.Vel *= Mathf.Exp(-p.Drag * dt);
                p.Pos += p.Vel * dt;
                var rt = p.Img.rectTransform;
                rt.anchoredPosition = p.Pos;
                float size = Mathf.Lerp(p.Size, p.EndSize, t);
                rt.sizeDelta = new Vector2(size, size);
                p.Img.color = WithA(p.Col, p.Col.a * (1f - t * t));
            }
        }

        static Color WithA(Color c, float a) { c.a = Mathf.Clamp01(a); return c; }

        static RectTransform NewRect(string name, Transform parent, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            return rt;
        }

        static Image NewImage(Transform parent, Sprite sprite, Vector2 pos, Vector2 size)
        {
            var go = new GameObject("Image", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return img;
        }
    }
}
