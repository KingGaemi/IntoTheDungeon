using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Features.Attack;

namespace IntoTheDungeon.Unity.Hud
{
    /// <summary>
    /// 패턴 끊기 연출(InterruptFeed를 받음): 끊긴 몹 머리 위에 「끊김!」(보스 내려찍기는 「패턴 파괴!」)이 튀어나오고,
    /// X자 균열 섬광·금빛 충격파·파편이 터지며 화면이 아주 잠깐 멈칫한다(히트스톱). 보스일 땐 화면이 번쩍이고 더 크게.
    /// 연출은 실제 시간으로 돌아 히트스톱 중에도 움직인다.
    /// </summary>
    public sealed class InterruptFxRenderer : MonoBehaviour, IWorldInjectable
    {
        [SerializeField] Color attackColor = new(1f, 0.78f, 0.3f, 1f);
        [SerializeField] Color patternColor = new(1f, 0.93f, 0.55f, 1f);
        [Tooltip("저지력이 모자랄 때(「저지력 부족 10/15」)")]
        [SerializeField] Color progressColor = new(1f, 0.42f, 0.38f, 1f);   // StopPowerUi.ShortColor와 같은 빨강(부족 = 부정 신호)
        [Tooltip("평타를 끊었을 때(글자 없이 충격파·먼지만)")]
        [SerializeField] Color impactColor = new(1f, 0.9f, 0.75f, 1f);
        [SerializeField] float impactHitStop = 0.06f;
        [SerializeField] float attackHitStop = 0.09f;
        [SerializeField] float patternHitStop = 0.25f;
        [SerializeField] int fontSize = 64;
        [SerializeField] float characterSize = 0.05f;

        const float Life = 1.15f;

        sealed class Fx
        {
            public GameObject Root;
            public float Born;
            public bool Big, Progress, Impact;
            public Color Col;
            public Transform TextRoot;
            public TextMesh Text, Shadow;
            public SpriteRenderer Ring, Ring2, SlashA, SlashB, Glow;
            public readonly List<(SpriteRenderer r, Vector3 vel)> Shards = new();
        }

        readonly List<Fx> _fx = new();
        Font _font;
        int _layer;
        Image _flash;
        float _flashA;

        public void Init(IWorld world)
        {
            if (!_font) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            int id = SortingLayer.NameToID("Projectiles");   // 캐릭터·체력바보다 위
            _layer = SortingLayer.IsValid(id) ? id : 0;
            InterruptFeed.Pending.Clear();
            if (!_flash) BuildFlash();
        }

        void LateUpdate()
        {
            float now = Time.unscaledTime;
            float dt = Time.unscaledDeltaTime;

            foreach (var info in InterruptFeed.Pending) Spawn(info, now);
            InterruptFeed.Pending.Clear();

            for (int i = _fx.Count - 1; i >= 0; i--)
            {
                var f = _fx[i];
                float age = now - f.Born;
                if (age >= Life) { Destroy(f.Root); _fx.RemoveAt(i); continue; }
                Animate(f, age, dt);
            }

            _flashA = Mathf.MoveTowards(_flashA, 0f, dt * 2.2f);
            _flash.color = new Color(1f, 0.95f, 0.8f, _flashA);
            if (_flash.enabled != _flashA > 0f) _flash.enabled = _flashA > 0f;
        }

        /// <summary>끊김·저지력 부족 연출이 나올 때(안내 HUD 등이 받음).</summary>
        public static event System.Action<InterruptKind> Shown;

        void Spawn(in InterruptInfo info, float now)
        {
            Shown?.Invoke(info.Kind);
            bool big = info.Kind is InterruptKind.PatternBroken or InterruptKind.Shattered;
            bool progress = info.Kind is InterruptKind.StopFailed or InterruptKind.Revived;   // 저지력 부족·부활: 작게, 멈칫 없이
            var col = big ? patternColor : progress ? progressColor : attackColor;
            bool impact = info.Kind == InterruptKind.AttackBroken;   // 평타 끊기: 글자·X자 없이 묵직하게 밀려나는 충격만
            var f = new Fx { Born = now, Big = big, Progress = progress, Impact = impact, Col = impact ? impactColor : col };
            f.Root = new GameObject(big ? "PatternBroken" : "AttackBroken");
            f.Root.transform.SetParent(transform, false);
            var chest = new Vector3(info.Position.X, info.Position.Y + info.Height * 0.55f, 0f);
            f.Root.transform.position = chest;

            const int order = 32650;
            float s = big ? 1.8f : progress ? 0.7f : 1f;
            f.Glow = Sprite(f.Root.transform, FxSprites.DotSprite, Vector3.zero, new Vector3(1.6f, 1.6f, 1f) * s, order);
            f.Ring = Sprite(f.Root.transform, FxSprites.RingSprite, Vector3.zero, Vector3.one, order + 1);
            f.Ring2 = Sprite(f.Root.transform, FxSprites.RingSprite, Vector3.zero, Vector3.one, order + 1);
            // X자 균열 섬광: 끊었다는 표시
            f.SlashA = Sprite(f.Root.transform, FxSprites.BeamSprite, Vector3.zero, Vector3.one, order + 2);
            f.SlashB = Sprite(f.Root.transform, FxSprites.BeamSprite, Vector3.zero, Vector3.one, order + 2);
            f.SlashA.transform.localRotation = Quaternion.Euler(0f, 0f, 35f);
            f.SlashB.transform.localRotation = Quaternion.Euler(0f, 0f, -35f);

            int shards = big ? 22 : progress ? 5 : 11;
            for (int k = 0; k < shards; k++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float sp = Random.Range(2.5f, 5.5f) * (big ? 1.4f : 1f);
                var r = Sprite(f.Root.transform, FxSprites.DotSprite, Vector3.zero, Vector3.one * Random.Range(0.08f, 0.16f) * s, order + 3);
                f.Shards.Add((r, new Vector3(Mathf.Cos(a) * sp, Mathf.Sin(a) * sp * 0.75f + 1.2f, 0f)));
            }

            _fx.Add(f);
            if (impact)
            {
                SpeedControlHud.HitStop(impactHitStop);
                return;
            }

            // 글자(머리 위)
            f.TextRoot = new GameObject("Text").transform;
            f.TextRoot.SetParent(f.Root.transform, false);
            f.TextRoot.position = new Vector3(info.Position.X, info.Position.Y + info.Height + 0.35f, 0f);
            string label = info.Kind == InterruptKind.Shattered ? "분쇄!"
                : info.Kind == InterruptKind.Revived ? "부활!"
                : big ? $"패턴 파괴!\n<size=40>기절 {PatternBreak.PatternStun(info.Power, info.Required):0.0}초</size>" : progress ? $"저지력 부족 {info.Power}/{info.Required}" : "끊김!";   // 저지력이 남은 만큼 기절이 길다
            f.Shadow = MakeText(f.TextRoot, label, new Vector3(0.04f, -0.04f, 0f), order + 4);
            f.Shadow.color = new Color(0.25f, 0.12f, 0f, 0.9f);
            f.Text = MakeText(f.TextRoot, label, Vector3.zero, order + 5);
            f.Text.color = col;

            if (progress) return;
            SpeedControlHud.HitStop(big ? patternHitStop : attackHitStop);
            if (big) _flashA = Mathf.Max(_flashA, 0.5f);
        }

        void Animate(Fx f, float age, float dt)
        {
            float big = f.Big ? 1.8f : f.Progress ? 0.7f : 1f;
            float t = age / Life;

            // 글자: 크게 튀어나왔다 자리 잡고 살짝 떠오른 뒤 사라짐
            if (f.TextRoot)
            {
                float punch = age < 0.14f ? Mathf.Lerp(2.3f, 0.9f, age / 0.14f) : age < 0.24f ? Mathf.Lerp(0.9f, 1f, (age - 0.14f) / 0.1f) : 1f;
                f.TextRoot.localScale = Vector3.one * punch * (f.Big ? 1.45f : f.Progress ? 0.8f : 1f);
                f.TextRoot.localPosition += new Vector3(0f, dt * (age < 0.3f ? 0f : 0.35f), 0f);
                float ta = t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f;
                f.Text.color = new Color(f.Col.r, f.Col.g, f.Col.b, ta);
                f.Shadow.color = new Color(0.25f, 0.12f, 0f, 0.9f * ta);
                // 처음 한 순간은 하얗게
                if (age < 0.08f) f.Text.color = new Color(1f, 1f, 1f, 1f);
            }

            // 충격파 두 겹(두 번째는 조금 늦게)
            Ring(f.Ring, age, 0.45f, 0.3f, 2.4f * big, f.Col);
            Ring(f.Ring2, age - 0.08f, 0.5f, 0.2f, 1.7f * big, Color.white);

            // X자 섬광: 순간 길게 번쩍였다가 얇아지며 사라짐
            float sk = Mathf.Clamp01(age / 0.28f);
            float slashA = 1f - sk;
            var sc = new Vector3(0.18f * big * (1f - 0.6f * sk), 1.6f * big * (0.6f + 0.6f * sk), 1f);
            foreach (var sl in new[] { f.SlashA, f.SlashB })
            {
                sl.transform.localScale = sc;
                sl.transform.localPosition = sl.transform.localRotation * new Vector3(0f, -sc.y * 0.5f, 0f);   // 가운데 기준
                sl.color = new Color(1f, 1f, 1f, f.Progress || f.Impact ? 0f : slashA);   // 못 끊었거나 평타 끊기면 X자 없음
            }

            // 빛 덩어리
            float g = Mathf.Clamp01(1f - age / 0.35f);
            f.Glow.color = new Color(f.Col.r, f.Col.g, f.Col.b, 0.75f * g);
            f.Glow.transform.localScale = Vector3.one * (1.2f + 1.2f * (1f - g)) * big;

            // 파편: 튀어 나가며 느려지고 살짝 떨어짐
            for (int k = 0; k < f.Shards.Count; k++)
            {
                var (r, vel) = f.Shards[k];
                vel *= Mathf.Exp(-3.5f * dt);
                vel.y -= 4f * dt;
                r.transform.localPosition += vel * dt;
                r.color = new Color(f.Col.r, f.Col.g, f.Col.b, Mathf.Clamp01(1f - t * 1.3f));
                f.Shards[k] = (r, vel);
            }
        }

        static void Ring(SpriteRenderer r, float age, float life, float from, float to, Color col)
        {
            if (age < 0f || age > life) { r.enabled = false; return; }
            r.enabled = true;
            float k = age / life;
            float e = 1f - (1f - k) * (1f - k);
            float size = Mathf.Lerp(from, to, e);
            r.transform.localScale = new Vector3(size, size * 0.6f, 1f);
            r.color = new Color(col.r, col.g, col.b, 1f - k);
        }

        SpriteRenderer Sprite(Transform parent, Sprite sprite, Vector3 local, Vector3 scale, int order)
        {
            var go = new GameObject("Fx", typeof(SpriteRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localScale = scale;
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingLayerID = _layer;
            sr.sortingOrder = order;
            return sr;
        }

        TextMesh MakeText(Transform parent, string text, Vector3 local, int order)
        {
            var go = new GameObject("T", typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            var tm = go.AddComponent<TextMesh>();
            tm.font = _font;
            tm.text = text;
            tm.fontSize = fontSize;
            tm.characterSize = characterSize;
            tm.fontStyle = FontStyle.Bold;
            tm.anchor = TextAnchor.LowerCenter;
            tm.alignment = TextAlignment.Center;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = _font.material;
            mr.sortingLayerID = _layer;
            mr.sortingOrder = order;
            return tm;
        }

        void BuildFlash()
        {
            var canvasGo = new GameObject("InterruptFlash", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 220;   // 전투 HUD 아래(글자·버튼은 가리지 않게)
            var go = new GameObject("Flash", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(canvasGo.transform, false);
            _flash = go.GetComponent<Image>();
            _flash.raycastTarget = false;
            var rt = _flash.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            _flash.enabled = false;
        }
    }
}
