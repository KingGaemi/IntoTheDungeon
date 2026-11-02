using System.Collections.Generic;
using UnityEngine;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.Abstractions.Gameplay;
using IntoTheDungeon.Features.Battle;

namespace IntoTheDungeon.Unity.Hud
{
    /// <summary>
    /// 방 끝의 문(Resources/DungeonDoor: 돌 아치·안쪽 어둠·문짝 두 장). 마지막 웨이브 구역에 들어서면 오른쪽 벽에 서 있고
    /// (적은 이 문 앞에서 나온다), 방을 깨는 순간 문짝이 안쪽으로 열리며 안에서 따뜻한 빛이 새어 나온다.
    /// 위치는 BattleFlowSystem.DoorPosition과 같다.
    /// </summary>
    public sealed class DoorRenderer : MonoBehaviour, IWorldInjectable
    {
        [SerializeField] float openTime = 0.9f;
        [SerializeField] Color lightColor = new(1f, 0.78f, 0.45f, 1f);
        const int Order = -29990;   // 바닥(-30000) 바로 위, 모든 캐릭터 뒤

        BattleProgress _progress;
        BattleConfig _config;
        RectWalkArea _area;
        GameObject _root;
        SpriteRenderer _arch, _inside, _left, _right, _glow;
        float _openAge;

        public void Init(IWorld world)
        {
            world.TryGet(out _progress);
            world.TryGet(out _config);
            if (world.TryGet(out IWalkArea area)) _area = area as RectWalkArea;
            if (!_root) Build();
        }

        void Build()
        {
            var sprites = new Dictionary<string, Sprite>();
            foreach (var s in Resources.LoadAll<Sprite>("DungeonDoor")) sprites[s.name] = s;
            _root = new GameObject("DungeonDoor");
            _root.transform.SetParent(transform, false);
            _inside = Part("Inside", sprites, "inside", Order);
            _glow = Part("Glow", null, null, Order + 1);
            _glow.sprite = FxSprites.DotSprite;
            _left = Part("LeafL", sprites, "leaf_l", Order + 2);
            _right = Part("LeafR", sprites, "leaf_r", Order + 2);
            _arch = Part("Arch", sprites, "arch", Order + 3);
            _root.SetActive(false);
        }

        SpriteRenderer Part(string name, Dictionary<string, Sprite> sprites, string key, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            if (sprites != null && sprites.TryGetValue(key, out var s)) r.sprite = s;
            r.sortingOrder = order;
            return r;
        }

        void Update()
        {
            if (_progress == null || _area == null || !_root) return;
            var p = _progress;
            bool last = p.TotalWaves > 0 && p.Wave >= p.TotalWaves && p.Phase is BattlePhase.Fighting or BattlePhase.Victory;
            // 마지막 구역으로 넘어가는 깜깜한 순간부터 보인다
            bool entering = p.Phase == BattlePhase.Interlude && p.Wave == p.TotalWaves - 1 && _config != null
                            && p.PhaseTime >= _config.AdvanceRunTime + _config.AdvanceFadeOut;
            bool show = last || entering;
            if (_root.activeSelf != show) { _root.SetActive(show); _openAge = 0f; }
            if (!show) return;

            var d = BattleFlowSystem.DoorPosition(_area);
            _root.transform.position = new Vector3(d.X, d.Y + 0.05f, 0f);

            if (p.Phase == BattlePhase.Victory) _openAge += Time.deltaTime;
            float k = Mathf.Clamp01(_openAge / openTime);
            k = 1f - (1f - k) * (1f - k) * (1f - k);   // 처음 확 열리고 끝에서 천천히
            // 문짝이 안쪽으로 돌아가며 좁아진다(경첩 쪽 피벗)
            _left.transform.localScale = _right.transform.localScale = new Vector3(1f - 0.82f * k, 1f, 1f);
            _left.color = _right.color = Color.Lerp(Color.white, new Color(0.55f, 0.5f, 0.5f, 1f), k);
            _inside.color = Color.Lerp(Color.white, lightColor, k * 0.85f);
            _glow.enabled = k > 0f;
            float flicker = 0.9f + 0.1f * Mathf.Sin(Time.time * 9f);
            _glow.transform.localPosition = new Vector3(0f, 1.0f, 0f);
            _glow.transform.localScale = new Vector3(2.4f, 2.8f, 1f) * (0.6f + 0.4f * k);
            _glow.color = new Color(lightColor.r, lightColor.g, lightColor.b, 0.55f * k * flicker);
        }
    }
}
