using System.Collections.Generic;
using UnityEngine;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.ECS.Components;
using IntoTheDungeon.Features.Battle;
using IntoTheDungeon.Features.Physics.Components;

namespace IntoTheDungeon.Unity.Hud
{
    /// <summary>
    /// 처치·레벨 업 이펙트(BattleRewardEvents를 받음).
    /// 처치: 시체에서 동전이 튀어 올라 떨어졌다 사라지고, 머리 위로 "+N 경험치"·"+N 골드"가 떠오른다.
    /// 레벨 업: 캐릭터를 따라다니며 금빛 기둥이 솟고, 발밑에 고리가 퍼지고, 반짝이가 올라가며 "레벨 업!"이 튄다.
    /// </summary>
    public sealed class RewardFxRenderer : MonoBehaviour, IWorldInjectable
    {
        [Header("색")]
        [SerializeField] Color xpColor = new(0.55f, 0.9f, 1f, 1f);
        [SerializeField] Color goldColor = new(1f, 0.82f, 0.25f, 1f);
        [SerializeField] Color levelColor = new(1f, 0.88f, 0.35f, 1f);

        [Header("처치")]
        [SerializeField] float coinSize = 0.16f;
        [SerializeField] float coinGravity = 14f;
        [SerializeField] float coinLife = 1.1f;
        [SerializeField] float textLife = 1.1f;
        [SerializeField] float textRise = 0.7f;

        [Header("레벨 업")]
        [SerializeField] float levelUpTime = 1.6f;
        [SerializeField] float pillarWidth = 2f;
        [SerializeField] float pillarHeight = 3f;

        const int FontSize = 64;
        const int TopOrder = 32700;

        IWorld _world;
        Font _font;
        int _layer;
        Sprite _dot, _ring, _beam;

        // 하나의 이펙트 조각: 매 프레임 Step이 false를 돌려주면 지운다
        sealed class Fx
        {
            public GameObject Go;
            public float Age;
            public System.Func<Fx, float, bool> Step;
            public Vector3 Pos, Vel;
            public Entity Follow;
            public SpriteRenderer Sprite;
            public TextMesh Text, Shadow;
            public float Delay, Seed;
        }

        readonly List<Fx> _fx = new();

        public void Init(IWorld world)
        {
            _world = world;
            if (!_font) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            int id = SortingLayer.NameToID("Projectiles");   // 캐릭터·체력바보다 위
            _layer = SortingLayer.IsValid(id) ? id : 0;
            _dot = FxSprites.DotSprite; _ring = FxSprites.RingSprite; _beam = FxSprites.BeamSprite;
            BattleRewardEvents.Killed -= OnKilled;
            BattleRewardEvents.Killed += OnKilled;
            BattleRewardEvents.LeveledUp -= OnLeveledUp;
            BattleRewardEvents.LeveledUp += OnLeveledUp;
        }

        void OnDestroy()
        {
            BattleRewardEvents.Killed -= OnKilled;
            BattleRewardEvents.LeveledUp -= OnLeveledUp;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            for (int i = _fx.Count - 1; i >= 0; i--)
            {
                var f = _fx[i];
                if (f.Delay > 0f) { f.Delay -= dt; f.Go.SetActive(f.Delay <= 0f); if (f.Delay > 0f) continue; }
                f.Age += dt;
                if (!f.Go || !f.Step(f, dt))
                {
                    if (f.Go) Destroy(f.Go);
                    _fx.RemoveAt(i);
                }
            }
        }

        // ───────── 처치: 동전 + 숫자 ─────────

        void OnKilled(KillRewardEvent e)
        {
            var feet = new Vector3(e.Position.X, e.Position.Y, 0f);
            var head = new Vector3(e.Position.X, e.Position.Y + e.Height, 0f);

            int coins = Mathf.Clamp(2 + e.Gold / 3, 3, 8);
            for (int i = 0; i < coins; i++)
            {
                var f = NewSprite("Coin", _dot, goldColor, TopOrder - 10);
                f.Pos = feet + new Vector3(0f, 0.5f, 0f);
                f.Vel = new Vector3(Random.Range(-1.8f, 1.8f), Random.Range(3.5f, 5.5f), 0f);
                f.Seed = feet.y + Random.Range(-0.15f, 0.15f);   // 떨어질 바닥
                f.Step = StepCoin;
                f.Go.transform.localScale = Vector3.one * coinSize;
            }

            AddFloat($"+{e.Xp} 경험치", xpColor, head + new Vector3(0f, 0.35f, 0f), 0f);
            AddFloat($"+{e.Gold} 골드", goldColor, head + new Vector3(0f, 0.05f, 0f), 0.12f);
        }

        bool StepCoin(Fx f, float dt)
        {
            f.Vel.y -= coinGravity * dt;
            f.Pos += f.Vel * dt;
            if (f.Pos.y < f.Seed && f.Vel.y < 0f)
            {
                f.Pos.y = f.Seed;
                f.Vel = new Vector3(f.Vel.x * 0.5f, -f.Vel.y * 0.35f, 0f);   // 통 튀고 멈춤
            }
            float t = f.Age / coinLife;
            float spin = Mathf.Abs(Mathf.Cos(f.Age * 14f + f.Seed * 7f));   // 빙글빙글(가로 폭만 줄였다 폈다)
            float fade = t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f;
            f.Go.transform.position = f.Pos;
            f.Go.transform.localScale = new Vector3(coinSize * (0.35f + 0.65f * spin), coinSize, 1f);
            var c = goldColor; c.a = fade;
            f.Sprite.color = c;
            return t < 1f;
        }

        void AddFloat(string text, Color color, Vector3 start, float delay)
        {
            var f = NewText("Float", text, 0.04f, color, TopOrder);
            f.Pos = start;
            f.Delay = delay;
            f.Go.SetActive(delay <= 0f);
            f.Step = (x, dt) =>
            {
                float t = x.Age / textLife;
                float up = (1f - (1f - t) * (1f - t)) * textRise;
                float pop = x.Age < 0.1f ? Mathf.Lerp(1.4f, 1f, x.Age / 0.1f) : 1f;
                float a = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
                x.Go.transform.position = x.Pos + new Vector3(0f, up, 0f);
                x.Go.transform.localScale = Vector3.one * pop;
                SetTextAlpha(x, color, a);
                return t < 1f;
            };
            LateStep(f);
        }

        // ───────── 레벨 업 ─────────

        void OnLeveledUp(LevelUpEvent e)
        {
            // 금빛 기둥: 폭이 확 벌어졌다가 가늘어지며 사라짐
            var pillar = NewSprite("LevelPillar", _beam, levelColor, TopOrder - 20);
            pillar.Follow = e.Entity;
            pillar.Step = (f, dt) =>
            {
                float t = f.Age / (levelUpTime * 0.75f);
                float w = t < 0.15f ? t / 0.15f : 1f - (t - 0.15f) / 0.85f;
                if (!FeetOf(f.Follow, out var feet)) return false;
                f.Go.transform.position = feet;
                f.Go.transform.localScale = new Vector3(pillarWidth * Mathf.Max(0f, w), pillarHeight * (0.8f + 0.2f * Mathf.Min(1f, t * 4f)), 1f);
                var c = levelColor; c.a = 0.55f * Mathf.Clamp01(w * 1.5f);
                f.Sprite.color = c;
                return t < 1f;
            };

            // 발밑 고리 두 개가 차례로 퍼짐
            for (int r = 0; r < 2; r++)
            {
                var ring = NewSprite("LevelRing", _ring, levelColor, TopOrder - 19);
                ring.Follow = e.Entity;
                ring.Delay = r * 0.25f;
                ring.Go.SetActive(r == 0);
                ring.Step = (f, dt) =>
                {
                    float t = f.Age / 0.7f;
                    if (!FeetOf(f.Follow, out var feet)) return false;
                    float s = Mathf.Lerp(0.3f, 2.2f, 1f - (1f - t) * (1f - t));
                    f.Go.transform.position = feet;
                    f.Go.transform.localScale = new Vector3(s, s * 0.4f, 1f);   // 바닥에 누운 타원
                    var c = levelColor; c.a = 1f - t;
                    f.Sprite.color = c;
                    return t < 1f;
                };
            }

            // 반짝이: 몸 주변에서 위로 올라감
            for (int i = 0; i < 14; i++)
            {
                var sp = NewSprite("LevelSparkle", _dot, Color.white, TopOrder - 5);
                sp.Follow = e.Entity;
                sp.Delay = Random.Range(0f, 0.5f);
                sp.Go.SetActive(false);
                sp.Pos = new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(0f, 0.6f), 0f);   // 발 기준 상대 위치
                sp.Vel = new Vector3(0f, Random.Range(1.2f, 2.4f), 0f);
                sp.Seed = Random.Range(0.06f, 0.12f);
                sp.Step = (f, dt) =>
                {
                    float t = f.Age / 0.9f;
                    if (!FeetOf(f.Follow, out var feet)) return false;
                    f.Pos += f.Vel * dt;
                    f.Go.transform.position = feet + f.Pos;
                    f.Go.transform.localScale = Vector3.one * f.Seed * (1f - t * 0.5f);
                    var c = Color.Lerp(Color.white, levelColor, t); c.a = 1f - t;
                    f.Sprite.color = c;
                    return t < 1f;
                };
            }

            // "레벨 업!" + "Lv.N"
            AddLevelText("레벨 업!", 0.06f, 0.55f, e);
            AddLevelText($"Lv.{e.Level}", 0.04f, 0.2f, e);
        }

        void AddLevelText(string text, float size, float above, LevelUpEvent e)
        {
            var f = NewText("LevelText", text, size, levelColor, TopOrder + 2);
            f.Follow = e.Entity;
            f.Step = (x, dt) =>
            {
                float t = x.Age / levelUpTime;
                if (!HeadOf(x.Follow, out var head)) return false;
                float pop = x.Age < 0.15f ? Mathf.Lerp(0.3f, 1.3f, x.Age / 0.15f) : x.Age < 0.3f ? Mathf.Lerp(1.3f, 1f, (x.Age - 0.15f) / 0.15f) : 1f;
                float a = t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f;
                x.Go.transform.position = head + new Vector3(0f, above + 0.4f * t, 0f);
                x.Go.transform.localScale = Vector3.one * pop;
                SetTextAlpha(x, levelColor, a);
                return t < 1f;
            };
            LateStep(f);
        }

        // ───────── 도우미 ─────────

        bool FeetOf(Entity e, out Vector3 pos)
        {
            pos = default;
            var em = _world?.EntityManager;
            if (em == null || !em.Exists(e) || !em.TryGetComponent<TransformComponent>(e, out var t)) return false;
            pos = new Vector3(t.Position.X, t.Position.Y + t.Height, 0f);
            return true;
        }

        bool HeadOf(Entity e, out Vector3 pos)
        {
            if (!FeetOf(e, out pos)) return false;
            float head = _world.EntityManager.TryGetComponent<BodyComponent>(e, out var body) ? body.HeadHeight : 1.2f;
            pos.y += head + 0.35f;
            return true;
        }

        // 만든 프레임에 한 번 위치를 잡아 (0,0)에서 깜빡이지 않게
        void LateStep(Fx f) { if (f.Delay <= 0f && !f.Step(f, 0f)) f.Go.SetActive(false); }

        Fx NewSprite(string name, Sprite sprite, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingLayerID = _layer;
            sr.sortingOrder = order;
            var f = new Fx { Go = go, Sprite = sr };
            _fx.Add(f);
            return f;
        }

        Fx NewText(string name, string text, float charSize, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var f = new Fx { Go = go };
            f.Shadow = MakeTextMesh(go.transform, new Vector3(0.025f, -0.025f, 0f), text, charSize, new Color(0f, 0f, 0f, 0.85f), order);
            f.Text = MakeTextMesh(go.transform, Vector3.zero, text, charSize, color, order + 1);
            _fx.Add(f);
            return f;
        }

        TextMesh MakeTextMesh(Transform parent, Vector3 local, string text, float charSize, Color color, int order)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            var tm = go.AddComponent<TextMesh>();
            tm.font = _font;
            tm.fontSize = FontSize;
            tm.characterSize = charSize;
            tm.fontStyle = FontStyle.Bold;
            tm.anchor = TextAnchor.LowerCenter;
            tm.alignment = TextAlignment.Center;
            tm.text = text;
            tm.color = color;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = _font.material;
            mr.sortingLayerID = _layer;
            mr.sortingOrder = order;
            return tm;
        }

        static void SetTextAlpha(Fx f, Color color, float a)
        {
            var c = color; c.a = a;
            f.Text.color = c;
            f.Shadow.color = new Color(0f, 0f, 0f, 0.85f * a);
        }
    }
}
