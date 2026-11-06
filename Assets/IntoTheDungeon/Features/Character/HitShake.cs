using UnityEngine;
using IntoTheDungeon.Core.Abstractions.Messages.Combat;

namespace IntoTheDungeon.Features.Character
{
    /// <summary>
    /// 피격 시 스프라이트만 흔든다(로직 위치·판정에는 영향 없음).
    /// 세기·길이는 최대 HP 대비 피해 비율 r로 정한다: w = ln(1 + k·r) / ln(1 + k).
    /// 작은 피해도 체감되고 큰 피해는 점점 덜 늘어나며, r = 1(한 방에 최대 HP)에서 w = 1.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HitShake : MonoBehaviour, IDamageEventListener
    {
        [Header("피해 비율 → 세기 매핑")]
        [Tooltip("클수록 작은 피해가 크게 느껴진다. 0에 가까우면 직선 비례")]
        [SerializeField] float sensitivity = 9f;

        [Header("흔들림 폭 (월드 단위)")]
        [SerializeField] float minAmplitude = 0.1f;
        [SerializeField] float maxAmplitude = 0.25f;

        [Header("지속 시간 (초)")]
        [SerializeField] float minDuration = 0.25f;
        [SerializeField] float maxDuration = 0.5f;

        [Header("모양")]
        [Tooltip("초당 방향 바꾸는 횟수. 프레임레이트 이상이면 매 프레임")]
        [SerializeField] float frequency = 60f;
        [Tooltip("직전 방향과 최소 이만큼(도) 다르게 튄다. 클수록 덜덜 떠는 느낌")]
        [Range(0f, 180f)]
        [SerializeField] float minAngleChange = 90f;

        Vector3 _baseLocal;
        float _amplitude;
        float _duration;
        float _elapsed;
        bool _active;

        Vector2 _dir = Vector2.right;   // 지금 튀어 있는 방향(단위벡터)
        float _nextJumpAt;

        void Awake() => _baseLocal = transform.localPosition;

        public void OnDamaged(in DamagedEvent evt)
        {
            if (evt.Amount <= 0) return;

            float t = Weight(evt.Ratio, sensitivity);
            float scale = IntoTheDungeon.Features.Settings.GameSettings.ShakeScale;   // 설정 화면의 흔들림 세기
            if (scale <= 0f) return;
            float amp = Mathf.Lerp(minAmplitude, maxAmplitude, t) * scale;
            float dur = Mathf.Lerp(minDuration, maxDuration, t);

            // 흔들리는 중에 또 맞으면 남은 세기보다 셀 때만 새로 시작
            if (_active && amp <= CurrentAmplitude()) return;

            _amplitude = amp;
            _duration = dur;
            _elapsed = 0f;
            _active = true;
            _nextJumpAt = 0f;
#if UNITY_EDITOR
            if (IntoTheDungeon.Unity.DebugTools.EntityStatusViewer.LogAnimation)
                Debug.Log($"[Shake] {transform.root.name} ratio={evt.Ratio:0.###} weight={t:0.##} amp={amp:0.###} dur={dur:0.##}");
#endif
        }

        public static float Weight(float ratio, float k)
        {
            float r = Mathf.Clamp01(ratio);
            if (k <= 0.0001f) return r;
            return Mathf.Log(1f + k * r) / Mathf.Log(1f + k);
        }

        static Vector2 NextDirection(Vector2 prev, float minAngleDeg)
        {
            float prevAngle = Mathf.Atan2(prev.y, prev.x) * Mathf.Rad2Deg;
            float delta = Random.Range(minAngleDeg, 360f - minAngleDeg);
            float a = (prevAngle + delta) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        }

        float CurrentAmplitude() => _duration > 0f ? _amplitude * (1f - _elapsed / _duration) : 0f;

        void LateUpdate()
        {
            if (!_active) return;

            _elapsed += Time.unscaledDeltaTime;   // 히트스톱·배속과 무관하게 같은 길이(연출)
            if (_elapsed >= _duration)
            {
                _active = false;
                transform.localPosition = _baseLocal;
                return;
            }

            // 일정 간격마다 무작위 방향으로 튄다(직전 방향과는 minAngleChange 이상 차이)
            if (_elapsed >= _nextJumpAt)
            {
                _dir = NextDirection(_dir, minAngleChange);
                _nextJumpAt = _elapsed + (frequency > 0f ? 1f / frequency : _duration);
            }

            float amp = CurrentAmplitude();
            var worldOffset = new Vector3(_dir.x * amp, _dir.y * amp, 0f);

            // 부모 스케일(캐릭터마다 다름)과 무관하게 월드 단위로 흔든다
            var local = transform.parent ? transform.parent.InverseTransformVector(worldOffset) : worldOffset;
            transform.localPosition = _baseLocal + local;
        }

        void OnDisable()
        {
            _active = false;
            transform.localPosition = _baseLocal;
        }
    }
}
